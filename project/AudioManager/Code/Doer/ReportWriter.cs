using System;
using System.Diagnostics;
using System.IO;

namespace AudioManager
{
    /// <summary>
    /// Saves the audio report to the reports folder in the repo root.
    /// </summary>
    internal static class ReportWriter
    {
        /// <summary>
        /// Saves the given report content to a dated .txt file.
        /// If LibChecker found issues, saves to reports\with-issues\ (gitignored).
        /// If clean, saves to reports\{year}\ for records.
        /// Overwrites any existing report for today. Prepends a generated header.
        /// </summary>
        /// <param name="reportContent">The captured console output from the Analysis run.</param>
        /// <param name="libCheckerClean">Whether LibChecker reported zero issues.</param>
        public static void Save(string reportContent, bool libCheckerClean)
        {
            DateTime now = DateTime.Now;
            string filename = now.ToString("yyyy-MM-dd") + " - AudioReport.md";

            // Determine save location based on LibChecker status
            string folder;
            string displayPath;
            if (libCheckerClean)
            {
                // Clean library - save to permanent records
                string year = now.Year.ToString();
                folder = Path.Combine(Constants.ReportsPath, year);
                displayPath = $"reports\\{year}\\{filename}";
            }
            else
            {
                // Issues found - save to gitignored folder to avoid cluttering diffs
                folder = Path.Combine(Constants.ReportsPath, "with-issues");
                displayPath = $"reports\\with-issues\\{filename}";
            }

            // Build and prepend header
            string header = BuildHeader(now);

            // Save to file (overwrite same-day report if it exists)
            Directory.CreateDirectory(folder);
            string fullPath = Path.Combine(folder, filename);
            File.WriteAllText(fullPath, header + reportContent);

            Console.WriteLine($"\nReport saved: {displayPath}");
        }

        /// <summary>
        /// Builds the report file header with date, git commit hash and link in markdown format.
        /// </summary>
        private static string BuildHeader(DateTime now)
        {
            string date = now.ToString("dddd d MMMM yyyy, HH:mm");
            string commitHash = GetGitCommitHash();
            string commitLink = string.IsNullOrEmpty(commitHash)
                ? "unavailable"
                : $"[{commitHash}](https://github.com/DavoDC/AudioManager/commit/{commitHash})";

            return "# Audio Report\n\n" +
                   $"**Generated:** {date}  \n" +
                   $"**Commit:** {commitLink}\n\n" +
                   "---\n\n";
        }

        /// <summary>
        /// Runs 'git rev-parse HEAD' and returns the full commit hash.
        /// Returns null if git is unavailable or the command fails.
        /// </summary>
        private static string GetGitCommitHash()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("git", "rev-parse HEAD")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Constants.ReportsPath
                };
                using (Process proc = Process.Start(psi))
                {
                    string hash = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit();
                    return string.IsNullOrEmpty(hash) ? null : hash;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
