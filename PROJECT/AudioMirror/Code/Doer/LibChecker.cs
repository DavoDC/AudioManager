using AudioMirror.Code.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

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
            Console.WriteLine(" - Checking all tags against filenames and for unwanted/missing info...");
            foreach (TrackTag tag in audioTags)
            {
                // Check filename against tag
                totalTagHits += CheckFilename(tag);

                // Check for unwanted strings in tag
                totalTagHits += CheckForUnwanted(tag);

                // Check for missing properties
                totalTagHits += CheckForMissing(tag);

                // Check album cover counts
                totalTagHits += CheckAlbumCoverCount(tag);

                // Check compilation status
                if(!bool.Parse(tag.Compilation))
                {
                    Console.WriteLine($"  - '{tag.RelPath}' is not set as a compilation!");
                    totalTagHits++;
                }
            }
            PrintTotalHits(totalTagHits);

            // Check Artists folder
            List<string> artistsWithAudioFolder = CheckArtistFolder();

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
            string filenameS = StandardiseStr(GetFileName(tag));

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
            filename = StandardiseStr(filename);
            subStr = StandardiseStr(subStr);

            // If the expected value is "Missing" (null), then skip this check.
            if(subStr.Equals("Missing"))
            {
                return 0;
            }

            // If the filename doesn't contain expected substring, notify
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
        /// Check for missing info in the artists, title, album and year fields
        /// </summary>
        /// <param name="tag">The audio tag to check</param>
        /// <returns>The number of issues found</returns>
        private int CheckForMissing(TrackTag tag)
        {
            int totalHits = CheckFieldMissing(tag.Title, "Title", tag.RelPath);
            totalHits += CheckFieldMissing(tag.Artists, "Artists", tag.RelPath);
            totalHits += CheckFieldMissing(tag.Album, "Album", tag.RelPath);
            totalHits += CheckFieldMissing(tag.Year, "Year", tag.RelPath);
            return totalHits;
        }

        /// <summary>
        /// Checks if a specific field value is "Missing" and prints a message if so.
        /// </summary>
        /// <param name="fieldValue">The value of the field to check.</param>
        /// <param name="fieldName">The name of the field being checked (e.g., "Title").</param>
        /// <param name="filePath">The relative file path of the audio file.</param>
        /// <returns>1 if the field was missing, 0 otherwise.</returns>
        private int CheckFieldMissing(string fieldValue, string fieldName, string filePath)
        {
            if (fieldValue.Equals("Missing"))
            {
                Console.WriteLine($"  - '{filePath}' has no {fieldName} set!");
                return 1;
            }
            return 0;
        }

        /// <summary>
        /// Checks the number of album covers in a track tag and prints warnings for unusual counts.
        /// </summary>
        /// <param name="tag">The track tag.</param>
        /// <returns>1 if the album cover count is invalid (none or more than one); otherwise, 0.</returns>
        private int CheckAlbumCoverCount(TrackTag tag)
        {
            try
            {
                int albumCoverNum = int.Parse(tag.AlbumCoverCount);
                if (albumCoverNum == 0)
                {
                    Console.WriteLine($"  - '{tag.RelPath}' has no album cover!");
                    return 1;
                }
                else if (albumCoverNum != 1)
                {
                    Console.WriteLine($"  - '{tag.RelPath}' has {albumCoverNum} album covers!");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                HandleLibCheckerException(ex, tag);
            }

            return 0;
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
            try
            {
                // If an exception, skip
                if (IsException(tag, unwanted)) { return 0; }

                // If property's value contains unwanted string, print message
                if (propExt(tag).ToLower().Contains(unwanted.ToLower()))
                {
                    Console.WriteLine($"  - Found '{unwanted}' in {propertyName} of '{tag}'");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                HandleLibCheckerException(ex, tag);
            }

            return 0;
        }

        /// <returns>True if metadata combination is whitelisted, false otherwise</returns>
        private bool IsException(TrackTag tag, string unwanted)
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

            if(tag.Artists.Contains("Agatha All Along"))
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
            var artistAudioTags = FilterTagsByMainFolder(artistsDir);

            // For all tags in the Artists folder
            int totalHits = 0;
            foreach (TrackTag tag in artistAudioTags)
            {
                // Extract folder name from relative path
                string artistFolderName = GetRelPathPart(tag, 2);

                // Extract primary artist
                string primaryArtist = tag.PrimaryArtist;

                // Warning message
                string artistMsg = $"  - A song by '{primaryArtist}'";
                artistMsg += $" is in the '{artistFolderName}' folder!";

                // If primary artist doesn't match artist folder name, notify
                if (!primaryArtist.Equals(artistFolderName))
                {
                    // If primary artist has full stop at the end OR has a quote character
                    if (primaryArtist.LastOrDefault().Equals('.') || primaryArtist.Contains("\""))
                    {
                        // Skip because Windows doesn't allow folders with these properties
                        continue;
                    }

                    Console.WriteLine(artistMsg + " (mismatch)");
                    totalHits++;
                }

                // If direct parent folder matches artist folder name, notify
                if(GetDirectParentFolder(tag).Equals(artistFolderName))
                {
                    // If this is an album folder that matches the artist name, skip
                    if(tag.Album.Equals(tag.PrimaryArtist))
                    {
                        continue;
                    }

                    Console.WriteLine(artistMsg + " (loose/directly)");
                    totalHits++;
                }
            }

            PrintTotalHits(totalHits);

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
            var miscAudioTags = FilterTagsByMainFolder(miscDir);

            // Generate primary artist frequency distribution of the Misc tags
            var sortedMiscArtistFreq = StatList.GetSortedFreqDist(miscAudioTags, t => t.PrimaryArtist);

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

            PrintTotalHits(totalHits);
        }

        /// <summary>
        /// Do various checks on the Musivation folder
        /// </summary>
        private void CheckMusivationFolder()
        {
            Console.WriteLine($" - Checking {musivDir} folder...");

            // Filter audio tags down to Musivation folder only
            var musivAudioTags = FilterTagsByMainFolder(musivDir);

            // For each Musivation track
            int totalHits = 0;
            foreach (TrackTag tag in musivAudioTags)
            {
                // If it doesn't have the Musivation genre, notify
                if(!tag.Genres.Contains("Musivation"))
                {
                    Console.WriteLine($"  - {tag.ToString()} does not have the Musivation genre!");
                    totalHits++;
                }
            }

            PrintTotalHits(totalHits);
        }

        /// <summary>
        /// Handles exceptions occurring in library checker methods: prints info and exits.
        /// </summary>
        /// <param name="ex">The exception that was thrown.</param>
        /// <param name="tag">The track tag associated with the exception.</param>
        /// <param name="methodName">The name of the calling method (auto-filled).</param>
        private void HandleLibCheckerException(Exception ex, TrackTag tag, [CallerMemberName] string methodName = "")
        {
            Console.WriteLine($"\nException occurred in {methodName}(): {ex.Message}");

            if (tag != null)
            {
                Console.WriteLine("\nTag details: " + tag.ToString());
                tag.PrintAllProperties();
            }
            else
            {
                Console.WriteLine("\nTag details are not available (null).");
            }

            Environment.Exit(1);
        }

        /// <summary>
        /// Print message displaying the total hits, if there were any
        /// </summary>
        private void PrintTotalHits(int totalHits)
        {
            if (totalHits != 0)
            {
                Console.WriteLine($"  - Total hits: {totalHits}");
            }
        }

        /// <returns>The given string sanitised and trimmed, with special chars removed</returns>
        private string StandardiseStr(string s)
        {
            return Reflector.SanitiseFilename(s).Replace("_", "").Trim();
        }

        /// <param name="mainFolderName">The name of the folder within the Audio folder</param>
        /// <returns>A list of the audio tags for the tracks in that folder only</returns>
        private List<TrackTag> FilterTagsByMainFolder(string mainFolderName)
        {
            return audioTags.Where(tag => GetRelPathPart(tag, 1) == mainFolderName).ToList();
        }

        /// <param name="tag">The audio tag</param>
        /// <returns>The track's filename</returns>
        private string GetFileName(TrackTag tag)
        {
            string[] pathParts = GetPathParts(tag);
            return pathParts[pathParts.Length - 1];
        }

        /// <param name="tag">The audio tag</param>
        /// <returns>The track's direct parent folder</returns>
        private string GetDirectParentFolder(TrackTag tag)
        {
            string[] pathParts = GetPathParts(tag);
            return pathParts[pathParts.Length - 2];
        }

        /// <param name="tag">The audio tag</param>
        /// <param name="pos">The index of the desired path part</param>
        /// <returns>The desired relative path part</returns>
        private string GetRelPathPart(TrackTag tag, int pos)
        {
            return GetPathParts(tag)[pos];
        }

        /// <param name="tag">The audio tag</param>
        /// <returns>The parts of the track's relative path</returns>
        private string[] GetPathParts(TrackTag tag)
        {
            return tag.RelPath.Split('\\');
        }
    }
}         

