using System;
using System.IO;

namespace AudioMirror
{
    internal class Program
    {
        //// CONSTANTS/SETTINGS

        // The path to the mirror folder relative to program executable
        static string relMirrorPath = "..\\..\\..\\..\\AUDIO_MIRROR";

        // Whether to regenerate mirror folder each time
        static bool recreateMirror = false;
        //static bool recreateMirror = true;


        /// <summary>
        /// Main function
        /// </summary>
        /// <param name="args">Arguments given to program</param>
        static void Main(string[] args)
        {
            // Start message
            Console.WriteLine("\n###### Audio Mirror ######");
            Console.WriteLine("\nDateTime.Now: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            // Set mirror path relative to program executable
            string programDir = AppDomain.CurrentDomain.BaseDirectory;
            string mirrorPath = Path.GetFullPath(Path.Combine(programDir, relMirrorPath));

            // 1) Create mirror of audio folder
            Reflector r = new Reflector(mirrorPath, recreateMirror);

            // 2) Parse metadata into XML files
            Parser p = new Parser(mirrorPath);

            // 3) Analyse metadata
            Analyser a = new Analyser(p.audioTags);

            // Print total time
            TimeSpan totalTime = r.ExecutionTime + p.ExecutionTime + a.ExecutionTime;
            Console.WriteLine("\n\nTotal time taken: " + Doer.ConvertTimeSpanToString(totalTime));

            // Finish message
            Console.WriteLine("\nFinished!\n");
        }
    }
}
