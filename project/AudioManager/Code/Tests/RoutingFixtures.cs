using System;
using System.IO;

namespace AudioManager
{
    internal static class RoutingFixtures
    {
        /// <summary>
        /// Creates a temp library directory with the given artist and Musivation folders.
        /// Returns the temp root path. Caller must delete with Cleanup() in a try/finally.
        /// </summary>
        internal static string CreateLibraryFixture(string[] artistFolders = null, string[] musivationFolders = null)
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AudioManagerTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tempRoot);

            if (artistFolders != null)
            {
                foreach (var artist in artistFolders)
                    Directory.CreateDirectory(Path.Combine(tempRoot, Constants.ArtistsDir, artist));
            }

            if (musivationFolders != null)
            {
                foreach (var artist in musivationFolders)
                    Directory.CreateDirectory(Path.Combine(tempRoot, Constants.MusivDir, artist));
            }

            return tempRoot;
        }

        /// <summary>
        /// Adds N placeholder .mp3 files into Artists/{artist}/{album}/ under the given library root.
        /// Used to make CountAlbumSongs return a controlled value.
        /// </summary>
        internal static void AddAlbumFiles(string libraryRoot, string artist, string album, int fileCount)
        {
            string albumDir = Path.Combine(libraryRoot, Constants.ArtistsDir, artist, album);
            Directory.CreateDirectory(albumDir);
            for (int i = 0; i < fileCount; i++)
                File.WriteAllBytes(Path.Combine(albumDir, $"track{i + 1}.mp3"), new byte[0]);
        }

        /// <summary>Deletes the temp directory tree created by CreateLibraryFixture.</summary>
        internal static void Cleanup(string tempRoot)
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }
}
