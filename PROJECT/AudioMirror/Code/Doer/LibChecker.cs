using AudioMirror.Code.Modules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AudioMirror
{
    /// <summary>
    /// Audio library organisational/metadata checks
    /// </summary>
    internal class LibChecker : Doer
    {
        // Variables
        private List<TrackTag> audioTags;

        /// <summary>
        /// Construct a library checker
        /// </summary>
        /// <param name="audioTags"></param>
        public LibChecker(List<TrackTag> audioTags)
        {
            // Notify
            Console.WriteLine("\nChecking library...");

            // Save parameter
            this.audioTags = audioTags;

            // Check titles
            CheckTitles();

            // Print time taken
            Console.WriteLine("");
            PrintTimeTaken();
        }

        /// <summary>
        /// Check for unwanted info in titles
        /// </summary>
        public void CheckTitles()
        {
            Console.WriteLine(" - Checking titles...");
            var unwantedInfo = new[] { "feat.", "ft.", "edit", 
                "version", "original", "soundtrack" };

            int totalHits = 0;
            foreach (TrackTag tag in audioTags)
            {
                foreach (var curUnwanted in unwantedInfo)
                {
                    totalHits += CheckTitle(tag, curUnwanted);
                }
            }

            Console.WriteLine($" - Total hits: {totalHits}");
        }

        /// <summary>
        /// Print a message if a given title contains an unwanted substring
        /// </summary>
        /// <param name="tag">The track's TrackTag object</param>
        /// <param name="unwanted">The unwanted parts</param>
        /// <returns>One if unwanted part was found, zero otherwise</returns>
        public int CheckTitle(TrackTag tag, params string[] unwanted)
        {
            // If tag.Title contains any unwanted element
            if (unwanted.Any(unwantedS => tag.Title.ToLower().Contains(unwantedS.ToLower())))
            {
                // Generate short description of unwanted parts
                string unwantedDesc = string.Join("/", unwanted);

                // Print message with description and track
                Console.WriteLine($"  - Found '{unwantedDesc}' in title of '{tag.ToString()}'");

                return 1;
            }

            return 0;
        }
    }
}
