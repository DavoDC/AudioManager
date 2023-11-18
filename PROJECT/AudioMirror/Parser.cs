using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioMirror
{
    /// <summary>
    /// Parses audio track metadata into XML files
    /// </summary>
    internal class Parser
    {
        //// VARIABLES
        private string mirrorPath;

        /// <summary>
        /// Parse audio files
        /// </summary>
        /// <param name="mirrorPath">The audio mirror folder path</param>
        public Parser(string mirrorPath)
        {
            // Save start time
            var startTime = DateTime.Now;

            // Save mirror path
            this.mirrorPath = mirrorPath;

            // Notify
            Console.WriteLine("\nParsing audio metadata...");

            // For every mirrored file
            string[] mirrorFiles = Directory.GetFiles(mirrorPath, "*", SearchOption.AllDirectories);
            foreach (var mirrorFilePath in mirrorFiles)
            {
                // Extract path of real file
                string[] fileContents = File.ReadAllLines(mirrorFilePath);
                string realFilePath = fileContents[0];



                // TEST
                Console.WriteLine("realfilepath: " + realFilePath);
                break;
            }

            // Calculate execution time
            var executionTime = DateTime.Now - startTime;

            // Print time taken
            Console.WriteLine($"\nTime taken: {Math.Round(executionTime.TotalSeconds, 3)} seconds");
        }
    }
}
