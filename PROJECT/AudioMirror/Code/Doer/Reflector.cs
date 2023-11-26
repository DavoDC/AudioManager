using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AudioMirror
{
    /// <summary>
    /// Creates a lightweight 'mirror' of an Audio track folder
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
            Console.WriteLine($"\n\nCreating mirror of '{audioFolderPath}'...");

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
            int sanitizationCount = 0;
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
                if(CreateFile(realFilePath, relativePath))
                {
                    sanitizationCount++;
                }

                mp3FileCount++;
            }

            // Return holders
            return Tuple.Create(mp3FileCount, sanitizationCount, nonMP3Files);
        }


        /// <summary>
        /// Create a mirrored file
        /// </summary>
        /// <param name="filePath">The actual file path</param>
        /// <param name="relativePath">The relative file path</param>
        /// <returns>True if the filename was sanitized</returns>
        private bool CreateFile(string realFilePath, string relativePath)
        {
            // Sanitization flag
            bool sanitized = false;

            // Get file name and sanitize it
            string fileName = Path.GetFileName(realFilePath);
            string sanitizedFilename = SanitizeFilename(fileName);

            // If file name was sanitized
            if (fileName != sanitizedFilename)
            {
                // Replace filename in path with sanitized version
                relativePath = relativePath.Replace(fileName, sanitizedFilename);

                // Set flag
                sanitized = true;
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

            // Return sanitized flag
            return sanitized;
        }


        /// <summary>
        /// Print info about completed mirroring process 
        /// </summary>
        /// <param name="statisticsInfo"></param>
        private void PrintStats(Tuple<int, int, List<string>> statisticsInfo)
        {
            // Extract info items
            int mp3FileCount = statisticsInfo.Item1;
            int sanitizedFileNames = statisticsInfo.Item2;
            List<string> nonMP3Files = statisticsInfo.Item3;

            // Print mirror path
            Console.WriteLine($" - Path: '{mirrorPath}'");

            // Print file count
            Console.WriteLine($" - MP3 file count: {mp3FileCount}");

            // Print non-MP3 file info
            Console.WriteLine($" - Non-MP3 files found: {nonMP3Files.Count}");
            //foreach (var file in nonMP3Files)
            //{
            //    Console.WriteLine("   " + file);
            //}

            // Print sanitization count
            Console.WriteLine($" - MP3 filenames sanitized: {sanitizedFileNames}");

            // Print recreation setting
            Console.WriteLine($" - Recreated: {recreateMirror}");

            // Print time taken
            PrintTimeTaken();
        }


        /// <summary>
        /// Sanitize a given filename of special characters
        /// </summary>
        /// <param name="filename">Original filename.</param>
        /// <returns>Sanitized filename.</returns>
        private string SanitizeFilename(string filename)
        {
            // Remove any non-ASCII characters and replace wide characters with their closest equivalent
            string sanitizedFilename = new string(filename
                .Where(c => Char.GetUnicodeCategory(c) != UnicodeCategory.Control &&
                            Char.GetUnicodeCategory(c) != UnicodeCategory.ModifierSymbol &&
                            Char.GetUnicodeCategory(c) != UnicodeCategory.OtherSymbol)
                .ToArray());
            sanitizedFilename = Encoding.ASCII.GetString(Encoding.GetEncoding("Cyrillic").GetBytes(sanitizedFilename));

            // Replace invalid characters with an underscore if found
            if (sanitizedFilename.Any(c => invalidChars.Contains(c)))
            {
                sanitizedFilename = new string(sanitizedFilename.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
            }

            // Return new file name
            return sanitizedFilename;
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
