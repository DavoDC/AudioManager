using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AudioManager
{
    /// <summary>
    /// Creates a lightweight 'mirror' of an Audio track folder.
    /// </summary>
    internal class Reflector : Doer
    {
        // Mirror path variable
        string mirrorPath;

        /// <summary>
        /// Construct an audio mirror
        /// </summary>
        /// <param name="mirrorPath">The audio mirror folder path</param>
        public Reflector(string mirrorPath)
        {
            // Save parameters
            this.mirrorPath = mirrorPath;

            // Notify
            Console.WriteLine($"\nCreating mirror of '{Constants.AudioFolderPath}'...");

            // Setup folder structure
            CreateFolders();

            // Populate with files
            var statisticsInfo = CreateFiles();

            // Print statistics
            PrintStats(statisticsInfo);
        }


        /// <summary>
        /// Set up folder structure mirroring the actual folder
        /// </summary>
        public void CreateFolders()
        {
            // If mirror path is outside of repo
            if (!mirrorPath.Contains("C:\\Users\\David\\GitHubRepos\\AudioMirror"))
            {
                // Throw exception and notify
                string msg = $"\nMirror path was incorrect, outside the repo:\n{mirrorPath}\n";
                throw new ArgumentException(msg);
            }

            // If recreation wanted
            if (AgeChecker.RegenMirror)
            {
                // Remove the mirror path if it exists, to recreate it
                if (Directory.Exists(mirrorPath))
                {
                    Directory.Delete(mirrorPath, true);
                }
            }

            // For every actual folder
            string[] realDirs = Directory.GetDirectories(Constants.AudioFolderPath, "*", SearchOption.AllDirectories);
            foreach (var directoryPath in realDirs)
            {
                // Get real relative path
                string relativePath = GetRelativePath(Constants.AudioFolderPath, directoryPath);

                // Create relative folder within mirror path
                string newDirectoryPath = Path.Combine(mirrorPath, relativePath);
                Directory.CreateDirectory(newDirectoryPath);
            }
        }


        /// <summary>
        /// Populate folder structure with mirrored files
        /// </summary>
        /// <returns>Statistics tuple</returns>
        private Tuple<int, int, int, List<string>> CreateFiles()
        {
            // Holders
            int mp3FileCount = 0;
            int sanitisationCount = 0;
            int refreshedCount = 0;
            List<string> nonMP3Files = new List<string>();

            // For every actual file
            string[] realFiles = Directory.GetFiles(Constants.AudioFolderPath, "*", SearchOption.AllDirectories);
            foreach (var realFilePath in realFiles)
            {
                // Get relative file path
                string relativePath = GetRelativePath(Constants.AudioFolderPath, realFilePath);

                // For non-MP3 files, add to list and skip
                if (Path.GetExtension(realFilePath)?.ToLower() != ".mp3")
                {
                    nonMP3Files.Add(relativePath);
                    continue;
                }

                // Otherwise, process MP3 file
                if (CreateFile(realFilePath, relativePath, out bool refreshed))
                    sanitisationCount++;
                if (refreshed)
                    refreshedCount++;

                mp3FileCount++;
            }

            // Return holders
            return Tuple.Create(mp3FileCount, sanitisationCount, refreshedCount, nonMP3Files);
        }


        /// <summary>
        /// Create a mirrored file (the file contents are just the real path at this stage).
        /// In incremental mode, also refreshes the XML when the MP3 has been modified since last analysis.
        /// </summary>
        /// <param name="realFilePath">The actual MP3 file path</param>
        /// <param name="relativePath">The relative file path</param>
        /// <param name="refreshed">Output: true if an existing XML was overwritten because the MP3 was newer</param>
        /// <returns>True if the filename was sanitised</returns>
        private bool CreateFile(string realFilePath, string relativePath, out bool refreshed)
        {
            refreshed = false;

            // Sanitisation flag
            bool sanitised = false;

            // Get file name and sanitise it
            string fileName = Path.GetFileName(realFilePath);
            string sanitisedFilename = SanitiseFilename(fileName);

            // If file name was sanitised
            if (fileName != sanitisedFilename)
            {
                // Replace filename in path with sanitised version
                relativePath = relativePath.Replace(fileName, sanitisedFilename);

                // Set flag
                sanitised = true;
            }

            // Generate the full mirror path
            string fullMirrorPath = Path.Combine(mirrorPath, relativePath);

            // Change extension
            fullMirrorPath = Path.ChangeExtension(fullMirrorPath, ".xml");

            if (!File.Exists(fullMirrorPath))
            {
                // XML doesn't exist: create it containing the real path
                File.WriteAllText(fullMirrorPath, realFilePath);
            }
            else if (IsStaleMirrorXml(realFilePath, fullMirrorPath))
            {
                // MP3 was modified after the XML was written (e.g., tag edit in Mp3tag):
                // overwrite with the real path so this run re-reads fresh tag data
                File.WriteAllText(fullMirrorPath, realFilePath);
                refreshed = true;
            }

            // Return sanitised flag
            return sanitised;
        }

        /// <summary>
        /// Returns true if the MP3 file has been modified more recently than its mirror XML,
        /// indicating that cached tag data in the XML is stale and should be refreshed.
        /// </summary>
        internal static bool IsStaleMirrorXml(string mp3Path, string xmlPath)
        {
            if (!File.Exists(xmlPath)) return false;
            return File.GetLastWriteTimeUtc(mp3Path) > File.GetLastWriteTimeUtc(xmlPath);
        }


        /// <summary>
        /// Print info about completed mirroring process
        /// </summary>
        /// <param name="statisticsInfo"></param>
        private void PrintStats(Tuple<int, int, int, List<string>> statisticsInfo)
        {
            // Extract info items
            int mp3FileCount = statisticsInfo.Item1;
            int sanitisedFileNames = statisticsInfo.Item2;
            int refreshedCount = statisticsInfo.Item3;
            List<string> nonMP3Files = statisticsInfo.Item4;

            // Print mirror path
            Console.WriteLine($" - Path: '{mirrorPath}'");

            // Print file count
            Console.WriteLine($" - MP3 file count: {mp3FileCount}");

            // Print non-MP3 file info
            var nonMP3info = ProcessNonMP3(nonMP3Files);
            Console.WriteLine($" - Non-MP3 files found: {nonMP3info.Item1}");
            if (nonMP3info.Item3 != null)
            {
                Console.WriteLine($"  - Extension list: {nonMP3info.Item3}");
            }
            if (nonMP3info.Item2 != null)
            {
                Console.Write(nonMP3info.Item2);
            }

            // Print sanitisation count
            Console.WriteLine($" - MP3 filenames sanitised: {sanitisedFileNames}");

            // Print refreshed count (only show when > 0 to avoid noise in normal runs)
            if (refreshedCount > 0)
                Console.WriteLine($" - XMLs refreshed (MP3 updated since last analysis): {refreshedCount}");

            // Print recreation setting
            Console.WriteLine($" - Recreated: {AgeChecker.RegenMirror}");

            // Finish and print time taken
            FinishAndPrintTimeTaken();
        }

        /// <summary>
        /// Generate info strings from a list of non-MP3 filenames
        /// </summary>
        /// <param name="nonMP3Files"></param>
        /// <returns>Tuple of (count info, detailed unexpected files info, extension list)</returns>
        private Tuple<string, string, string> ProcessNonMP3(List<string> nonMP3Files)
        {
            // Extract list of extensions
            var extList = nonMP3Files.Select(fileName => Path.GetExtension(fileName)).ToList();

            // Check against expected types
            var expectedExt = new HashSet<string> { ".ini", ".txt", ".lnk", ".ffs_db" };
            bool allExpected = extList.TrueForAll(ext => expectedExt.Contains(ext));

            // Combine and format info
            string nonMP3infoStr = $"{extList.Count} ({(allExpected ? "all expected" : "UNEXPECTED!")})";

            // Format extension list (only if unexpected)
            string extListInfo = null;

            // If extensions were unexpected, build details string
            string detailedInfo = null;
            if (!allExpected)
            {
                // Only show extension list if there are unexpected ones
                extListInfo = string.Join(", ", extList.Distinct().OrderBy(e => e));

                // Group files by extension, filter to only unexpected ones
                var unexpectedByExt = nonMP3Files
                    .GroupBy(file => Path.GetExtension(file))
                    .Where(group => !expectedExt.Contains(group.Key))
                    .OrderBy(group => group.Key);

                var sb = new StringBuilder();
                sb.Append("  - Unexpected file types:\n");
                foreach (var extGroup in unexpectedByExt)
                {
                    sb.Append($"    {extGroup.Key} ({extGroup.Count()} files):\n");
                    foreach (var file in extGroup.OrderBy(f => f))
                    {
                        sb.Append($"      - {file}\n");
                    }
                }
                detailedInfo = sb.ToString();
            }

            // Return info
            return Tuple.Create<string, string, string>(nonMP3infoStr, detailedInfo, extListInfo);
        }


        /// <summary>
        /// Sanitise a given filename of special characters
        /// </summary>
        /// <param name="filename">Original filename.</param>
        /// <returns>Sanitised filename.</returns>
        public static string SanitiseFilename(string filename)
        {
            // Remove any non-ASCII characters and replace wide characters with their closest equivalent
            string sanitisedFilename = new string(filename
                .Where(c => Char.GetUnicodeCategory(c) != UnicodeCategory.Control &&
                            Char.GetUnicodeCategory(c) != UnicodeCategory.ModifierSymbol &&
                            Char.GetUnicodeCategory(c) != UnicodeCategory.OtherSymbol)
                .ToArray());
            sanitisedFilename = Encoding.ASCII.GetString(Encoding.GetEncoding("Cyrillic").GetBytes(sanitisedFilename));

            // Replace invalid characters with an underscore if found
            if (sanitisedFilename.Any(c => Constants.InvalidChars.Contains(c)))
            {
                sanitisedFilename = new string(sanitisedFilename.Select(c => Constants.InvalidChars.Contains(c) ? '_' : c).ToArray());
            }

            // Return new file name
            return sanitisedFilename;
        }


        /// <summary>
        /// Calculates the relative path from a base path to a target path.
        /// </summary>
        /// <param name="basePath">The base path.</param>
        /// <param name="targetPath">The target path.</param>
        /// <returns>The relative path from the base path to the target path.</returns>
        private string GetRelativePath(string basePath, string targetPath)
        {
            // Ensure basePath is treated as a directory (trailing separator) so Uri.MakeRelativeUri
            // returns paths relative to the directory rather than including the directory name.
            string basePathFixed = basePath;
            if (!basePathFixed.EndsWith(Path.DirectorySeparatorChar.ToString()) && !basePathFixed.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                basePathFixed += Path.DirectorySeparatorChar;
            }

            // Convert paths to URIs for accurate relative path calculation
            Uri baseUri = new Uri(basePathFixed);
            Uri targetUri = new Uri(targetPath);

            // Calculate relative URI
            Uri relativeUri = baseUri.MakeRelativeUri(targetUri);

            // Convert relative URI to a string and unescape special characters
            return Uri.UnescapeDataString(relativeUri.ToString());
        }
    }
}
