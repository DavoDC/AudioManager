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

                // Print out info in columns
                PrintColumns(pos.ToString(), percentageS, property, count.ToString());
            }
        }

        /// <summary>
        /// Print out three strings in spaced out columns
        /// </summary>
        /// <param name="c1"></param>
        /// <param name="c2"></param>
        /// <param name="c3"></param>
        public static void PrintColumns(string c1, string c2, string c3, string c4)
        {
            Console.WriteLine($"{c1,-5} {c2,-10} {c3, -35} {c4}");
        }
    }
}
