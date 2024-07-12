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
        private readonly static string musivDir = "Musivation";
        private readonly static string[] unwantedInfo = { "feat.", "ft.", "edit", "version", "original", "soundtrack" };


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

            // Check all tags
            int totalTagHits = 0;
            Console.WriteLine(" - Checking all tags against filenames and unwanted strings...");
            foreach (TrackTag tag in audioTags)
            {
                // Check filename against tag
                totalTagHits += CheckFilename(tag);

                // Check for unwanted strings in tag
                totalTagHits += CheckForUnwanted(tag);
            }
            printTotalHits(totalTagHits);

            // Check Artists folder
            var artistsWithAudioFolder = CheckArtistFolder();

            // Check Miscellaneous Songs folder
            CheckMiscFolder(artistsWithAudioFolder);

            // Check Musivation folder
            CheckMusivationFolder();

            // Print time taken
            Console.WriteLine("");
            PrintTimeTaken();
        }

        /// <summary>
        /// Checks a given audio tag against it's track's filename
        /// </summary>
        /// <param name="tag">The audio tag to check</param>
        /// <returns>The number of issues found</returns>
        private int CheckFilename(TrackTag tag)
        {
            int totalHits = 0;

            // Get standardised filename
            string filenameS = standardiseStr(getFileName(tag));

            // Check filename contains all artists
            string[] artistsArr = tag.Artists.Split(';');
            foreach (string artist in artistsArr)
            {
                totalHits += CheckFilenameForStr(filenameS, artist);
            }

            // Check filename contains separator
            totalHits += CheckFilenameForStr(filenameS, " - ");

            // Check filename contains title
            totalHits += CheckFilenameForStr(filenameS, tag.Title);

            return totalHits;
        }

        /// <param name="filename">The track's filename</param>
        /// <param name="subStr">A given substring</param>
        /// <returns>1 if the filename didn't contain the substring, 0 otherwise</returns>
        private int CheckFilenameForStr(string filename, string subStr)
        {
            // Standardise inputs
            filename = standardiseStr(filename);
            subStr = standardiseStr(subStr);

            // If the filename doesn't contain substring, notify
            if (!filename.Contains(subStr))
            {
                Console.WriteLine($"  - '{filename}' should include '{subStr}'");
                return 1;
            }
            return 0;
        }

        /// <summary>
        /// Check for unwanted strings in the artists, title and album fields
        /// </summary>
        /// <param name="tag">The audio tag to check</param>
        /// <returns>The number of issues found</returns>
        private int CheckForUnwanted(TrackTag tag)
        {
            int totalHits = 0;

            // For every unwanted string
            foreach (var curUnwanted in unwantedInfo)
            {
                // Check fields for unwanted substrings
                totalHits += CheckProperty(tag, t => t.Artists, "artists", curUnwanted);
                totalHits += CheckProperty(tag, t => t.Title, "title", curUnwanted);
                totalHits += CheckProperty(tag, t => t.Album, "album", curUnwanted);
            }

            return totalHits;
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
            if (isException(tag, unwanted)) { return 0; }

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
            Console.WriteLine($" - Checking {artistsDir} folder...");

            // Filter audio tags down to Artist Songs folder only
            var artistAudioTags = filterTagsByMainFolder(artistsDir);

            // For all tags in the Artists folder
            int totalHits = 0;
            foreach (TrackTag tag in artistAudioTags)
            {
                // Extract folder name from relative path
                string artistFolderName = getRelPathPart(tag, 2);

                // Extract primary artist
                string primaryArtist = tag.PrimaryArtist;

                // If primary artist has full stop at the end OR has a quote character
                if (primaryArtist.LastOrDefault().Equals('.') || primaryArtist.Contains("\""))
                {
                    // Skip because Windows doesn't allow folders with these properties
                    continue;
                }

                // If primary artist doesn't match artist folder name, notify
                if (!primaryArtist.Equals(artistFolderName))
                {
                    string primArtPart = $"  - A song by '{primaryArtist}'";
                    string foldPart = $" is in the '{artistFolderName}' folder!";
                    Console.WriteLine(primArtPart + foldPart);
                    totalHits++;
                }
            }

            printTotalHits(totalHits);

            // Get list of artists with an audio folder (without duplicates) and return
            var artistsWithAudioFolder = artistAudioTags.Select(tag => tag.PrimaryArtist).Distinct().ToList();
            return artistsWithAudioFolder;
        }

        /// <summary>
        /// Do various checks on the Miscellaneous folder
        /// </summary>
        private void CheckMiscFolder(List<String> artistsWithAudioFolder)
        {
            Console.WriteLine($" - Checking {miscDir} folder...");

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

            printTotalHits(totalHits);
        }

        /// <summary>
        /// Do various checks on the Musivation folder
        /// </summary>
        private void CheckMusivationFolder()
        {
            Console.WriteLine($" - Checking {musivDir} folder...");

            // Filter audio tags down to Musivation folder only
            var musivAudioTags = filterTagsByMainFolder(musivDir);

            // For each Musivation track
            int totalHits = 0;
            foreach (TrackTag tag in musivAudioTags)
            {
                // If it doesn't have the Musivation genre, notify
                if(!tag.Genres.Contains("Musivation"))
                {
                    Console.WriteLine($"  - {tag.ToString()} does not have the Musivation genre!");
                }
            }

            printTotalHits(totalHits);
        }

        /// <summary>
        /// Print message displaying the total hits, if there were any
        /// </summary>
        private void printTotalHits(int totalHits)
        {
            if (totalHits != 0)
            {
                Console.WriteLine($"  - Total hits: {totalHits}");
            }
        }

        /// <returns>The given string sanitised and trimmed, with special chars removed</returns>
        private string standardiseStr(string s)
        {
            return Reflector.SanitiseFilename(s).Replace("_", "").Trim();
        }

        /// <param name="mainFolderName">The name of the folder within the Audio folder</param>
        /// <returns>A list of the audio tags for the tracks in that folder only</returns>
        private List<TrackTag> filterTagsByMainFolder(string mainFolderName)
        {
            return audioTags.Where(tag => getRelPathPart(tag, 1) == mainFolderName).ToList();
        }

        /// <param name="tag">The audio tag</param>
        /// <returns>The track's filename, santised</returns>
        private string getFileName(TrackTag tag)
        {
            string[] pathParts = getPathParts(tag);
            return pathParts[pathParts.Length - 1];
        }

        /// <param name="tag">The audio tag</param>
        /// <param name="pos">The index of the desired path part</param>
        /// <returns>The desired relative path part</returns>
        private string getRelPathPart(TrackTag tag, int pos)
        {
            return getPathParts(tag)[pos];
        }

        /// <param name="tag">The audio tag</param>
        /// <returns>The parts of the track's relative path</returns>
        private string[] getPathParts(TrackTag tag)
        {
            return tag.RelPath.Split('\\');
        }
    }
}         

