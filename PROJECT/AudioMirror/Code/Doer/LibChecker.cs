﻿using AudioMirror.Code.Modules;
using System;
using System.Collections.Generic;
using System.Linq;

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

            // Check for unwanted strings
            CheckForUnwanted();

            // Print time taken
            Console.WriteLine("");
            PrintTimeTaken();
        }

        /// <summary>
        /// Check for unwanted strings in metadata
        /// </summary>
        private void CheckForUnwanted()
        {
            Console.WriteLine(" - Checking for unwanted strings...");

            // Unwanted strings
            var unwantedInfo = new[] { "feat.", "ft.", "edit", 
                "version", "original", "soundtrack" };

            int totalHits = 0;
            foreach (TrackTag tag in audioTags)
            {
                foreach (var curUnwanted in unwantedInfo)
                {
                    totalHits += CheckProperty(tag, t => t.Artists, "artists", curUnwanted);
                    totalHits += CheckProperty(tag, t => t.Title, "title", curUnwanted);
                    totalHits += CheckProperty(tag, t => t.Album, "album", curUnwanted);
                }
            }

            Console.WriteLine($"  - Total hits: {totalHits}");
        }

        /// <summary>
        /// Print a message if a given track property contains an unwanted substring
        /// </summary>
        /// <param name="tag">The track's TrackTag object</param>
        /// <param name="propExt">A delegate that extracts the property</param>
        /// <param name="propertyName">The name of the property being checked</param>
        /// <param name="unwanted">The unwanted part</param>
        /// <returns>One if unwanted part was found, zero otherwise</returns>
        private int CheckProperty(TrackTag tag, Func<TrackTag, string> propExt, 
            string propertyName, string unwanted)
        {
            // If an exception, skip
            if (isException(tag, unwanted))  { return 0; }

            // If property's value contains unwanted string, print message
            if (propExt(tag).ToLower().Contains(unwanted.ToLower()))
            {
                Console.WriteLine($"  - Found '{unwanted}' in {propertyName} of '{tag.ToString()}'");
                return 1;
            }

            return 0;
        }

        /// <returns>True if metadata combination is whitelisted, false otherwise</returns>
        private bool isException(TrackTag tag, string unwanted)
        {
            if (unwanted.Equals("original") &&
                (tag.Album.Equals("Original Rappers") || tag.Artists.Contains("KRS-One")))
            {
                return true;
            }

            if (unwanted.Equals("edit"))
            {
                if (tag.Title.Contains("Going To Be Alright") || tag.Title.Contains("Medicine Man"))
                {
                    return true;
                }

                if(tag.Album.Contains("Edition"))
                {
                    return true;
                }
            }

            if (unwanted.Equals("soundtrack") && tag.Title.Equals("Soundtrack 2 My Life"))
            {
                return true;
            }
            
            return false;
        }
    }
}
