using AudioMirror.Code.Modules;
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
        // Constants
        private readonly static string miscDir = "Miscellaneous Songs";
        private readonly static string inMiscMsg = $" in the {miscDir} folder!";
        private readonly static string artistsDir = "Artists";
        //private readonly static string musivDir = "Musivation";

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

            // Check for unwanted strings in metadata
            CheckForUnwanted();

            // Check Artists folder
            var artistsWithAudioFolder = CheckArtistFolder();

            // Check Miscellaneous Songs folder
            CheckMiscFolder(artistsWithAudioFolder);

            // Print time taken
            Console.WriteLine("");
            PrintTimeTaken();
        }

        /// <summary>
        /// Check for unwanted strings in the artists, title and album fields
        /// </summary>
        private void CheckForUnwanted()
        {
            Console.WriteLine(" - Checking for unwanted strings...");

            // Unwanted strings
            var unwantedInfo = new[] { "feat.", "ft.", "edit", 
                "version", "original", "soundtrack" };

            // For every tag
            int totalHits = 0;
            foreach (TrackTag tag in audioTags)
            {
                // For every unwanted string
                foreach (var curUnwanted in unwantedInfo)
                {
                    // Check fields for unwanted substrings
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

        /// <summary>
        /// Do various checks on the Artist folder
        /// </summary>
        private List<String> CheckArtistFolder()
        {
            Console.WriteLine($" - Checking {artistsDir}...");

            // Filter audio tags down to Artist Songs folder only
            var artistAudioTags = filterTagsByMainFolder(artistsDir);

            // Get list of artists with an audio folder (without duplicates)
            var artistsWithAudioFolder = artistAudioTags.Select(tag => tag.PrimaryArtist).Distinct().ToList();

            // TODO

            // Return artist list
            return artistsWithAudioFolder;
        }

        /// <summary>
        /// Do various checks on the Miscellaneous folder
        /// </summary>
        private void CheckMiscFolder(List<String> artistsWithAudioFolder)
        {
            Console.WriteLine($" - Checking {miscDir}...");

            // Filter audio tags down to Miscellaneous Songs folder only
            var miscAudioTags = filterTagsByMainFolder(miscDir);

            // Generate primary artist frequency distribution of the Misc tags
            var sortedMiscArtistFreq = Analyser.getSortedFreqDist(miscAudioTags, t => t.PrimaryArtist);

            // For each artist-frequency pair in the Misc folder
            int totalHits = 0;
            foreach (var miscArtistPair in sortedMiscArtistFreq)
            {
                // Extract artist name
                string curMiscArtist = miscArtistPair.Key;

                // Extract number of songs by that artist in the Misc folder
                int curMiscArtistCount = miscArtistPair.Value;

                // If trio (or more) of songs detected
                if (curMiscArtistCount >= 3)
                {
                    string trioMsg = $"  - There are {curMiscArtistCount} songs by '{curMiscArtist}'";
                    Console.WriteLine(trioMsg + inMiscMsg);
                    totalHits++;
                }

                // If song with an Artists folder is found in the Misc folder
                if (artistsWithAudioFolder.Contains(curMiscArtist))
                {
                    string artistMsg = $"  - '{curMiscArtist}' has an {artistsDir} folder but has a song";
                    Console.WriteLine(artistMsg + inMiscMsg);
                    totalHits++;
                }
            }

            Console.WriteLine($"  - Total hits: {totalHits}");
        }

        /// <param name="mainFolderName">The name of the folder within the Audio folder</param>
        /// <returns>A list of the audio tags for the tracks in that folder only</returns>
        private List<TrackTag> filterTagsByMainFolder(string mainFolderName)
        {
            return audioTags.Where(tag => tag.RelPath.Split('\\')[1] == mainFolderName).ToList();
        }
    }
}         

