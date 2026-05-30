using AudioManager.Code.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AudioManager
{
    /// <summary>
    /// Validates GetDestDir routing decisions against a JSON manifest file.
    /// Manifest format: array of objects with artist, title, album, genres (optional),
    /// filename, scenario (optional label), and expectedDest (relative to Audio root).
    /// Exits 0 if all pass, 1 if any fail.
    /// </summary>
    internal static class ManifestRunner
    {
        internal static bool Run(string manifestPath)
        {
            Console.WriteLine("\n###### Routing Manifest ######\n");

            if (!File.Exists(manifestPath))
            {
                Console.WriteLine($"[ERROR] Manifest not found: {manifestPath}");
                return false;
            }

            string json;
            try { json = File.ReadAllText(manifestPath, System.Text.Encoding.UTF8); }
            catch (Exception ex) { Console.WriteLine($"[ERROR] Could not read manifest: {ex.Message}"); return false; }

            List<ManifestEntry> entries;
            try { entries = ParseManifest(json); }
            catch (Exception ex) { Console.WriteLine($"[ERROR] Could not parse manifest: {ex.Message}"); return false; }

            if (entries.Count == 0)
            {
                Console.WriteLine("[ERROR] Manifest contains no valid entries.");
                return false;
            }

            Console.WriteLine($"Loaded {entries.Count} entry(ies) from: {Path.GetFileName(manifestPath)}");
            Console.WriteLine($"Library: {Constants.AudioFolderPath}\n");

            // Compute scan-ahead: artists with 3+ entries that have no existing library folder
            var artistCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
                artistCounts[e.PrimaryArtist] = artistCounts.ContainsKey(e.PrimaryArtist)
                    ? artistCounts[e.PrimaryArtist] + 1 : 1;

            var newArtistFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in artistCounts)
            {
                if (kvp.Value >= 3)
                {
                    string artistFolder = Path.Combine(Constants.AudioFolderPath, Constants.ArtistsDir, kvp.Key);
                    string musivFolder  = Path.Combine(Constants.AudioFolderPath, Constants.MusivDir,   kvp.Key);
                    if (!Directory.Exists(artistFolder) && !Directory.Exists(musivFolder))
                        newArtistFolders.Add(kvp.Key);
                }
            }
            if (newArtistFolders.Count > 0)
                Console.WriteLine($"Scan-ahead: {newArtistFolders.Count} artist(s) will hit 3-song threshold: {string.Join(", ", newArtistFolders)}\n");

            // Create integrator with real library path (no pipeline execution)
            var integrator = new MusicIntegrator(Constants.AudioFolderPath);

            int passed = 0, failed = 0;
            foreach (var entry in entries)
            {
                var track = new Track
                {
                    Artists = entry.Artist,
                    Title   = entry.Title,
                    Album   = entry.Album,
                    Genres  = entry.Genres,
                    Year    = "Missing",
                };

                string actualAbsolute = integrator.GetDestDir(track, newArtistFolders, out string reason, out bool isNewFolder);
                string actualRel  = NormalizeDest(GetRelativeDest(actualAbsolute));
                string expectedRel = NormalizeDest(entry.ExpectedDest);

                bool ok = actualRel.Equals(expectedRel, StringComparison.OrdinalIgnoreCase);
                string label = !string.IsNullOrEmpty(entry.Scenario) ? entry.Scenario : entry.Filename;

                if (ok)
                {
                    Console.WriteLine($"[PASS] {label}");
                    Console.WriteLine($"       -> {entry.ExpectedDest}");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"[FAIL] {label}");
                    Console.WriteLine($"       Expected: {entry.ExpectedDest}");
                    Console.WriteLine($"       Actual:   {GetRelativeDest(actualAbsolute)}");
                    Console.WriteLine($"       Reason:   {reason}");
                    failed++;
                }
                Console.WriteLine();
            }

            Console.WriteLine("-------------------------------");
            Console.WriteLine($"Results: {passed} passed, {failed} failed");
            Console.WriteLine("-------------------------------\n");

            return failed == 0;
        }

        // ---- helpers ----

        private static string GetRelativeDest(string absoluteDest)
        {
            string audioPath = Constants.AudioFolderPath;
            if (absoluteDest.StartsWith(audioPath, StringComparison.OrdinalIgnoreCase))
                return absoluteDest.Substring(audioPath.Length).TrimStart('\\', '/');
            return absoluteDest;
        }

        private static string NormalizeDest(string dest)
        {
            if (dest == null) return "";
            return dest.Replace('/', '\\').TrimEnd('\\');
        }

        // ---- simple JSON manifest parser (handles flat array of string-valued objects) ----

        private static List<ManifestEntry> ParseManifest(string json)
        {
            var result = new List<ManifestEntry>();
            // Match each {...} object block
            var objMatches = Regex.Matches(json, @"\{[^}]+\}", RegexOptions.Singleline);
            foreach (Match obj in objMatches)
            {
                string text = obj.Value;
                string artist    = ExtractStr(text, "artist");
                string expected  = ExtractStr(text, "expectedDest");
                if (string.IsNullOrEmpty(artist) || string.IsNullOrEmpty(expected)) continue;

                result.Add(new ManifestEntry
                {
                    Artist      = artist,
                    Title       = ExtractStr(text, "title")    ?? "Missing",
                    Album       = ExtractStr(text, "album")    ?? "Missing",
                    Genres      = ExtractStr(text, "genres")   ?? "Missing",
                    Filename    = ExtractStr(text, "filename") ?? $"{artist}.mp3",
                    Scenario    = ExtractStr(text, "scenario"),
                    ExpectedDest = expected,
                });
            }
            return result;
        }

        private static string ExtractStr(string obj, string key)
        {
            var m = Regex.Match(obj, $@"""{Regex.Escape(key)}""\s*:\s*""((?:[^""\\]|\\.)*)""");
            if (!m.Success) return null;
            return m.Groups[1].Value
                .Replace("\\\\", "\x00SLASH\x00")
                .Replace("\\\"", "\"")
                .Replace("\\n", "\n")
                .Replace("\x00SLASH\x00", "\\");
        }
    }

    internal class ManifestEntry
    {
        public string Artist;
        public string Title;
        public string Album;
        public string Genres;
        public string Filename;
        public string Scenario;
        public string ExpectedDest;
        public string PrimaryArtist => Artist.Contains(";") ? Artist.Split(';')[0].Trim()
                                     : Artist.Contains(",") ? Artist.Split(',')[0].Trim()
                                     : Artist;
    }
}
