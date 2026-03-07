using System;
using System.Diagnostics;
using System.IO;

namespace AudioManager
{
    /// <summary>
    /// Saves the audio report to the REPORTS folder in the repo root.
    /// </summary>
    internal static class ReportWriter
    {
        /// <summary>
        /// Saves the given report content to a dated .txt file under REPORTS\{year}\. 
        /// Overwrites any existing report for today. Prepends a generated header.
        /// </summary>
        /// <param name="reportContent">The captured console output from the Analysis run.</param>
        public static void Save(string reportContent)
        {
            DateTime now = DateTime.Now;
            string year = now.Year.ToString();
            string yearFolder = Path.Combine(Constants.ReportsPath, year);
            string filename = now.ToString("yyyy-MM-dd") + " - AudioReport.txt";
            string fullPath = Path.Combine(yearFolder, filename);

            // Build and prepend header
            string header = BuildHeader(now);

            // Save to file (overwrite same-day report if it exists)
            Directory.CreateDirectory(yearFolder);
            File.WriteAllText(fullPath, header + reportContent);

            Console.WriteLine($"Report saved: REPORTS\\{year}\\{filename}");
        }

        /// <summary>
        /// Builds the report file header with date, git commit hash and link.
        /// </summary>
        private static string BuildHeader(DateTime now)
        {
            string date = now.ToString("dddd d MMMM yyyy, HH:mm");
            string commitHash = GetGitCommitHash();
            string commitLink = string.IsNullOrEmpty(commitHash)
                ? "unavailable"
                : $"https://github.com/DavoDC/AudioManager/commit/{commitHash}";

            return "###### Audio Report ######\n" +
                   $"Generated: {date}\n" +
                   $"Commit:    {commitHash ?? "unknown"}\n" +
                   $"Link:      {commitLink}\n" +
                   "##########################\n\n";
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
