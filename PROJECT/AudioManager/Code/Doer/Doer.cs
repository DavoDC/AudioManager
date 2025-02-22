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
        protected DateTime startTime;

        /// <summary>
        /// The execution time of the action
        /// </summary>
        private TimeSpan executionTime;
        public TimeSpan ExecutionTime
        {
            get => executionTime;
        }


        /// <summary>
        /// Initialises a new instance of the <see cref="Doer"/> class.
        /// </summary>
        protected Doer()
        {
            startTime = DateTime.Now;
        }

        /// <summary>
        /// Prints the time taken for the action.
        /// </summary>
        protected void PrintTimeTaken()
        {
            executionTime = DateTime.Now - startTime;
            Console.WriteLine(" - Time taken: " + ConvertTimeSpanToString(executionTime));
        }

        /// <summary>
        /// Formats a TimeSpan into a string
        /// </summary>
        public static string ConvertTimeSpanToString(TimeSpan timeSpan)
        {
            return ($"{Math.Round(timeSpan.TotalSeconds, 3)} seconds");
        }
    }
}
