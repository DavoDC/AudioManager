using System;
using System.IO;
using System.Linq;

namespace AudioManager
{
    internal class Program
    {
        /// <summary>
        /// Main function
        /// </summary>
        /// <param name="args">Arguments given to program</param>
        static void Main(string[] args)
        {
            try
            {
                // Start message
                Console.WriteLine("\n###### Audio Manager ######");

                // Get the path of the executable
                string progExecPath = AppDomain.CurrentDomain.BaseDirectory;

                // Set mirror path relative to the executable
                string mirrorPath = Path.GetFullPath(Path.Combine(progExecPath, Constants.MirrorFolderPath));

                // Detect CLI args: "analysis [--force-regen]" or "integrate [--dry-run]"
                int mode;
                bool forceMirrorRegen;
                bool dryRun = false;
                if (args.Length > 0)
                {
                    string modeArg = args[0].ToLower();
                    bool forceRegen = args.Any(a => a.Equals("--force-regen", StringComparison.OrdinalIgnoreCase));
                    dryRun = args.Any(a => a.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));

                    if (modeArg == "analysis" || modeArg == "analyse" || modeArg == "analyze")
                    {
                        mode = 1;
                        forceMirrorRegen = forceRegen;
                    }
                    else if (modeArg == "integrate" || modeArg == "integration")
                    {
                        mode = 2;
                        forceMirrorRegen = false;
                    }
                    else if (modeArg == "tagfix" || modeArg == "fix-tags")
                    {
                        mode = 3;
                        forceMirrorRegen = false;
                    }
                    else
                    {
                        Console.WriteLine($"Unknown mode '{args[0]}'. Use: analysis [--force-regen] | integrate [--dry-run] | tagfix [--dry-run]");
                        Environment.Exit(1);
                        return;
                    }
                }
                else
                {
                    // Interactive menu fallback
                    mode = PromptMode();
                    forceMirrorRegen = (mode == 1) ? PromptForceMirrorRegen() : false;
                }

                // Set up file logging for all modes (console output tee'd to file + console simultaneously)
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                string modeLabel = (mode == 1) ? "analysis" : (mode == 2) ? "integrate" : "tagfix";
                string logFilePath = Path.Combine(Constants.LogsPath, $"{modeLabel}-{timestamp}.log");
                StringWriter captureWriter = (mode == 1) ? new StringWriter() : null;
                TeeWriter teeWriter = new TeeWriter(Console.Out, logFilePath, captureWriter);
                Console.SetOut(teeWriter);

                try
                {
                    if (mode == 1)
                    {
                        // Analysis mode
                        // 1) Check the age of the mirror
                        AgeChecker ac = new AgeChecker(forceMirrorRegen);

                        // 2) Create mirror of audio folder
                        // Note: Files created at this stage just contain paths to the actual file, not metadata info.
                        Reflector r = new Reflector(mirrorPath);

                        // 3) Parse metadata into XML files and tag list
                        // Note: The file contents get overwritten with actual XML content in this stage.
                        Parser p = new Parser(mirrorPath);

                        // 4) Analyse metadata and print statistics
                        Analyser a = new Analyser(p.audioTags);

                        // 5) Do audio library organisational/metadata checks
                        LibChecker lc = new LibChecker(p.audioTags);

                        // Print total time taken (captured into report)
                        Doer.PrintTotalTimeTaken();

                        // 6) Auto-commit AudioMirror if LibChecker was clean
                        // Check this before saving report so ReportWriter knows where to save it.
                        // LibChecker prints "LibChecker: Clean" to the captured stream when zero issues found.
                        bool libCheckerClean = captureWriter.ToString().Contains("LibChecker: Clean");

                        // Save report (reports with issues go to gitignored folder to avoid cluttering diffs)
                        ReportWriter.Save(captureWriter.ToString(), libCheckerClean);

                        // Auto-commit AudioMirror after report save
                        // Runs after report save so the commit output is not captured into the report.
                        AudioMirrorCommitter.TryCommit(libCheckerClean);

                        // Finish message
                        Console.WriteLine("\nFinished!\n");

                        // Analysis mode complete
                        return;
                    }
                    else if (mode == 2)
                    {
                        // Integrate mode - run pre-integration gate first
                        if (!RunPreIntegrationGate(mirrorPath))
                        {
                            Console.WriteLine("\nFix library issues before adding new songs.\n");
                            Environment.Exit(1);
                        }

                        // Gate passed - proceed with integration
                        MusicIntegrator mi = new MusicIntegrator(dryRun);

                        // Post-integration validation: regenerate mirror and run LibChecker
                        if (!dryRun)
                        {
                            Console.WriteLine("\nPost-integration validation...");
                            try
                            {
                                // Regenerate mirror to reflect newly integrated files
                                Console.WriteLine(" - Regenerating AudioMirror XMLs...");
                                Reflector r = new Reflector(mirrorPath);
                                Parser p = new Parser(mirrorPath);

                                // Run LibChecker to validate the updated library
                                Console.WriteLine(" - Running library validation...");
                                LibChecker lc = new LibChecker(p.audioTags);

                                if (lc.IsClean)
                                {
                                    Console.WriteLine(" - Post-integration validation: CLEAN (no issues found)\n");
                                }
                                else
                                {
                                    Console.WriteLine(" - Post-integration validation: ISSUES FOUND (see above)\n");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($" - WARNING: Could not run post-integration validation: {ex.Message}\n");
                            }
                        }
                    }
                    else if (mode == 3)
                    {
                        // TagFix mode - clean tags in NewMusic folder
                        TagFixer tf = new TagFixer(dryRun);
                    }

                    // Print total time taken (Integrate and TagFix modes only)
                    if (mode == 2 || mode == 3)
                    {
                        Doer.PrintTotalTimeTaken();

                        // Finish message (Integrate and TagFix modes only)
                        Console.WriteLine("\nFinished!\n");
                    }
                }
                finally
                {
                    // Restore real console output and close file logger
                    teeWriter?.CloseLogFile();
                    Console.SetOut(teeWriter.ConsoleWriter);
                    Console.WriteLine($"Log file saved: logs/{modeLabel}-{timestamp}.log");
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions
                Console.WriteLine($"\nEXCEPTION ENCOUNTERED");
                Console.WriteLine($"\nMessage: {ex.Message}");
                Console.WriteLine($"\nStack Trace: \n{ex.StackTrace}");
                Console.WriteLine("\n");
                Environment.Exit(123);
            }
        }

        /// <summary>
        /// Pre-integration validation gate: ensures AudioMirror is fresh and LibChecker is clean.
        /// </summary>
        /// <param name="mirrorPath">Path to the AudioMirror folder.</param>
        /// <returns>true if gate passes (mirror fresh and LibChecker clean), false otherwise.</returns>
        private static bool RunPreIntegrationGate(string mirrorPath)
        {
            Console.WriteLine("\nPre-integration validation...");

            try
            {
                // Get mirror repo path
                string mirrorRepoPath = Path.GetFullPath(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, Constants.MirrorRepoPath));

                if (!Directory.Exists(mirrorRepoPath))
                {
                    Console.WriteLine(" - ERROR: AudioMirror repo not found. Cannot validate library state.");
                    return false;
                }

                // Step 1: Regenerate AudioMirror XMLs
                Console.WriteLine(" - Regenerating AudioMirror XMLs...");
                Reflector r = new Reflector(mirrorPath);
                Parser p = new Parser(mirrorPath);

                // Step 2: Check if AudioMirror XMLs changed (is it fresh?)
                Console.WriteLine(" - Checking if AudioMirror is fresh...");
                string mirrorStatusOutput = RunGitStatus(mirrorRepoPath);

                // Ignore LastRunInfo.txt (just a timestamp) - only care about XML changes
                var changedFiles = mirrorStatusOutput
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => !line.Contains("LastRunInfo.txt"))
                    .ToList();

                bool mirrorIsStale = changedFiles.Count > 0;

                if (mirrorIsStale)
                {
                    Console.WriteLine(" - ERROR: AudioMirror is out of sync with the library.");
                    Console.WriteLine("   AudioMirror is the source of truth for all library validation.");
                    Console.WriteLine("   Commit the mirror changes (git commit in AudioMirror/) before integrating new songs.");
                    return false;
                }

                // Step 3: Verify LibChecker is clean
                Console.WriteLine(" - Running library validation...");
                LibChecker lc = new LibChecker(p.audioTags);

                if (!lc.IsClean)
                {
                    Console.WriteLine(" - ERROR: LibChecker found issues in the library.");
                    return false;
                }

                Console.WriteLine(" - Pre-integration validation passed.\n");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($" - ERROR during pre-integration validation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Runs 'git status --porcelain' and returns the output.
        /// </summary>
        private static string RunGitStatus(string repoPath)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("git", "status --porcelain")
                {
                    WorkingDirectory = repoPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    return output;
                }
            }
            catch (Exception)
            {
                return "error";
            }
        }

        /// <summary>
        /// Prompt the user to select the program mode.
        /// Clears the console and displays the mode menu, then reads a single key.
        /// Loops until the user selects '1' or '2'.
        /// </summary>
        /// <returns>1 for Scan, 2 for Integrate</returns>
        private static int PromptMode()
        {
            int selected = PromptMenu("Select mode:", new[] { "Analysis", "Integrate" });
            return selected + 1; // 1 = Analysis, 2 = Integrate
        }

        /// <summary>
        /// Prompt whether to force mirror regeneration. Reads a single key and loops until Y/N.
        /// </summary>
        /// <returns>true if user selects Y, false for N</returns>
        private static bool PromptForceMirrorRegen()
        {
            int selected = PromptMenu("Force mirror regeneration?", new[] { "No", "Yes" });
            return selected == 1;
        }

        /// <summary>
        /// Displays an arrow-key navigable menu and returns the index of the selected option.
        /// </summary>
        /// <param name="title">The title shown above the options.</param>
        /// <param name="options">The options to display.</param>
        /// <returns>The zero-based index of the selected option.</returns>
        private static int PromptMenu(string title, string[] options)
        {
            int selected = 0;
            while (true)
            {
                Console.Clear();
                Console.WriteLine("###### Audio Manager ######\n");
                Console.WriteLine(title + "\n");
                for (int i = 0; i < options.Length; i++)
                {
                    Console.WriteLine(i == selected ? $"  > {options[i]}" : $"    {options[i]}");
                }
                var key = Console.ReadKey(intercept: true).Key;
                if (key == ConsoleKey.UpArrow) selected = (selected == 0) ? options.Length - 1 : selected - 1;
                if (key == ConsoleKey.DownArrow) selected = (selected == options.Length - 1) ? 0 : selected + 1;
                if (key == ConsoleKey.Enter) return selected;
            }
        }
    }
}