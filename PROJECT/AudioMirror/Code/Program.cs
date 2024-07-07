using System;
using System.IO;
using File = System.IO.File;

namespace AudioMirror
{
    internal class Program
    {
        //// CONSTANTS/SETTINGS

        // The path back to the project folder
        private static string projectPath = "..\\..\\..\\";
        public static string ProjectPath { get => projectPath; }

        // The mirror folder name
        private static string mirrorFolder = "AUDIO_MIRROR";
        public static string MirrorFolder { get => mirrorFolder; }

        // The path to the mirror folder relative to program executable
        private static readonly string relMirrorPath = projectPath + "..\\" + mirrorFolder;

        /// <summary>
        /// Main function
        /// </summary>
        /// <param name="args">Arguments given to program</param>
        static void Main(string[] args)
        {
            // Start message
            Console.WriteLine("\n###### Audio Mirror ######\n");

            // Set mirror path relative to program executable
            string programDir = AppDomain.CurrentDomain.BaseDirectory;
            string mirrorPath = Path.GetFullPath(Path.Combine(programDir, relMirrorPath));

            // 0) Check the age of the mirror
            AgeChecker ac = new AgeChecker();

            // 1) Create mirror of audio folder
            // Note: Files created at this stage just contain paths to the actual file, not metadata info.
            Reflector r = new Reflector(mirrorPath, ac.recreateMirror);

            // 2) Parse metadata into XML files and tag list
            // Note: The file contents get overwritten with actual XML content in this stage.
            Parser p = new Parser(mirrorPath);

            // 3) Analyse metadata and print statistics
            Analyser a = new Analyser(p.audioTags);

            // 4) Audio library organisational/metadata checks
            LibChecker lc = new LibChecker(p.audioTags);

            // Print total time
            TimeSpan totalTime = ac.ExecutionTime + r.ExecutionTime + p.ExecutionTime;
            totalTime += a.ExecutionTime + lc.ExecutionTime;
            Console.WriteLine("\n\nTotal time taken: " + Doer.ConvertTimeSpanToString(totalTime));

            // Finish message
            Console.WriteLine("\nFinished!\n");
        }
    }
}