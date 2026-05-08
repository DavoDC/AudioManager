using System;

namespace AudioManager.Code.Modules
{
    /// <summary>
    /// Represents an instance of statistical information
    /// </summary>
    internal class Statistic
    {
        // The property's value
        private string property;

        // The property's frequency count
        private int count;

        // The property's frequency percentage
        private double percentage;

        /// <summary>
        /// Create a Statistic object
        /// </summary>
        /// <param name="property">The property's value</param>
        /// <param name="count">The property's frequency count</param>
        /// <param name="totalItems">The total amount of this property (i.e. sum of occurrences)</param>
        public Statistic(string property, int count, int totalItems) 
        {
            this.property = property;
            this.count = count;
            percentage = ((double)count / totalItems) * 100;
        }

        /// <summary>
        /// Print line representing this statistics object
        /// </summary>
        public void Print(int pos, double cutoff = 0.25)
        {
            // If percentage is greater than cutoff
            if (cutoff < percentage)
            {
                // Format percentage
                string percentageS = percentage.ToString("F2") + "%";

                // Print out info in columns (markdown table row)
                PrintColumns(pos.ToString(), percentageS, property, count.ToString(), isHeader: false);
            }
        }

        /// <summary>
        /// Print out four strings in markdown table row format
        /// </summary>
        /// <param name="c1">First column</param>
        /// <param name="c2">Second column</param>
        /// <param name="c3">Third column</param>
        /// <param name="c4">Fourth column</param>
        /// <param name="isHeader">If true, print header row and separator; if false, print data row</param>
        public static void PrintColumns(string c1, string c2, string c3, string c4, bool isHeader = false)
        {
            if (isHeader)
            {
                // Print markdown table header row
                Console.WriteLine($"| {c1} | {c2} | {c3} | {c4} |");
                // Print separator row with reasonable column widths
                Console.WriteLine("|---|---|---------|----------|");
            }
            else
            {
                // Print markdown table data row
                Console.WriteLine($"| {c1} | {c2} | {c3} | {c4} |");
            }
        }
    }
}
