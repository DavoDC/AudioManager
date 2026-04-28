using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AudioManager.Code.Modules;
using TagLib;
using File = System.IO.File;

namespace AudioManager
{
    /// <summary>
    /// Cleans raw MP3 files in NewMusic folder before integration.
    /// Applies tag cleanup rules (removing parentheticals, fixing featured artists, setting TCMP, etc.)
    /// and renames files to match the library convention.
    /// </summary>
    internal class TagFixer : Doer
    {
        private bool dryRun;
        private int fixedCount = 0;
        private int skippedCount = 0;

        private class FixLog
        {
            public string Filename;
            public string OriginalFilename;
            public List<string> Changes = new List<string>();
            public string Status; // "fixed", "would-fix", "skipped", "error"
            public string Detail;
        }

        public TagFixer(bool dryRun = false)
        {
            this.dryRun = dryRun;
            string modeLabel = dryRun ? " [DRY RUN - no files will be modified]" : "";
            Console.WriteLine($"\nFixing music tags...{modeLabel}");

            var fixLogs = new List<FixLog>();

            try
            {
                var files = Directory.Exists(Constants.NewMusicPath)
                    ? Directory.GetFiles(Constants.NewMusicPath, "*.mp3", SearchOption.AllDirectories)
                    : Array.Empty<string>();

                if (files.Length == 0)
                {
                    Console.WriteLine(" - Nothing to fix!");
                    return;
                }

                foreach (var sourcePath in files)
                {
                    var log = new FixLog { OriginalFilename = Path.GetFileName(sourcePath) };
                    try
                    {
                        // Read current tags
                        TagLib.File tagFile = TagLib.File.Create(sourcePath);
                        var tag = tagFile.Tag;
                        var id3 = (TagLib.Id3v2.Tag)tagFile.GetTag(TagLib.TagTypes.Id3v2, true);

                        string title = tag.Title ?? "";
                        string artists = tag.JoinedPerformers ?? "";
                        string album = tag.Album ?? "";
                        string genres = tag.JoinedGenres ?? "";

                        // AUTO-DELETE: Akira The Don instrumentals (Rule from STAGE_3B)
                        // Delete if: artist = "Akira The Don" AND title contains/ends with "Instrumental"
                        if (artists.IndexOf("Akira The Don", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            (title.IndexOf("Instrumental", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             title.EndsWith("(Instrumental)", StringComparison.OrdinalIgnoreCase)))
                        {
                            if (!dryRun && File.Exists(sourcePath))
                            {
                                File.Delete(sourcePath);
                                Console.WriteLine($"  [AUTO-DELETED] {Path.GetFileName(sourcePath)} (Akira The Don instrumental)");
                            }
                            else if (dryRun)
                            {
                                Console.WriteLine($"  [WOULD AUTO-DELETE] {Path.GetFileName(sourcePath)} (Akira The Don instrumental)");
                            }
                            skippedCount++;
                            continue;
                        }

                        // Apply tag cleanup rules
                        string cleanTitle = RemoveParentheticals(title);
                        string cleanAlbum = RemoveParentheticals(album);
                        var artistList = ExtractAndFixArtists(title, artists);
                        string cleanArtists = string.Join(";", artistList);

                        // Check if any changes needed
                        bool titleChanged = cleanTitle != title;
                        bool albumChanged = cleanAlbum != album;
                        bool artistsChanged = cleanArtists != artists && !string.IsNullOrEmpty(cleanArtists);
                        bool tcmpNeeded = !id3.IsCompilation;
                        bool genreNeeded = ShouldFixGenre(cleanArtists, genres);

                        if (!titleChanged && !albumChanged && !artistsChanged && !tcmpNeeded && !genreNeeded)
                        {
                            log.Status = "skipped";
                            log.Detail = "no fixes needed";
                            fixLogs.Add(log);
                            skippedCount++;
                            continue;
                        }

                        // Record what will change
                        if (titleChanged) log.Changes.Add($"Title: \"{title}\" → \"{cleanTitle}\"");
                        if (albumChanged) log.Changes.Add($"Album: \"{album}\" → \"{cleanAlbum}\"");
                        if (artistsChanged) log.Changes.Add($"Artists: \"{artists}\" → \"{cleanArtists}\"");
                        if (tcmpNeeded) log.Changes.Add("TCMP: False → True");
                        if (genreNeeded)
                        {
                            string newGenre = DetermineGenre(cleanArtists);
                            log.Changes.Add($"Genre: \"{genres}\" → \"{newGenre}\"");
                        }

                        // Apply changes (write tags + rename file)
                        if (!dryRun)
                        {
                            if (titleChanged) tag.Title = cleanTitle;
                            if (albumChanged) tag.Album = cleanAlbum;
                            if (artistsChanged) tag.Performers = artistList.ToArray();
                            if (tcmpNeeded) id3.IsCompilation = true;
                            if (genreNeeded) tag.Genres = new[] { DetermineGenre(cleanArtists) };

                            tagFile.Save();

                            // Rename file: {artists} - {title}.mp3
                            string sanitisedArtists = Reflector.SanitiseFilename(cleanArtists);
                            string sanitisedTitle = Reflector.SanitiseFilename(cleanTitle);
                            string newFilename = $"{sanitisedArtists} - {sanitisedTitle}.mp3";
                            string newPath = Path.Combine(Path.GetDirectoryName(sourcePath), newFilename);

                            if (newPath != sourcePath && !File.Exists(newPath))
                            {
                                File.Move(sourcePath, newPath);
                                log.Changes.Add($"Filename: \"{Path.GetFileName(sourcePath)}\" → \"{newFilename}\"");
                            }

                            log.Filename = newFilename;
                            log.Status = "fixed";
                            fixedCount++;
                        }
                        else
                        {
                            // Dry run: show what would change
                            string sanitisedArtists = Reflector.SanitiseFilename(cleanArtists);
                            string sanitisedTitle = Reflector.SanitiseFilename(cleanTitle);
                            string newFilename = $"{sanitisedArtists} - {sanitisedTitle}.mp3";
                            log.Filename = newFilename;
                            log.Status = "would-fix";
                            fixedCount++;
                        }

                        fixLogs.Add(log);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($" - Skipped '{Path.GetFileName(sourcePath)}': error reading file ({ex.Message})");
                        log.Status = "error";
                        log.Detail = ex.Message;
                        fixLogs.Add(log);
                        skippedCount++;
                    }
                }

                // Summary
                Console.WriteLine();
                Console.WriteLine("============================================================");
                Console.WriteLine(dryRun ? "  Tag Fix Summary (Dry Run)" : "  Tag Fix Summary");
                Console.WriteLine("============================================================");
                Console.WriteLine($"  Files processed: {files.Length}");
                Console.WriteLine($"  Fixed: {fixedCount}  |  Skipped: {skippedCount}");

                // Per-file report
                Console.WriteLine("\n  --- Per-file results ---");
                foreach (var log in fixLogs)
                {
                    if (log.Status == "skipped")
                    {
                        Console.WriteLine($"  [SKIPPED] {log.OriginalFilename} ({log.Detail})");
                    }
                    else if (log.Status == "error")
                    {
                        Console.WriteLine($"  [ERROR] {log.OriginalFilename} ({log.Detail})");
                    }
                    else
                    {
                        string prefix = dryRun ? "[WOULD FIX]" : "[FIXED]";
                        Console.WriteLine($"  {prefix} {log.OriginalFilename}");
                        foreach (var change in log.Changes)
                        {
                            Console.WriteLine($"    - {change}");
                        }
                        if (!string.IsNullOrEmpty(log.Filename) && log.Filename != log.OriginalFilename)
                        {
                            Console.WriteLine($"    - Filename: {log.OriginalFilename} → {log.Filename}");
                        }
                    }
                }

                Console.WriteLine("\n============================================================");
            }
            finally
            {
                FinishAndPrintTimeTaken();
            }
        }

        /// <summary>
        /// Removes entire parenthetical phrases from a string.
        /// Patterns: (feat. X), (ft. X), (Album Version), (Explicit), (Edit), (Radio Edit), (Original), (Remix), (Version)
        /// </summary>
        private string RemoveParentheticals(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            string result = input;
            // Patterns to remove (in order of priority)
            var patterns = new[]
            {
                @"\s*\(feat\.\s+[^)]+\)", // (feat. Artist)
                @"\s*\(ft\.\s+[^)]+\)",   // (ft. Artist)
                @"\s*\(Album\s+Version\)", // (Album Version)
                @"\s*\(Explicit\)",       // (Explicit)
                @"\s*\(Radio\s+Edit\)",  // (Radio Edit)
                @"\s*\(Edit\)",           // (Edit)
                @"\s*\(Original\)",       // (Original)
                @"\s*\(Remix\)",          // (Remix)
                @"\s*\(Version\)",        // (Version)
            };

            foreach (var pattern in patterns)
            {
                result = Regex.Replace(result, pattern, "", RegexOptions.IgnoreCase);
            }

            // Clean up any double spaces left behind
            result = Regex.Replace(result, @"\s+", " ");
            return result.Trim();
        }

        /// <summary>
        /// Extracts featured artists from title parentheticals and combines with existing artists.
        /// Returns a list of artists with primary artist first, others semicolon-separated.
        /// </summary>
        private List<string> ExtractAndFixArtists(string title, string currentArtists)
        {
            var artists = new List<string>();

            // Parse existing artists
            if (!string.IsNullOrEmpty(currentArtists))
            {
                var existing = Track.ProcessProperty(currentArtists).ToList();
                artists.AddRange(existing);
            }

            // Extract featured artists from title
            var featPatterns = new[]
            {
                @"\(feat\.\s+([^)]+)\)",  // (feat. Artist)
                @"\(ft\.\s+([^)]+)\)",    // (ft. Artist)
            };

            foreach (var pattern in featPatterns)
            {
                var matches = Regex.Matches(title, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        string featArtist = match.Groups[1].Value.Trim();
                        if (!artists.Any(a => a.Equals(featArtist, StringComparison.OrdinalIgnoreCase)))
                        {
                            artists.Add(featArtist);
                        }
                    }
                }
            }

            return artists.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Checks if genre needs to be fixed (special handling for Akira The Don and genre "Motivation").
        /// </summary>
        private bool ShouldFixGenre(string artists, string currentGenres)
        {
            if (string.IsNullOrEmpty(artists)) return false;

            // Akira The Don must have Musivation
            if (artists.IndexOf("Akira The Don", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return currentGenres.IndexOf("Musivation", StringComparison.OrdinalIgnoreCase) < 0;
            }

            return false;
        }

        /// <summary>
        /// Determines what genre should be set for a track based on artist/album.
        /// </summary>
        private string DetermineGenre(string artists)
        {
            if (string.IsNullOrEmpty(artists)) return "";

            if (artists.IndexOf("Akira The Don", StringComparison.OrdinalIgnoreCase) >= 0)
                return Constants.MusivDir; // "Musivation"

            return "";
        }
    }
}
