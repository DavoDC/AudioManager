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
        //// CONSTANTS
        // Actual Audio folder path
        private static readonly string audioFolderPath = @"C:\Users\David\Audio\";

        // Invalid file name characters
        private static readonly char[] invalidChars = Path.GetInvalidFileNameChars();

        //// VARIABLES
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
            Console.WriteLine($"\nCreating mirror of '{audioFolderPath}'...");

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
            string[] realDirs = Directory.GetDirectories(audioFolderPath, "*", SearchOption.AllDirectories);
            foreach (var directoryPath in realDirs)
            {
                // Get real relative path
                string relativePath = GetRelativePath(audioFolderPath, directoryPath);

                // Create relative folder within mirror path
                string newDirectoryPath = Path.Combine(mirrorPath, relativePath);
                Directory.CreateDirectory(newDirectoryPath);
            }
        }


        /// <summary>
        /// Populate folder structure with mirrored files
        /// </summary>
        /// <returns>Statistics tuple</returns>
        private Tuple<int, int, List<string>> CreateFiles()
        {
            // Holders
            int mp3FileCount = 0;
            int sanitisationCount = 0;
            List<string> nonMP3Files = new List<string>();

            // For every actual file
            string[] realFiles = Directory.GetFiles(audioFolderPath, "*", SearchOption.AllDirectories);
            foreach (var realFilePath in realFiles)
            {
                // Get relative file path
                string relativePath = GetRelativePath(audioFolderPath, realFilePath);

                // For non-MP3 files, add to list and skip
                if (Path.GetExtension(realFilePath)?.ToLower() != ".mp3")
                {
                    nonMP3Files.Add(relativePath);
                    continue;
                }

                // Otherwise, process MP3 file
                if (CreateFile(realFilePath, relativePath))
                {
                    sanitisationCount++;
                }

                mp3FileCount++;
            }

            // Return holders
            return Tuple.Create(mp3FileCount, sanitisationCount, nonMP3Files);
        }


        /// <summary>
        /// Create a mirrored file (the file contents are just the real path at this stage)
        /// </summary>
        /// <param name="filePath">The actual file path</param>
        /// <param name="relativePath">The relative file path</param>
        /// <returns>True if the filename was sanitised</returns>
        private bool CreateFile(string realFilePath, string relativePath)
        {
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

            // If the mirrored file doesn't exist already
            if (!File.Exists(fullMirrorPath))
            {
                // Create mirror file containing real path
                File.WriteAllText(fullMirrorPath, realFilePath);
            }

            // Return sanitised flag
            return sanitised;
        }


        /// <summary>
        /// Print info about completed mirroring process 
        /// </summary>
        /// <param name="statisticsInfo"></param>
        private void PrintStats(Tuple<int, int, List<string>> statisticsInfo)
        {
            // Extract info items
            int mp3FileCount = statisticsInfo.Item1;
            int sanitisedFileNames = statisticsInfo.Item2;
            List<string> nonMP3Files = statisticsInfo.Item3;

            // Print mirror path
            Console.WriteLine($" - Path: '{mirrorPath}'");

            // Print file count
            Console.WriteLine($" - MP3 file count: {mp3FileCount}");

            // Print non-MP3 file info
            var nonMP3info = ProcessNonMP3(nonMP3Files);
            Console.WriteLine($" - Non-MP3 files found: {nonMP3info.Item1}");
            if (nonMP3info.Item2 != null)
            {
                Console.WriteLine($"  - Extension list: {nonMP3info.Item2}");
            }

            // Print sanitisation count
            Console.WriteLine($" - MP3 filenames sanitised: {sanitisedFileNames}");

            // Print recreation setting
            Console.WriteLine($" - Recreated: {AgeChecker.RegenMirror}");

            // Finish and print time taken
            FinishAndPrintTimeTaken();
        }

        /// <summary>
        /// Generate an info string from a list of non-MP3 filenames
        /// </summary>
        /// <param name="nonMP3Files"></param>
        /// <returns></returns>
        private Tuple<string, string> ProcessNonMP3(List<string> nonMP3Files)
        {
            // Extract list of extensions
            var extList = nonMP3Files.Select(fileName => Path.GetExtension(fileName)).ToList();

            // Check against expected types
            var expectedExt = new HashSet<string> { ".ini", ".txt", ".lnk", ".ffs_db" };
            bool expected = extList.TrueForAll(ext => expectedExt.Contains(ext));

            // Combine and format info
            string nonMP3infoStr = $"{extList.Count} ({(expected ? "all expected" : "UNEXPECTED!")})";

            // If extensions were unexpected, include in info
            string extListInfo = expected ? null : string.Join(",", extList);

            // Return info and extensions list
            return Tuple.Create(nonMP3infoStr, extListInfo);
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
            if (sanitisedFilename.Any(c => invalidChars.Contains(c)))
            {
                sanitisedFilename = new string(sanitisedFilename.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
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
            // Convert paths to URIs for accurate relative path calculation
            Uri baseUri = new Uri(basePath);
            Uri targetUri = new Uri(targetPath);

            // Calculate relative URI
            Uri relativeUri = baseUri.MakeRelativeUri(targetUri);

            // Convert relative URI to a string and unescape special characters
            return Uri.UnescapeDataString(relativeUri.ToString());
        }
    }
}
