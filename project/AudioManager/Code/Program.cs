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
                    else
                    {
                        Console.WriteLine($"Unknown mode '{args[0]}'. Use: analysis [--force-regen] | integrate [--dry-run]");
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

                if (mode == 1)
                {
                    // Analysis mode

                    // Start capturing console output for report
                    StringWriter captureWriter = new StringWriter();
                    TeeWriter teeWriter = new TeeWriter(Console.Out, captureWriter);
                    Console.SetOut(teeWriter);

                    try
                    {
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
                    }
                    finally
                    {
                        // Restore real console output
                        Console.SetOut(teeWriter.ConsoleWriter);
                    }

                    // Save report (overwrite today's report if exists)
                    ReportWriter.Save(captureWriter.ToString());

                    // Reminder to commit AudioMirror repo (console only)
                    Console.WriteLine("\nReminder: commit and push the AudioMirror repo.");

                    // Finish message
                    Console.WriteLine("\nFinished!\n");

                    // Analysis mode complete
                    return;
                }
                else if (mode == 2)
                {
                    // Integrate mode
                    MusicIntegrator mi = new MusicIntegrator(dryRun);
                }

                // Print total time taken (Integrate mode only)
                Doer.PrintTotalTimeTaken();

                // Finish message (Integrate mode only)
                Console.WriteLine("\nFinished!\n");
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