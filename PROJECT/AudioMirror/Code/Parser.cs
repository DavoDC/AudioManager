using AudioMirror.Code.Modules;
using System;
using System.IO;
using System.Xml;

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
                // If non-XML file found, notify
                if (Path.GetExtension(mirrorFilePath) != ".xml")
                {
                    throw new ArgumentException($"Non-XML file found in mirror folder: {mirrorFilePath}");
                }

                // Try to extract real file path from file contents
                string[] fileContents = File.ReadAllLines(mirrorFilePath);
                string realFilePath = fileContents[0];

                // If real path is invalid, set to null
                if(!File.Exists(realFilePath)) 
                {
                    realFilePath = null;
                }

                // Parse audio track into XML file
                new TrackXML(mirrorFilePath, realFilePath);
            }

            // Calculate execution time
            var executionTime = DateTime.Now - startTime;

            // Print time taken
            Console.WriteLine($"\nTime taken: {Math.Round(executionTime.TotalSeconds, 3)} seconds");
        }
    }
}
