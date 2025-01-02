﻿using System;
using System.Collections.Generic;
using System.Linq;
using StringIntFreqDist = System.Linq.IOrderedEnumerable<System.Collections.Generic.KeyValuePair<string, int>>;

namespace AudioMirror.Code.Modules
{
    /// <summary>
    /// Calculates, stores and displays frequency statistics for a given track property
    /// </summary>
    internal class StatList
    {
        // Name of these statistics
        private string name;

        // Underlying data structure - a list of statistics objects
        private List<Statistic> statList;

        /// <summary>
        /// Create a StatList object from audio tags and a property extractor
        /// </summary>
        /// <param name="name">The name of the statistics category</param>
        /// <param name="audioTagsIn">The list of audio tags inputted</param>
        /// <param name="func">Function that returns the property</param>
        public StatList(string name, List<TrackTag> audioTagsIn, Func<TrackTag, string> func)
        {
            // Save name
            this.name = name;

            // Calculate sorted frequency distribution
            StringIntFreqDist sortedFreqDist = GetSortedFreqDist(audioTagsIn, func);

            // Sum up total number of items (i.e. sum of occurrences)
            int totalItems = sortedFreqDist.Sum(pair => pair.Value);

            // Convert each frequency pair to a Statistic and save to list
            statList = new List<Statistic>();
            foreach (var freqPair in sortedFreqDist)
            {
                statList.Add(new Statistic(freqPair.Key, freqPair.Value, totalItems));
            }
        }

        /// <summary>
        /// Generates a frequency distribution of sub-properties extracted from a list of audio tags.
        /// </summary>
        /// <param name="audioTagsIn">The list of audio tags inputted</param>
        /// <param name="func">A function that extracts a property from a given audio tag.</param>
        /// <returns>List of key-value pairs (property-frequency_count pairs), sorted in descending order by count.</returns>
        public static StringIntFreqDist GetSortedFreqDist(List<TrackTag> audioTagsIn, Func<TrackTag, string> func)
        {
            // A dictionary that maps each unique item to how many there are
            var itemVariants = new Dictionary<string, int>();

            // For each tag
            foreach (var tag in audioTagsIn)
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
        /// Print out this statistics list
        /// </summary>
        /// <param name="cutoff">Percentage cutoff for statistics</param>
        public void Print(double cutoff = 0.25, string comment = "")
        {
            // Print heading and columns
            Console.WriteLine($"\n# {name} Statistics {comment}");
            Statistic.PrintColumns("#", "%", name, "Occurrences");

            // Print out every statistics object
            for (int i = 0; i < statList.Count; i++)
            {
                statList[i].Print(i + 1, cutoff);
            }
        }
    }
}