using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioMirror
{
    internal class Program
    {
        /// <summary>
        /// Main function
        /// </summary>
        /// <param name="args">Arguments given to program</param>
        static void Main(string[] args)
        {
            // Start message
            Console.WriteLine("\n###### Audio Mirror ######");

            // Set mirror path relative to program executable
            string programDir = AppDomain.CurrentDomain.BaseDirectory;
            string relMirrorPath = "..\\..\\..\\..\\AUDIO_MIRROR";
            string mirrorPath = Path.GetFullPath(Path.Combine(programDir, relMirrorPath));

            // 1) Create mirror of audio folder
            Mirror mirror = new Mirror(mirrorPath);

            // 2) Parse metadata into XML files
            // Parser

            // 3) Analyse metadata
            // Analyser

            // Finish message
            Console.WriteLine("\nFinished!\n");
        }
    }
}
