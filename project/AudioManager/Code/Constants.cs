using System;
using System.IO;

namespace AudioManager
{
    internal static class Constants
    {
        /// <summary>
        /// Paths
        /// </summary>

        // Common user base path from %USERPROFILE% with fallback (e.g. C:\Users\David\)
        public static readonly string UserBasePath = GetUserBasePath();

        // The path to the actual audio library (e.g. C:\Users\David\Audio\)
        public static readonly string AudioFolderPath = Path.Combine(UserBasePath, "Audio");

        // The path to the new music folder (e.g. C:\Users\David\Downloads\NewMusic\)
        public static readonly string NewMusicPath = Path.Combine(UserBasePath, "Downloads", "NewMusic");

        // Relative path from executable back to project folder
        public const string ProjectPath = "..\\..\\..\\";

        // Relative path to mirror repo (assumed to sit next to this repo)
        public static readonly string MirrorRepoPath = ProjectPath + "..\\..\\AudioMirror\\";

        // Mirror folder name
        public const string MirrorFolderName = "AUDIO_MIRROR";

        // Relative path to mirror folder
        public static readonly string MirrorFolderPath = $"{MirrorRepoPath}\\{MirrorFolderName}";

        // Path to the reports folder in the repo root (e.g. C:\Users\David\GitHubRepos\AudioManager\reports\)
        public static readonly string ReportsPath = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "reports"));

        /// <summary>
        /// Library folder names
        /// </summary>

        public const string MiscDir    = "Miscellaneous Songs";
        public const string ArtistsDir = "Artists";
        public const string MusivDir   = "Musivation";
        public const string MotivDir   = "Motivation";

        /// <summary>
        /// Mirror Management
        /// </summary>

        // Last run info file path
        public static readonly string LastRunInfoFilePath = MirrorRepoPath + "LastRunInfo.txt";

        // Age threshold for mirror regeneration
        public static readonly TimeSpan AgeThreshold = TimeSpan.FromDays(7);

        /// <summary>
        /// File System / Validation Utilities
        /// </summary>

        // Invalid file name characters
        public static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

        /// <summary>
        /// Tag validation
        /// </summary>

        /// Unwanted info in song titles that should be removed when validating tags
        public static readonly string[] UnwantedInfo = {
            "feat.", "ft.", "edit", "bonus", "original", "soundtrack", "version", "explicit"
        };

        /// <summary>
        /// Helpers
        /// </summary>

        /// <summary>
        /// Gets the user's base path from the USERPROFILE environment variable,
        /// falling back to the system UserProfile folder if unset. Ensures a trailing separator.
        /// </summary>
        /// <returns>The user base path ending with a directory separator.</returns>
        private static string GetUserBasePath()
        {
            string userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            if (string.IsNullOrEmpty(userProfile))
            {
                userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            if (!userProfile.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !userProfile.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                userProfile += Path.DirectorySeparatorChar;
            }

            return userProfile;
        }
    }
}