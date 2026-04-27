using System;
using System.Diagnostics;
using System.IO;

namespace AudioManager
{
    /// <summary>
    /// Commits and pushes AudioMirror repo after a clean analysis run.
    /// Rules: only commit if LibChecker was clean AND files actually changed.
    /// </summary>
    internal static class AudioMirrorCommitter
    {
        /// <summary>
        /// Manual commit instructions for AudioMirror after analysis.
        /// Auto-commit is disabled until program is more stable and trusted.
        /// See IDEAS.md TIER 3 for future re-enable.
        /// </summary>
        /// <param name="libCheckerClean">Whether LibChecker reported zero issues.</param>
        public static void TryCommit(bool libCheckerClean)
        {
            string repoPath = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, Constants.MirrorRepoPath));

            Console.WriteLine("\nAudioMirror commit instructions:");

            if (!libCheckerClean)
            {
                Console.WriteLine(" - DO NOT COMMIT: LibChecker reported issues. Fix them first, then re-run analysis.");
                return;
            }

            if (!Directory.Exists(repoPath))
            {
                Console.WriteLine($" - ERROR: AudioMirror repo not found at: {repoPath}");
                return;
            }

            // Check if there are uncommitted changes
            string statusOutput = RunGit(repoPath, "status --porcelain");
            if (string.IsNullOrWhiteSpace(statusOutput))
            {
                Console.WriteLine(" - AudioMirror is clean (no changes to commit).");
                return;
            }

            // Manual commit instructions
            string commitMsg = DateTime.Now.ToString("MMM d") + " Update";
            Console.WriteLine($" - AudioMirror's changes are ready to be committed! ✅");
            Console.WriteLine($" - Commit in GHD with the message: {commitMsg}");

            // DISABLED AUTO-COMMIT (see IDEAS.md TIER 3 to re-enable when stable)
            // OLD CODE BELOW - commented out for future re-enable:
            //
            // // Stage AUDIO_MIRROR folder
            // // RunGit(repoPath, "add AUDIO_MIRROR/");
            // //
            // // // Build commit message: "Apr 9 Update"
            // // // string commitMsg = DateTime.Now.ToString("MMM d") + " Update";
            // // // string commitResult = RunGit(repoPath, $"commit -m \"{commitMsg}\"");
            // // // if (string.IsNullOrWhiteSpace(commitResult) || commitResult.Contains("nothing to commit"))
            // // // {
            // // //     Console.WriteLine(" - Nothing staged to commit.");
            // // //     return;
            // // // }
            // // //
            // // // Console.WriteLine($" - Committed: {commitMsg}");
            // // //
            // // // // Push
            // // // string pushResult = RunGit(repoPath, "push");
            // // // bool pushOk = !pushResult.Contains("error") && !pushResult.Contains("fatal");
            // // // Console.WriteLine(pushOk
            // // //     ? " - Pushed to remote."
            // // //     : $" - Push may have failed. Check output: {pushResult}");
        }

        /// <summary>
        /// Runs a git command in the given working directory and returns stdout+stderr.
        /// </summary>
        private static string RunGit(string workingDir, string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args)
                {
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();
                    return stdout + stderr;
                }
            }
            catch (Exception ex)
            {
                return $"error: {ex.Message}";
            }
        }
    }
}
