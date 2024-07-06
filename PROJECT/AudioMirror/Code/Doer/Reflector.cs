using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace AudioMirror
{
    /// <summary>
    /// Creates a lightweight 'mirror' of an Audio track folder.
    /// </summary>
    internal class Reflector : Doer
    {
        //// CONSTANTS
        // Actual Audio folder path
        string audioFolderPath = @"C:\Users\David\Audio\";

        // Invalid file name characters
        char[] invalidChars = Path.GetInvalidFileNameChars();

        //// VARIABLES
        string mirrorPath;
        bool recreateMirror;


        /// <summary>
        /// Construct an audio mirror
        /// </summary>
        /// <param name="mirrorPath">The audio mirror folder path</param>
        /// <param name="recreateMirror">Whether to recreate the mirror each time</param>
        public Reflector(string mirrorPath, bool recreateMirror)
        {
            // Save parameters
            this.mirrorPath = mirrorPath;
            this.recreateMirror = recreateMirror;

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
            if (recreateMirror)
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
        /// Create a mirrored file
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
            Console.WriteLine($" - Non-MP3 files found: {ProcessNonMP3(nonMP3Files)}");

            // Print sanitisation count
            Console.WriteLine($" - MP3 filenames sanitised: {sanitisedFileNames}");

            // Print recreation setting
            Console.WriteLine($" - Recreated: {recreateMirror}");

            // Print time taken
            PrintTimeTaken();
        }

        /// <summary>
        /// Generate an info string from a list of non-MP3 filenames
        /// </summary>
        /// <param name="nonMP3Files"></param>
        /// <returns></returns>
        private string ProcessNonMP3(List<string> nonMP3Files)
        {
            // Extract non-MP3 file count
            int fileCount = nonMP3Files.Count;

            // Info string
            string infoStr = fileCount.ToString();

            // Expected flag
            bool expected = true;

            // For each filename
            infoStr += " (";
            for (int i = 0; i < fileCount; i++)
            {
                // Get filename
                string fileName = nonMP3Files[i];

                // Extract extension and add
                string fileExt = "." + fileName.Split('.')[1].Trim();
                infoStr += fileExt;

                // Add comma if not last
                if(i != fileCount - 1)
                {
                    infoStr += ",";
                }

                // If extension is not an expected one
                if (fileExt != ".ini" && fileExt != ".txt" && fileExt != ".lnk" && fileExt != ".ffs_db")
                {
                    expected = false;
                }
            }
            infoStr += ")";

            // Add note regarding expected flag
            infoStr += expected ? " (expected)" : " (UNEXPECTED!!!)";

            return infoStr;
        }


        /// <summary>
        /// Sanitise a given filename of special characters
        /// </summary>
        /// <param name="filename">Original filename.</param>
        /// <returns>Sanitised filename.</returns>
        private string SanitiseFilename(string filename)
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
