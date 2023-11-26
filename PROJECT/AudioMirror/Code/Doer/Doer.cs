using System;

namespace AudioMirror
{
    /// <summary>
    /// Represents a base class for performing some action with timing information.
    /// </summary>
    internal class Doer
    {
        /// <summary>
        /// The start time of the action.
        /// </summary>
        protected DateTime startTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="Doer"/> class.
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
            var executionTime = DateTime.Now - startTime;
            Console.WriteLine($" - Time taken: {Math.Round(executionTime.TotalSeconds, 3)} seconds");
        }
    }
}
