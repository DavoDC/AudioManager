using System;
using System.IO;

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

                // Prompt user for mode
                int mode = PromptMode();

                if (mode == 1)
                {
                    // Scan mode

                    // Capture console output for report
                    StringWriter captureWriter = new StringWriter();
                    TeeWriter teeWriter = new TeeWriter(Console.Out, captureWriter);
                    Console.SetOut(teeWriter);

                    try
                    {
                        // Ask whether to force mirror regeneration
                        bool forceMirrorRegen = PromptForceMirrorRegen();

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
                    }
                    finally
                    {
                        // Restore console (do not save report yet)
                        Console.SetOut(teeWriter.ConsoleWriter);
                    }

                    // Print total time taken and finish message, then save report
                    Doer.PrintTotalTimeTaken();
                    Console.WriteLine("\nFinished!\n");

                    // Save report (overwrite today's report if exists)
                    new ReportWriter(captureWriter.ToString());

                    // Done for Scan mode — avoid duplicate final messages
                    return;
                }
                else if (mode == 2)
                {
                    // Integrate mode
                    MusicIntegrator mi = new MusicIntegrator();
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
            while (true)
            {
                Console.Clear();
                Console.WriteLine("###### Audio Manager ######\n");
                Console.WriteLine("Select mode:");
                Console.WriteLine("  [1] Scan");
                Console.WriteLine("  [2] Integrate\n");
                Console.WriteLine("Enter number:\n");

                var key = Console.ReadKey(intercept: true);
                if (key.KeyChar == '1') return 1;
                if (key.KeyChar == '2') return 2;

                // invalid input -> loop
            }
        }

        /// <summary>
        /// Prompt whether to force mirror regeneration. Reads a single key and loops until Y/N.
        /// </summary>
        /// <returns>true if user selects Y, false for N</returns>
        private static bool PromptForceMirrorRegen()
        {
            while (true)
            {
                Console.WriteLine(" - Force mirror regeneration?\n  [Y] Yes   [N] No\n");
                var key = Console.ReadKey(intercept: true);
                if (key.KeyChar == 'Y' || key.KeyChar == 'y') return true;
                if (key.KeyChar == 'N' || key.KeyChar == 'n') return false;

                // invalid input -> loop
            }
        }
    }
}