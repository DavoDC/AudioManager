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
        /// Finish execution and calculate execution time
        /// NB: ALL DOERS MUST CALL THIS (directly or indirectly)
        /// </summary>
        protected void Finished()
        {
            executionTime = DateTime.Now - startTime;
        }

        /// <summary>
        /// Finishes execution and prints the time taken.
        /// </summary>
        protected void FinishAndPrintTimeTaken()
        {
            Finished();
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