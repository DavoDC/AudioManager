﻿using AudioMirror.Code.Modules;
using System;
using System.Collections.Generic;
using System.Linq;

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
            PrintFreqStats("Year", tag => tag.Year);

            // Print time taken
            Console.WriteLine("");
            PrintTimeTaken();
        }


        /// <summary>
        /// Print frequency statistics for a given track property
        /// </summary>
        /// <param name="statName">The name of the property</param>
        /// <param name="func">Function that returns the property</param>
        private void PrintFreqStats(string statName, Func<TrackTag, string> func)
        {
            // Heading
            Console.WriteLine($"\n# {statName} Statistics");

            // Get sorted frequency distribution
            var sortedFreqDist = getSortedFreqDist(audioTags, func);

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
        }

        /// <summary>
        /// Generates a frequency distribution of sub-properties extracted from a list of audio tags.
        /// </summary>
        /// <param name="audioTags">The list of audio tags</param>
        /// <param name="func">A function that extracts a property from a given audio tag.</param>
        /// <returns>List of key-value pairs (property-frequency_count pairs), sorted in descending order by count.</returns>
        public static IOrderedEnumerable<KeyValuePair<string, int>> getSortedFreqDist
            (List<TrackTag> audioTags, Func<TrackTag, string> func)
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
            char[] separators = { ',', ';' };

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
            if (0.25 < percentage)
            {
                Console.WriteLine($"{percentS,-10} {itemValue,-40} {freqS}");
            }
        }
    }
}
