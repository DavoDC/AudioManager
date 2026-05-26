using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

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

            // Build commit message with artist breakdown
            string commitTitle = DateTime.Now.ToString("MMM d") + " Update";
            string commitBody = BuildCommitBody(repoPath);
            string fullMessage = string.IsNullOrWhiteSpace(commitBody)
                ? commitTitle
                : commitTitle + "\n" + commitBody;

            Console.WriteLine($" - AudioMirror's changes are ready to be committed!");
            Console.WriteLine($" - Commit in GHD with the message:");
            Console.WriteLine();
            Console.WriteLine(fullMessage);
            Console.WriteLine();

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
        /// Builds the commit message body by parsing new files in the AudioMirror repo.
        /// Only counts untracked (new) files - modified files are regen noise.
        /// Sections: New artists / Existing artists / Musivation / Misc.
        /// </summary>
        private static string BuildCommitBody(string repoPath)
        {
            // Use -u all to get individual files, not just directory stubs
            string statusOutput = RunGit(repoPath, "status --porcelain=v1 -u all");
            if (string.IsNullOrWhiteSpace(statusOutput))
                return string.Empty;

            // Get artist dirs already tracked in HEAD to distinguish new vs existing
            var trackedArtists = GetTrackedArtists(repoPath);

            // Buckets
            var newArtistAlbums    = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
            var existingArtistAlbums = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
            var musivationTracks   = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var miscArtists        = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string line in statusOutput.Split('\n'))
            {
                if (!line.StartsWith("??")) continue;

                // Path starts after "?? ", normalize separators
                string path = line.Substring(3).Trim().Replace('\\', '/');

                // Strip leading AUDIO_MIRROR/ prefix
                const string prefix = "AUDIO_MIRROR/";
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string rel = path.Substring(prefix.Length);

                string[] parts = rel.Split('/');
                if (parts.Length < 2) continue;
                string topFolder = parts[0];

                if (topFolder.Equals(Constants.ArtistsDir, StringComparison.OrdinalIgnoreCase)
                    && parts.Length >= 3)
                {
                    string artist = parts[1];
                    string album  = parts[2];

                    if (!trackedArtists.Contains(artist))
                    {
                        AddAlbum(newArtistAlbums, artist, album);
                    }
                    else
                    {
                        AddAlbum(existingArtistAlbums, artist, album);
                    }
                }
                else if (topFolder.Equals(Constants.MusivDir, StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = parts[parts.Length - 1];
                    if (fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        fileName = fileName.Substring(0, fileName.Length - 4);
                    if (!string.IsNullOrWhiteSpace(fileName))
                        musivationTracks.Add(fileName);
                }
                else if (topFolder.Equals(Constants.MiscDir, StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = parts[parts.Length - 1];
                    if (fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        fileName = fileName.Substring(0, fileName.Length - 4);
                    // Extract artist part before " - "
                    int dashIdx = fileName.IndexOf(" - ", StringComparison.Ordinal);
                    string artist = dashIdx > 0 ? fileName.Substring(0, dashIdx) : fileName;
                    if (!string.IsNullOrWhiteSpace(artist))
                        miscArtists.Add(artist);
                }
            }

            if (newArtistAlbums.Count == 0 && existingArtistAlbums.Count == 0
                && musivationTracks.Count == 0 && miscArtists.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            if (newArtistAlbums.Count > 0)
            {
                sb.AppendLine("\nNew artists:");
                foreach (var kvp in newArtistAlbums.OrderBy(k => k.Key))
                {
                    string albumList = string.Join(", ", kvp.Value);
                    sb.AppendLine($"- {kvp.Key} ({albumList})");
                }
            }

            if (existingArtistAlbums.Count > 0)
            {
                sb.AppendLine("\nExisting artists:");
                var entries = existingArtistAlbums.OrderBy(k => k.Key).Select(kvp =>
                {
                    bool singlesOnly = kvp.Value.Count == 1
                        && kvp.Value.First().Equals(Constants.SinglesDir, StringComparison.OrdinalIgnoreCase);
                    return singlesOnly
                        ? kvp.Key
                        : $"{kvp.Key} ({string.Join(", ", kvp.Value)})";
                });
                sb.AppendLine("- " + string.Join(", ", entries));
            }

            if (musivationTracks.Count > 0)
            {
                sb.AppendLine("\nMusivation:");
                foreach (string track in musivationTracks)
                    sb.AppendLine($"- {track}");
            }

            if (miscArtists.Count > 0)
                sb.AppendLine("\nMisc: " + string.Join(", ", miscArtists));

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Returns artist directory names already tracked in HEAD (i.e. not new this run).
        /// </summary>
        private static HashSet<string> GetTrackedArtists(string repoPath)
        {
            string output = RunGit(repoPath, $"ls-tree --name-only HEAD AUDIO_MIRROR/{Constants.ArtistsDir}/");
            return new HashSet<string>(
                output.Split('\n')
                      .Select(l => l.Trim())
                      .Where(l => !string.IsNullOrEmpty(l)),
                StringComparer.OrdinalIgnoreCase);
        }

        private static void AddAlbum(Dictionary<string, SortedSet<string>> dict, string artist, string album)
        {
            if (!dict.TryGetValue(artist, out var albums))
            {
                albums = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                dict[artist] = albums;
            }
            albums.Add(album);
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
