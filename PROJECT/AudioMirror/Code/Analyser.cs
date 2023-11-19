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
            PrintFreqStats("Artists", tag => tag.Artists);

            // Print genre stats
            //PrintFreqStats("Genre", tag => tag.Genre);
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

            // The unique items
            var itemVariants = new HashSet<T>();

            // All the items
            var itemsList = new List<T>();

            // For each tag
            foreach (var tag in audioTags)
            {
                // Extract specified property
                var property = func(tag);

                // Add to collections
                if (property is string)
                {
                    itemVariants.Add(property);
                    itemsList.Add(property);
                }
            }

            //// Get frequency value pairs
            // Count the occurrences of each item variant in the itemsList
            var itemVariantCounts = itemVariants.Select(itemVariant => new
                {
                    ItemVariant = itemVariant,
                    Count = itemsList.Count(item => EqualityComparer<T>.Default.Equals(item, itemVariant))
                });

            // Create key-value pairs with item variant and its count
            var frequencyValuePairs = itemVariantCounts
                .Select(itemCount => new KeyValuePair<int, T>(itemCount.Count, itemCount.ItemVariant));

            // Sort the pairs in descending order based on the count
            var sortedPairs = frequencyValuePairs.OrderByDescending(pair => pair.Key);

            // Convert the sorted pairs to a list
            var fvPairs = sortedPairs.ToList();

            // Print columns
            PrintColumns("%", statName, "Occurrences");

            // For each pair
            foreach (var curPair in fvPairs)
            {
                // Extract info
                var itemValue = curPair.Value.ToString();
                var count = curPair.Key;
                var percentage = ((double) count / audioTags.Count) * 100;

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
            if (0 < percentage)
            {
                Console.WriteLine($"{percentS,-10} {itemValue,-40} {freqS}");
            }
        }
    }
}
