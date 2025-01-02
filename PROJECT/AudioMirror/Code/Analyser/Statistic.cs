using System;

namespace AudioMirror.Code.Modules
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
        public void Print(double cutoff = 0.25)
        {
            // Convert count and percentage to strings
            string countS = count.ToString();
            string percentageS = percentage.ToString("F2") + "%";

            // If percentage is greater than cutoff, print out formatted info
            if (cutoff < percentage)
            {
                Console.WriteLine($"{percentageS,-10} {property,-40} {countS}");
            }
        }
    }
}
