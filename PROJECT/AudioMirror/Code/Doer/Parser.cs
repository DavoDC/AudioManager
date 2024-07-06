using AudioMirror.Code.Modules;
using System;
using System.Collections.Generic;
using System.IO;

namespace AudioMirror
{
    /// <summary>
    /// Parses audio track metadata into XML files.
    /// </summary>
    internal class Parser : Doer
    {
        // Properties
        public List<TrackTag> audioTags { get; }

        /// <summary>
        /// Construct a parser
        /// </summary>
        /// <param name="mirrorPath">The audio mirror folder path</param>
        public Parser(string mirrorPath)
        {
            // Notify
            Console.WriteLine("\nParsing audio metadata...");

            // Initialise tag list
            audioTags = new List<TrackTag>();

            // For every mirrored file
            string[] mirrorFiles = Directory.GetFiles(mirrorPath, "*", SearchOption.AllDirectories);
            foreach (var mirrorFilePath in mirrorFiles)
            {
                // If non-XML file found, notify
                if (Path.GetExtension(mirrorFilePath) != ".xml")
                {
                    throw new ArgumentException($"Non-XML file found in mirror folder: {mirrorFilePath}");
                }

                // Get audio file tag and add to list 
                audioTags.Add(new TrackTag(mirrorFilePath));
            }

            // Print statistics
            Console.WriteLine($" - Tags parsed: {audioTags.Count}");
            PrintTimeTaken();
        }
    }
}
