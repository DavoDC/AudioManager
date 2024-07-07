using AudioMirror.Code.Modules;
using System;
using System.Collections.Generic;

namespace AudioMirror
{
    /// <summary>
    /// Audio library organisational/metadata checks
    /// </summary>
    internal class LibChecker : Doer
    {
        // Variables
        private List<TrackTag> audioTags;

        /// <summary>
        /// Construct a library checker
        /// </summary>
        /// <param name="audioTags"></param>
        public LibChecker(List<TrackTag> audioTags)
        {
            // Notify
            Console.WriteLine("\nChecking library...");

            // Save parameter
            this.audioTags = audioTags;

            // TODO

            // Print time taken
            Console.WriteLine("");
            PrintTimeTaken();
        }
    }
}
