using AudioManager.Code.Modules;
using System;
using System.Collections.Generic;
using System.IO;

namespace AudioManager
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
        /// <param name="cachePath">Override cache file path. Null uses Constants.ParseCachePath (production default).</param>
        public Parser(string mirrorPath, string cachePath = null)
        {
            string effectiveCachePath = cachePath ?? Constants.ParseCachePath;

            // Notify
            Console.Write("\nParsing audio metadata...");

            // Initialise tag list
            audioTags = new List<TrackTag>();

            // Cache hit: skip all XML reads when nothing changed since last parse
            if (ParseCache.TryLoad(effectiveCachePath, mirrorPath, out var cached))
            {
                audioTags = cached;
                Console.WriteLine($" - Tags parsed: {audioTags.Count} (cache)");
                FinishAndPrintTimeTaken();
                return;
            }

            // Cache miss: parse all XMLs
            string[] mirrorFiles = Directory.GetFiles(mirrorPath, "*", SearchOption.AllDirectories);
            int parsedCount = 0;
            int parsedTotal = mirrorFiles.Length;
            int dotInterval = Math.Max(1, parsedTotal / 10);
            foreach (var mirrorFilePath in mirrorFiles)
            {
                // Skip the README file
                if (Path.GetFileName(mirrorFilePath).Equals("README.md"))
                {
                    continue;
                }

                // If non-XML file found (other than README), notify
                if (Path.GetExtension(mirrorFilePath) != ".xml" )
                {
                    throw new ArgumentException($"Non-XML file found in mirror folder: {mirrorFilePath}");
                }

                // Get audio file tag and add to list
                audioTags.Add(new TrackTag(mirrorFilePath));
                parsedCount++;
                if (parsedTotal >= 100 && parsedCount % dotInterval == 0)
                    Console.Write(".");
            }
            if (parsedTotal >= 100) Console.WriteLine();

            // Save cache so next run can skip these reads
            ParseCache.Save(effectiveCachePath, audioTags);

            // Print number of tags parsed
            Console.WriteLine($" - Tags parsed: {audioTags.Count}");

            // Finish and print time taken
            FinishAndPrintTimeTaken();
        }
    }
}
