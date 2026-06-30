using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
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

        /// <summary>
        /// Per-file tag changes for display in MusicIntegrator's combined routing block.
        /// Key: filename MusicIntegrator will see (original in dry-run; post-rename in real mode).
        /// Value: list of change strings e.g. "Title: \"X\" -> \"Y\"". Excludes filename renames.
        /// </summary>
        public Dictionary<string, List<string>> FileChanges { get; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

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
                        string cleanAlbum = StripAlbumSuffixes(RemoveParentheticals(album));
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
                        if (titleChanged) log.Changes.Add($"Title: \"{title}\"  -> \"{cleanTitle}\"");
                        if (albumChanged) log.Changes.Add($"Album: \"{album}\"  -> \"{cleanAlbum}\"");
                        if (artistsChanged) log.Changes.Add($"Artists: \"{artists}\"  -> \"{cleanArtists}\"");
                        // TCMP fix is silent - almost always needed, adds noise to output
                        if (genreNeeded)
                        {
                            string newGenre = DetermineGenre(cleanArtists);
                            log.Changes.Add($"Genre: \"{genres}\"  -> \"{newGenre}\"");
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
                            if (string.IsNullOrEmpty(sanitisedArtists) || string.IsNullOrEmpty(sanitisedTitle))
                            {
                                log.Changes.Add("[WARN] Rename skipped: empty artist or title after sanitisation");
                                log.Filename = log.OriginalFilename;
                            }
                            else
                            {
                                string newFilename = $"{sanitisedArtists} - {sanitisedTitle}.mp3";
                                string newPath = Path.Combine(Path.GetDirectoryName(sourcePath), newFilename);
                                if (newPath != sourcePath && !File.Exists(newPath))
                                {
                                    File.Move(sourcePath, newPath);
                                    log.Changes.Add($"Filename: \"{Path.GetFileName(sourcePath)}\"  -> \"{newFilename}\"");
                                }
                                log.Filename = newFilename;
                            }
                            log.Status = "fixed";
                            fixedCount++;
                        }
                        else
                        {
                            // Dry run: show what would change
                            string sanitisedArtists = Reflector.SanitiseFilename(cleanArtists);
                            string sanitisedTitle = Reflector.SanitiseFilename(cleanTitle);
                            if (string.IsNullOrEmpty(sanitisedArtists) || string.IsNullOrEmpty(sanitisedTitle))
                            {
                                log.Changes.Add("[WARN] Rename skipped: empty artist or title after sanitisation");
                                log.Filename = log.OriginalFilename;
                            }
                            else
                            {
                                string newFilename = $"{sanitisedArtists} - {sanitisedTitle}.mp3";
                                log.Filename = newFilename;
                            }
                            log.Status = "would-fix";
                            fixedCount++;
                        }

                        // Register changes for combined display in MusicIntegrator's routing block
                        var nonFilenameChanges = log.Changes.Where(c => !c.StartsWith("Filename:")).ToList();
                        if (nonFilenameChanges.Count > 0)
                        {
                            FileChanges[log.OriginalFilename] = nonFilenameChanges;
                            if (!string.IsNullOrEmpty(log.Filename) && !log.Filename.Equals(log.OriginalFilename, StringComparison.OrdinalIgnoreCase))
                                FileChanges[log.Filename] = nonFilenameChanges;
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

                // Per-file tag changes shown in MusicIntegrator's combined routing block.
                // Only surface errors and the count here.
                var tagErrors = fixLogs.Where(l => l.Status == "error").ToList();
                if (tagErrors.Count > 0)
                {
                    Console.WriteLine($"[ERRORS: {tagErrors.Count}]");
                    foreach (var e in tagErrors) Console.WriteLine($" {e.OriginalFilename}: {e.Detail}");
                }
                Console.WriteLine($"Fixed: {fixedCount}  |  Skipped: {skippedCount}");
            }
            finally
            {
                FinishAndPrintTimeTaken("Tag fixing");
            }
        }

        /// <summary>
        /// Removes entire parenthetical phrases from a string.
        /// Patterns: (feat. X), (ft. X), (Album Version), (Explicit), (Edit), (Radio Edit), (Original), (Remix), (Version)
        /// </summary>
        internal static string RemoveParentheticals(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            string result = input;
            // Patterns to remove (in order of priority)
            var patterns = new[]
            {
                @"\s*\(feat\.\s+[^)]+\)", // (feat. Artist)
                @"\s*\(ft\.\s+[^)]+\)",   // (ft. Artist)
                @"\s*\(with\s+[^)]+\)",   // (with Artist)
                @"\s*\[feat\.\s+[^\]]+\]", // [feat. Artist]
                @"\s*\[ft\.\s+[^\]]+\]",   // [ft. Artist]
                @"\s*\(Album\s+Version\)", // (Album Version)
                @"\s*\(Single\s+Version\)", // (Single Version)
                @"\s*\(Radio\s+Version\)", // (Radio Version) - title field; album field already handled by StripAlbumSuffixes
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
        /// Strips album-folder suffix patterns from an album tag value.
        /// Called after RemoveParentheticals - handles edition markers, remaster tags, version
        /// qualifiers, and year suffixes that folder names inject into album metadata.
        /// NOT applied to title field - only album.
        /// Test cases: "(International Version)" -> "", "(Deluxe Edition)" -> "",
        /// "(2011 Remaster)" -> "", "(Remastered)" -> "", "(Bonus Tracks)" -> "",
        /// "(2019)" -> "", plain album unchanged.
        /// </summary>
        internal static string StripAlbumSuffixes(string album)
        {
            if (string.IsNullOrEmpty(album)) return album;

            string result = album;
            var patterns = new[]
            {
                @"\s*\([^)]*\s+Version\)",                                                   // (International Version), (Radio Version), (Acoustic Version), etc.
                @"\s*\(Deluxe(?:\s+Edition)?\)",                                             // (Deluxe Edition) or (Deluxe)
                @"\s*\((?:Special|Limited|Collector'?s?|Anniversary|Expanded|Extended)\s+Edition\)", // edition markers
                @"\s*\((?:\d{4}\s+)?Remaster(?:ed)?(?:\s+Edition)?\)",                          // (Remastered), (Remaster), (2011 Remaster), (2011 Remastered), (2014 Remastered Edition)
                @"\s*\(Mono\)",                                                              // (Mono)
                @"\s*\(Stereo\)",                                                            // (Stereo)
                @"\s*\(Bonus\s+Tracks?\)",                                                   // (Bonus Track) or (Bonus Tracks)
                @"\s*\((?:19|20)\d{2}\)",                                                    // year suffixes: (2019), (1995), etc.
            };

            foreach (var pattern in patterns)
            {
                result = Regex.Replace(result, pattern, "", RegexOptions.IgnoreCase);
            }

            result = Regex.Replace(result, @"\s+", " ");
            return result.Trim();
        }

        private static Dictionary<string, string> _artistOverrides;

        /// <summary>
        /// Loads artist name overrides from config/artist-name-overrides.xml (once per process).
        /// Keys and values are both the canonical form; lookup is case-insensitive.
        /// </summary>
        private static Dictionary<string, string> GetArtistOverrides()
        {
            if (_artistOverrides != null) return _artistOverrides;
            _artistOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(Constants.ArtistOverridesPath)) return _artistOverrides;
                var doc = new XmlDocument();
                doc.Load(Constants.ArtistOverridesPath);
                foreach (XmlElement el in doc.SelectNodes("//Artist").OfType<XmlElement>())
                {
                    string canonical = el.GetAttribute("canonical");
                    if (!string.IsNullOrEmpty(canonical))
                    {
                        _artistOverrides[canonical] = canonical;
                        // Optional variant attribute maps stylized/Unicode-variant names to the canonical form.
                        // Needed when Unicode diacritics (e.g. JAŸ-Z) differ from the canonical ASCII form (Jay-Z)
                        // and OrdinalIgnoreCase can't match them as equal.
                        string variant = el.GetAttribute("variant");
                        if (!string.IsNullOrEmpty(variant))
                            _artistOverrides[variant] = canonical;
                    }
                }
            }
            catch { /* fallback: apply no overrides */ }
            return _artistOverrides;
        }

        /// <summary>
        /// Extracts featured artists from title parentheticals and combines with existing artists.
        /// Returns a list of artists with primary artist first, others semicolon-separated.
        /// </summary>
        internal static List<string> ExtractAndFixArtists(string title, string currentArtists)
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
                @"\(with\s+([^)]+)\)",    // (with Artist)
                @"\[feat\.\s+([^\]]+)\]", // [feat. Artist]
                @"\[ft\.\s+([^\]]+)\]",   // [ft. Artist]
            };

            foreach (var pattern in featPatterns)
            {
                var matches = Regex.Matches(title, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        // "&" in title parentheticals is always a separator, never part of a name.
                        // Split "A & B" into individual artists so compound forms don't duplicate
                        // their components (e.g. "X & Y" when "X" and "Y" are already present).
                        var featParts = match.Groups[1].Value.Trim()
                            .Split(new[] { " & " }, StringSplitOptions.None)
                            .Select(p => p.Trim())
                            .Where(p => !string.IsNullOrEmpty(p));
                        foreach (var featArtist in featParts)
                        {
                            // Skip exact duplicates
                            if (artists.Any(a => a.Equals(featArtist, StringComparison.OrdinalIgnoreCase)))
                                continue;
                            // Skip "X of BandName" clarifications where BandName is already present.
                            // e.g. "(feat. Macklemore & Patrick Stump of Fall Out Boy)" when "Fall Out Boy"
                            // is already in the artist field - "of BandName" is a descriptor, not a separator.
                            int ofIdx = featArtist.IndexOf(" of ", StringComparison.OrdinalIgnoreCase);
                            if (ofIdx >= 0 && artists.Any(a => a.Equals(featArtist.Substring(ofIdx + 4).Trim(), StringComparison.OrdinalIgnoreCase)))
                                continue;
                            artists.Add(featArtist);
                        }
                    }
                }
            }

            // Fix casing: "scott adams" -> "Scott Adams" (title-cased by default).
            // Exception 1: artists in the overrides config keep their canonical casing (e.g. "mike." stays lowercase).
            // Exception 2: mixed-case names (e.g. "PJ Simas") are preserved as-is.
            //   Applying .ToLower() first would destroy intentional casing in abbreviations and
            //   initial-style names. Only normalize all-uppercase or all-lowercase strings.
            var overrides = GetArtistOverrides();
            var textInfo = System.Globalization.CultureInfo.InvariantCulture.TextInfo;
            return artists
                .Select(a =>
                {
                    if (overrides.TryGetValue(a, out string canonical)) return canonical;
                    bool isMixedCase = a.Any(char.IsUpper) && a.Any(char.IsLower);
                    return textInfo.ToTitleCase(isMixedCase ? a : a.ToLower());
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Checks if genre needs to be fixed.
        /// Musivation artists (ATD, Loot Bryon Smith): genre must be "Musivation".
        /// Generic Motivation tracks: any genre containing "Motivation" is normalized to exactly "Motivation".
        /// </summary>
        internal static bool ShouldFixGenre(string artists, string currentGenres)
        {
            if (string.IsNullOrEmpty(artists)) return false;

            // Musivation artists must have Musivation genre
            if (artists.IndexOf("Akira The Don", StringComparison.OrdinalIgnoreCase) >= 0 ||
                artists.IndexOf("Loot Bryon Smith", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return currentGenres.IndexOf("Musivation", StringComparison.OrdinalIgnoreCase) < 0;
            }

            // Generic Motivation: normalize non-exact "Motivation" genre variants (e.g. "Motivational")
            if (currentGenres.IndexOf("Motivation", StringComparison.OrdinalIgnoreCase) >= 0 &&
                currentGenres.IndexOf("Musivation", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return !currentGenres.Equals("Motivation", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>
        /// Determines what genre should be set. Only called when ShouldFixGenre returns true.
        /// Musivation artists -> "Musivation"; all other cases -> "Motivation" (only other fix case).
        /// </summary>
        internal static string DetermineGenre(string artists)
        {
            if (string.IsNullOrEmpty(artists)) return "";

            if (artists.IndexOf("Akira The Don", StringComparison.OrdinalIgnoreCase) >= 0 ||
                artists.IndexOf("Loot Bryon Smith", StringComparison.OrdinalIgnoreCase) >= 0)
                return Constants.MusivDir; // "Musivation"

            return Constants.MotivDir; // "Motivation" - the only other case ShouldFixGenre triggers for
        }
    }
}
