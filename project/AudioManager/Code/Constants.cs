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

        // Absolute path to the repo root (discovered by walking up from exe directory)
        public static readonly string RepoRoot = FindRepoRoot();

        // Relative path from executable back to project folder
        public const string ProjectPath = "..\\..\\..\\";

        // Absolute path to the mirror repo (assumed to sit next to this repo in GitHubRepos\)
        public static readonly string MirrorRepoPath = Path.GetFullPath(
            Path.Combine(RepoRoot, "..", "AudioMirror")) + Path.DirectorySeparatorChar;

        // Mirror folder name
        public const string MirrorFolderName = "AUDIO_MIRROR";

        // Absolute path to mirror folder
        public static readonly string MirrorFolderPath = Path.Combine(MirrorRepoPath, MirrorFolderName);

        // Path to the reports folder in the repo root
        public static readonly string ReportsPath = Path.Combine(RepoRoot, "reports");

        // Path to the LibChecker exceptions config file
        public static readonly string LibCheckerExceptionsPath = Path.Combine(RepoRoot, "config", "libchecker-exceptions.xml");

        // Path to the integration logs folder (gitignored, written by MusicIntegrator)
        public static readonly string LogsPath = Path.Combine(RepoRoot, "logs");

        /// <summary>
        /// Library folder names
        /// </summary>

        public const string MiscDir    = "Miscellaneous Songs";
        public const string ArtistsDir = "Artists";
        public const string MusivDir   = "Musivation";
        public const string MotivDir   = "Motivation";
        public const string SourcesDir = "Sources";
        public const string SinglesDir = "Singles";

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

        /// <summary>
        /// Finds the repo root by walking up from the exe directory until a sentinel file is found.
        /// Sentinels: CLAUDE.md, README.md, or .git directory.
        /// Falls back to exe directory if no sentinel is found.
        /// Replaces hardcoded ".." chains with dynamic discovery.
        /// </summary>
        /// <returns>The repo root directory path.</returns>
        private static string FindRepoRoot()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            string[] sentinels = { "CLAUDE.md", "README.md", ".git" };

            while (!string.IsNullOrEmpty(current))
            {
                foreach (var sentinel in sentinels)
                {
                    string path = Path.Combine(current, sentinel);
                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        return current;
                    }
                }

                DirectoryInfo parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}