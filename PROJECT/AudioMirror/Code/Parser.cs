using AudioMirror.Code.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace AudioMirror
{
    /// <summary>
    /// Parses audio track metadata into XML files
    /// </summary>
    internal class Parser
    {
        /// <summary>
        /// Construct a parser
        /// </summary>
        /// <param name="mirrorPath">The audio mirror folder path</param>
        public Parser(string mirrorPath)
        {
            // Save start time
            var startTime = DateTime.Now;

            // Notify
            Console.WriteLine("\nParsing audio metadata...");

            // List of audio tags
            List<TrackTag> audioTags = new List<TrackTag>();

            // For every mirrored file
            string[] mirrorFiles = Directory.GetFiles(mirrorPath, "*", SearchOption.AllDirectories);
            foreach (var mirrorFilePath in mirrorFiles)
            {
                // If non-XML file found, notify
                if (Path.GetExtension(mirrorFilePath) != ".xml")
                {
                    throw new ArgumentException($"Non-XML file found in mirror folder: {mirrorFilePath}");
                }

                // Get audio file tag
                TrackTag tag = new TrackTag(mirrorFilePath);

                // If audio tag is valid
                if (tag != null)
                {
                    // Save into XML mirror file
                    TrackXML xmlFile = new TrackXML(mirrorFilePath, tag);

                    // Add tag to list
                    audioTags.Add(tag);
                }
            }

            //// Print statistics
            // Print tag count
            Console.WriteLine($" - Tags parsed: {audioTags.Count}");

            // Print time taken
            var executionTime = DateTime.Now - startTime;
            Console.WriteLine($" - Time taken: {Math.Round(executionTime.TotalSeconds, 3)} seconds");
        }
    }
}
