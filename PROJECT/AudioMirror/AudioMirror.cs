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
    internal class AudioMirror
    {
        // Audio folder path
        string audioFolderPath = @"C:\Users\David\Audio\";

        // Relative mirror path
        string relMirrorPath = "..\\..\\..\\..\\AUDIO_MIRROR";

        // Filenames sanitized
        private int sanitizationCount = 0;

        // Invalid file name characters
        char[] invalidChars = Path.GetInvalidFileNameChars();


        /// <summary>
        /// Constructor
        /// </summary>
        public AudioMirror()
        {
            // Initialize count
            sanitizationCount = 0;

            // Start
            Console.WriteLine("\n### Audio Mirror ###");
            var start_time = DateTime.Now;

            // Set destination mirror path relative to where program is running
            string programDir = AppDomain.CurrentDomain.BaseDirectory;
            string mirrorPath = Path.Combine(programDir, relMirrorPath);

            // Setup folders
            SetupFolders(audioFolderPath, mirrorPath);

            // Populate with files
            var mirrorInfo = MirrorMp3Files(audioFolderPath, mirrorPath);

            // Calculate execution time
            var executionTime = DateTime.Now - start_time;

            // Print info
            PrintInfo(mirrorInfo, executionTime);
        }


        /// <summary>
        /// Set up folder structure
        /// </summary>
        /// <param name="srcFolder">The folder being mirrored</param>
        /// <param name="destFolder">The destination folder</param>
        /// <returns></returns>
        public void SetupFolders(string srcFolder, string destFolder)
        {
            // Remove the destination folder if it exists
            if (Directory.Exists(destFolder))
            {
                Directory.Delete(destFolder, true);
            }

            // Get sub dirs
            string[] dirs = Directory.GetDirectories(srcFolder, "*", SearchOption.AllDirectories);

            // For each directory
            foreach (var directoryPath in dirs)
            {
                // Create equivalent directory
                string relativePath = GetRelativePath(srcFolder, directoryPath);
                string newDirectoryPath = Path.Combine(destFolder, relativePath);
                Directory.CreateDirectory(newDirectoryPath);
            }
        }


        /// <summary>
        /// Populate folder structure with mirrored files
        /// </summary>
        /// <param name="srcFolder">The folder being mirrored</param>
        /// <param name="destFolder">The destination folder</param>
        /// <returns></returns>
        private Tuple<int, List<string>> MirrorMp3Files(string srcFolder, string destFolder)
        {
            // Holders
            int fileCount = 0;
            List<string> nonMp3Files = new List<string>();

            // For every file
            foreach (var filePath in Directory.GetFiles(srcFolder, "*", SearchOption.AllDirectories))
            {
                // If its an MP3 file
                if (Path.GetExtension(filePath)?.ToLower() == ".mp3")
                {
                    // Get relative file path
                    string relativePath = GetRelativePath(srcFolder, filePath);

                    // Replace file name part with sanitized file name
                    string fileName = Path.GetFileName(filePath);
                    string sanitizedFilename = SanitizeFilename(fileName);
                    relativePath = relativePath.Replace(fileName, sanitizedFilename);

                    // Get full path
                    string newFilePath = Path.Combine(destFolder, relativePath);
                    newFilePath = Path.ChangeExtension(newFilePath, ".txt");

                    // Create an empty .txt file if it doesn't exist already
                    if (!File.Exists(newFilePath))
                    {
                        File.WriteAllText(newFilePath, string.Empty);
                    }

                    // Increment file count
                    fileCount++;
                }
                else
                {
                    // Else if it is not an MP3 file, add to list
                    nonMp3Files.Add(GetRelativePath(srcFolder, filePath));
                }
            }

            // Return holders
            return Tuple.Create(fileCount, nonMp3Files);
        }


        /// <summary>
        /// Print info about completed mirroring process 
        /// </summary>
        /// <param name="mirrorInfo"></param>
        /// <param name="executionTime"></param>
        private void PrintInfo(Tuple<int, List<string>> mirrorInfo, TimeSpan executionTime)
        {
            // Extract mirror info items
            int fileCount = mirrorInfo.Item1;
            List<String> nonMp3Files = mirrorInfo.Item2;

            // Print file count
            Console.WriteLine($"\nMP3 file count: {fileCount}");

            // Print sanitization count
            Console.WriteLine($"\nMP3 filenames sanitized: {sanitizationCount}");

            // Print non-MP3 file info
            Console.WriteLine($"\nNon-MP3 files found: {nonMp3Files.Count}");
            //foreach (var file in nonMp3Files)
            //{
            //    Console.WriteLine("   " + file);
            //}

            // Print time taken
            Console.WriteLine($"\nTime taken: {Math.Round(executionTime.TotalSeconds, 3)} seconds");

            // Finish message
            Console.WriteLine("\nFinished!\n");
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

            // If filename was sanitized
            if (filename != sanitizedFilename)
            {
                // Increment count
                sanitizationCount++;

                // Print the filenames
                //Console.WriteLine("\nOriginal: " + filename);
                //Console.WriteLine("Sanitized: " + sanitizedFilename);
            }

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
