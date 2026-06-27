using AudioManager.Code.Modules;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

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

            // Cache miss: parse all XMLs in parallel (each file read is independent)
            string[] mirrorFiles = Directory.GetFiles(mirrorPath, "*", SearchOption.AllDirectories);
            var bag = new ConcurrentBag<TrackTag>();
            var badFiles = new ConcurrentBag<string>(); // non-XML files found (mirror corruption)

            Parallel.ForEach(mirrorFiles, mirrorFilePath =>
            {
                string name = Path.GetFileName(mirrorFilePath);
                if (name.Equals("README.md")) return;

                if (Path.GetExtension(mirrorFilePath) != ".xml")
                {
                    badFiles.Add(mirrorFilePath);
                    return;
                }

                bag.Add(new TrackTag(mirrorFilePath));
            });

            // Surface mirror corruption after parallel phase completes
            if (!badFiles.IsEmpty)
                throw new ArgumentException($"Non-XML file(s) found in mirror folder: {string.Join(", ", badFiles)}");

            audioTags = new List<TrackTag>(bag);
            Console.WriteLine();

            // Save cache so next run can skip these reads
            ParseCache.Save(effectiveCachePath, audioTags);

            // Print number of tags parsed
            Console.WriteLine($" - Tags parsed: {audioTags.Count}");

            // Finish and print time taken
            FinishAndPrintTimeTaken();
        }
    }
}
