using System;

namespace AudioManager
{
    /// <summary>
    /// Performs some action and records how long it took.
    /// </summary>
    internal class Doer
    {
        /// <summary>
        /// The start time of the action.
        /// </summary>
        private DateTime startTime;

        /// <summary>
        /// The time taken for the action to execute.
        /// </summary>
        private TimeSpan executionTime;

        /// <summary>
        /// The total time taken by all Doers during this program's runtime.
        /// </summary>
        private static TimeSpan totalTime;

        /// <summary>
        /// Initialises a new instance of the <see cref="Doer"/> class.
        /// </summary>
        protected Doer()
        {
            startTime = DateTime.Now;
        }

        /// <summary>
        /// Finish execution: Calculate execution time and add it to the running total.
        /// NB: ALL DOERS MUST CALL THIS (directly or indirectly) when they finish.
        /// </summary>
        protected void Finished()
        {
            executionTime = DateTime.Now - startTime;
            totalTime += executionTime;
        }

        /// <summary>
        /// Print time taken for this Doer to execute.
        /// </summary>
        protected void PrintTimeTaken()
        {
            Console.WriteLine(" - Time taken: " + ConvertTimeSpanToString(executionTime));
        }

        /// <summary>
        /// Finishes execution and prints the time taken.
        /// </summary>
        protected void FinishAndPrintTimeTaken()
        {
            Finished();
            PrintTimeTaken();
        }

        /// <summary>
        /// Print the total time taken by all Doers during this program's runtime.
        /// </summary>
        public static void PrintTotalTimeTaken()
        {
            Console.WriteLine("\nTotal time taken: " + ConvertTimeSpanToString(totalTime));
        }

        /// <summary>
        /// Formats a TimeSpan into a string.
        /// </summary>
        public static string ConvertTimeSpanToString(TimeSpan timeSpan)
        {
            return ($"{Math.Round(timeSpan.TotalSeconds, 3)} seconds");
        }
    }
}