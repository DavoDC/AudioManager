using System;
using System.IO;

namespace AudioManager
{
    internal class Program
    {
        //// CONSTANTS/SETTINGS

        // Common user base path (e.g. C:\Users\David\) from %USERPROFILE% with fallback
        private static readonly string userBasePath = GetUserBasePath();
        public static string UserBasePath { get => userBasePath; }

        // Actual Audio folder path
        private static readonly string audioFolderPath = Path.Combine(userBasePath, "Audio");
        public static string AudioFolderPath { get => audioFolderPath; }

        // Additional audio path (Downloads\NewMusic)
        private static readonly string newMusicPath = Path.Combine(userBasePath, "Downloads", "NewMusic");
        public static string NewMusicPath { get => newMusicPath; }

        // The relative path from the executable back to the project folder
        private static readonly string projectPath = "..\\..\\..\\";
        public static string ProjectPath { get => projectPath; }

        // The relative path to the mirror repo (assumed to be next to this repo)
        private static readonly string mirrorRepoPath = ProjectPath + "..\\..\\AudioMirror\\";
        public static string MirrorRepoPath { get => mirrorRepoPath; }

        // The mirror folder name
        private static readonly string mirrorFolderName = "AUDIO_MIRROR";
        public static string MirrorFolderName { get => mirrorFolderName; }

        // The relative path to the mirror folder
        private static readonly string mirrorFolderPath = $"{MirrorRepoPath}\\{MirrorFolderName}";
        public static string MirrorFolderPath { get => mirrorFolderPath; }

        /// <summary>
        /// Main function
        /// </summary>
        /// <param name="args">Arguments given to program</param>
        static void Main(string[] args)
        {
            try
            {
                // Start message
                Console.WriteLine("\n###### Audio Manager ######");

                // Get the path of the executable
                string progExecPath = AppDomain.CurrentDomain.BaseDirectory;

                // Set mirror path relative to the executable
                string mirrorPath = Path.GetFullPath(Path.Combine(progExecPath, MirrorFolderPath));

                // Toggle forcing mirror to be regenerated (e.g. during development)
                bool forceMirrorRegen = false;
                //bool forceMirrorRegen = true;

                // 0) Check the age of the mirror
                AgeChecker ac = new AgeChecker(forceMirrorRegen);

                // 1) Create mirror of audio folder
                // Note: Files created at this stage just contain paths to the actual file, not metadata info.
                Reflector r = new Reflector(mirrorPath);

                // 2) Parse metadata into XML files and tag list
                // Note: The file contents get overwritten with actual XML content in this stage.
                Parser p = new Parser(mirrorPath);

                // 3) Analyse metadata and print statistics
                Analyser a = new Analyser(p.audioTags);

                // 4) Do audio library organisational/metadata checks
                LibChecker lc = new LibChecker(p.audioTags);

                // Print total time taken
                Doer.PrintTotalTimeTaken();

                // Finish message
                Console.WriteLine("\nFinished!\n");
            }
            catch (Exception ex)
            {
                // Handle exceptions
                Console.WriteLine($"\nEXCEPTION ENCOUNTERED");
                Console.WriteLine($"\nMessage: {ex.Message}");
                Console.WriteLine($"\nStack Trace: \n{ex.StackTrace}");
                Console.WriteLine("\n");
                Environment.Exit(123);
            }
        }

        /// <summary>
        /// Gets the current user's profile directory.
        /// </summary>
        /// <remarks>
        /// Uses the USERPROFILE environment variable with a fallback to
        /// <see cref="Environment.SpecialFolder.UserProfile"/>. Ensures the
        /// returned path ends with a directory separator.
        /// </remarks>
        /// <returns>
        /// The user profile path with a trailing directory separator.
        /// </returns>
        private static string GetUserBasePath()
        {
            string userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            if (string.IsNullOrEmpty(userProfile))
            {
                userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            // Ensure trailing separator
            if (!userProfile.EndsWith(Path.DirectorySeparatorChar.ToString()) && !userProfile.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                userProfile += Path.DirectorySeparatorChar;
            }

            return userProfile;
        }
    }
}