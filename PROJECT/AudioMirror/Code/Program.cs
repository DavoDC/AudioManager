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

        // The path to the mirror folder relative to program executable
        static string relMirrorPath = projectPath + "..\\AUDIO_MIRROR";

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
            Reflector r = new Reflector(mirrorPath, ac.recreateMirror);

            // 2) Parse metadata into XML files and tag list
            Parser p = new Parser(mirrorPath);

            // 3) Analyse metadata and print statistics
            Analyser a = new Analyser(p.audioTags);

            // Print total time
            TimeSpan totalTime = r.ExecutionTime + p.ExecutionTime + a.ExecutionTime;
            Console.WriteLine("\n\nTotal time taken: " + Doer.ConvertTimeSpanToString(totalTime));

            // Finish message
            Console.WriteLine("\nFinished!\n");
        }
    }
}