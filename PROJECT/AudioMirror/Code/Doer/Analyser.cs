using AudioMirror.Code.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using StringIntFreqDist = System.Linq.IOrderedEnumerable<System.Collections.Generic.KeyValuePair<string, int>>;

namespace AudioMirror
{
    /// <summary>
    /// Analyses audio track metadata to produce statistics.
    /// </summary>
    internal class Analyser : Doer
    {
        // Variables
        private List<TrackTag> audioTags;

        /// <summary>
        /// Construct an audio tag analyser
        /// </summary>
        /// <param name="audioTags"></param>
        public Analyser(List<TrackTag> audioTags)
        {
            // Notify
            Console.WriteLine("\nAnalysing tags...");

            // Save parameter
            this.audioTags = audioTags;

            // Print artist stats
            PrintFreqStats("Artists", tag => tag.Artists);

            // Print genre stats
            PrintFreqStats("Genre", tag => tag.Genres);

            // Print year stats
            StringIntFreqDist yearFreqDist = PrintFreqStats("Year", tag => tag.Year);

            // Print decade/time period stats 
            PrintDecadeStats("Decade", yearFreqDist);

            // Print time taken
            Console.WriteLine("");
            PrintTimeTaken();
        }

        /// <summary>
        /// Print frequency statistics for a given track property
        /// </summary>
        /// <param name="statName">The name of the property</param>
        /// <param name="func">Function that returns the property</param>
        private StringIntFreqDist PrintFreqStats(string statName, Func<TrackTag, string> func)
        {
            // Print heading
            PrintHeading(statName);

            // Get sorted frequency distribution
            StringIntFreqDist sortedFreqDist = getSortedFreqDist(audioTags, func);

            // Print columns
            PrintColumns("%", statName, "Occurrences");

            // Get total number of items (i.e. sum of occurrences)
            int totalItems = sortedFreqDist.Sum(pair => pair.Value);

            // For each item
            foreach (var item in sortedFreqDist)
            {
                // Extract info
                var itemValue = item.Key.ToString();
                var count = item.Value;
                var percentage = ((double)count / totalItems) * 100;

                // Print statistics line
                PrintStatsLine(percentage, itemValue, count);
            }

            // Return frequency distribution
            return sortedFreqDist;
        }

        /// <summary>
        /// Generates a frequency distribution of sub-properties extracted from a list of audio tags.
        /// </summary>
        /// <param name="audioTags">The list of audio tags</param>
        /// <param name="func">A function that extracts a property from a given audio tag.</param>
        /// <returns>List of key-value pairs (property-frequency_count pairs), sorted in descending order by count.</returns>
        public static StringIntFreqDist getSortedFreqDist(List<TrackTag> audioTags, Func<TrackTag, string> func)
        {
            // A dictionary that maps each unique item to how many there are
            var itemVariants = new Dictionary<string, int>();

            // For each tag
            foreach (var tag in audioTags)
            {
                // Extract properties using the given function
                string[] properties = ProcessProperty(func(tag));

                // For each sub-property
                foreach (string subProperty in properties)
                {
                    // If in dictionary
                    if (itemVariants.ContainsKey(subProperty))
                    {
                        // Increment value
                        itemVariants[subProperty]++;
                    }
                    else
                    {
                        // Otherwise if not in dictionary, add it
                        // NOTE: REQUIRED to prevent 'KeyNotFoundException' errors
                        itemVariants[subProperty] = 1;
                    }
                }
            }

            // Sort the dictionary by count in descending order,
            // and return as an IOrderedEnumerable of KeyValuePairs
            return itemVariants.OrderByDescending(pair => pair.Value);
        }

        /// <summary>
        /// Splits a string of possibly concatenated values into an array.
        /// </summary>
        /// <param name="full">The full string, possibly concatenated with separators.</param>
        /// <returns>An array extracted from the input string.</returns>
        public static string[] ProcessProperty(string full)
        {
            char[] separators = { ';', ',' };

            // If doesn't contain any separators, return as is
            if (!separators.Any(full.Contains))
            {
                return new[] { full };
            }

            // Split string using first separator found
            char selectedSeparator = separators.First(s => full.Contains(s));
            string[] artistArr = full.Split(selectedSeparator);

            // Return array without whitespace in strings
            return artistArr.Select(a => a.Trim()).ToArray();
        }

        /// <summary>
        /// Print statistics on how many tracks are in each time/decade period
        /// </summary>
        /// <param name="yearFreqDist"></param>
        private void PrintDecadeStats(string statName, StringIntFreqDist yearFreqDist)
        {
            // Print heading
            PrintHeading(statName);

            // Print columns
            PrintColumns("%", statName, "Occurrences");

            // A dictionary that maps each period to how many tracks there are in it
            var decadeDict = new Dictionary<int, int>();

            // For each year pair
            foreach (var yearPair in yearFreqDist)
            {
                // Extract info from pair
                var yearNum = int.Parse(yearPair.Key.ToString());
                var count = yearPair.Value;

                // Calculate decade 
                int decade = (yearNum / 10) * 10;

                // If in dictionary
                if (decadeDict.ContainsKey(decade))
                {
                    // Increment value by count 
                    decadeDict[decade] += count;
                }
                else
                {
                    // Otherwise if not in dictionary, add it
                    // NOTE: REQUIRED to prevent 'KeyNotFoundException' errors
                    decadeDict[decade] = 1;
                }
            }

            // Sort decade dictionary
            var decadeFreqDist = decadeDict.OrderByDescending(pair => pair.Value);

            // Get total number of items (i.e. sum of occurrences)
            int totalItems = decadeFreqDist.Sum(pair => pair.Value);

            // For each decade pair
            foreach (var decadePair in decadeFreqDist)
            {
                // Extract info
                var decade = decadePair.Key.ToString() + "s";
                var count = decadePair.Value;
                var percentage = ((double)count / totalItems) * 100;

                // Print statistics line
                PrintStatsLine(percentage, decade, count, 0);
            }
        }

        /// <summary>
        /// Print statistics line
        /// </summary>
        /// <param name="percentage">Percentage</param>
        /// <param name="itemValue">The actual item/instance value</param>
        /// <param name="freq">Frequency </param>
        private void PrintStatsLine(double percentage, string itemValue, int freq, double cutoff = 0.25)
        {
            string freqS = freq.ToString();
            string percentS = percentage.ToString("F2") + "%";

            if (cutoff < percentage)
            {
                Console.WriteLine($"{percentS,-10} {itemValue,-40} {freqS}");
            }
        }

        /// <summary>
        /// Print columns for statistics lines
        /// </summary>
        /// <param name="c1"></param>
        /// <param name="c2"></param>
        /// <param name="c3"></param>
        private void PrintColumns(string c1, string c2, string c3)
        {
            Console.WriteLine($"{c1,-10} {c2,-40} {c3}");
        }

        /// <summary>
        /// Print a statistics category heading
        /// </summary>
        /// <param name="title"></param>
        private void PrintHeading(string title)
        {
            Console.WriteLine($"\n# {title} Statistics");
        }
    }
}
