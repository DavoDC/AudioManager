using AudioMirror.Code.Modules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AudioMirror
{
    internal class Analyser
    {
        // Variables
        private List<TrackTag> audioTags;

        /// <summary>
        /// Construct an audio tag analyser
        /// </summary>
        /// <param name="audioTags"></param>
        public Analyser(List<TrackTag> audioTags)
        {
            // Save parameter
            this.audioTags = audioTags;

            // Notify
            Console.WriteLine("\nAnalysing tags...");

            // Print artist stats
            PrintFreqStats("Artists", tag => GetArtistArr(tag.Artists));

            // Print genre stats
            //PrintFreqStats("Genre", tag => tag.Genre);
        }


        /// <summary>
        /// Splits a string of possibly concatenated artists into an array.
        /// </summary>
        /// <param name="rawArtists">The full artist string, possibly concatenated with separators.</param>
        /// <returns>An array of artists extracted from the input string.</returns>
        private string[] GetArtistArr(string rawArtists)
        {
            char[] separators = { ',', ';' };

            // If doesn't contain any separators, return as is
            if (!separators.Any(rawArtists.Contains))
            {
                return new[] { rawArtists };
            }

            // Split string using first separator found
            char selectedSeparator = separators.First(s => rawArtists.Contains(s));
            string[] artistArr = rawArtists.Split(selectedSeparator);

            // Return array without whitespace in strings
            return artistArr.Select(a => a.Trim()).ToArray();
        }


        /// <summary>
        /// Print frequency statistics
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="statName"></param>
        /// <param name="func"></param>
        private void PrintFreqStats<T>(string statName, Func<TrackTag, T> func)
        {
            // Heading
            Console.WriteLine($"\n# {statName} Statistics");

            // Maps each unique item to how many there are
            var itemVariants = new Dictionary<T, int>();

            // For each tag
            foreach (var tag in audioTags)
            {
                // Extract specified property
                var property = func(tag);

                if (property != null)
                {
                    // For each sub property
                    //foreach (var property in properties)
                    {
                        // If in dictionary
                        if (itemVariants.ContainsKey(property))
                        {
                            // Increment
                            itemVariants[property]++;
                        }
                        else
                        {
                            // Otherwise add to dictionary
                            itemVariants[property] = 1;
                        }
                    }
                }
            }

            // Sort the dictionary by count in descending order
            var sortedItems = itemVariants.OrderByDescending(pair => pair.Value);

            // Print columns
            PrintColumns("%", statName, "Occurrences");

            // Calculate total tags
            int totalTags = audioTags.Count;

            // For each item
            foreach (var item in sortedItems)
            {
                // Extract info
                var itemValue = item.Key.ToString();

                var count = item.Value;
                var percentage = ((double) count / totalTags) * 100;

                // Print statistics line
                PrintStatsLine(percentage, itemValue, count);
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
        /// Print statistics line
        /// </summary>
        /// <param name="percentage">Percentage</param>
        /// <param name="itemValue">The actual item/instance value</param>
        /// <param name="freq">Frequency </param>
        private void PrintStatsLine(double percentage, string itemValue, int freq)
        {
            string freqS = freq.ToString();
            string percentS = percentage.ToString("F2") + "%";

            // If percentage exceeds cutoff
            if (0.03 < percentage &&  0.05 > percentage)
            {
                Console.WriteLine($"{percentS,-10} {itemValue,-40} {freqS}");
            }
        }
    }
}
