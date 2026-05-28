using AudioManager.Code.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using File = System.IO.File;
using TagLib;

namespace AudioManager
{
    /// <summary>
    /// Scans the NewMusic folder, validates files, and sorts them into the Audio library.
    /// </summary>
    internal class MusicIntegrator : Doer
    {
        private bool dryRun;
        private bool noInput;
        private Dictionary<string, List<string>> _miscMigrationCandidates = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        // Populated by RunScanAhead; used by GetDestDir to build full routing reason for Misc fallback
        private Dictionary<string, int> _scanAheadBatchCounts;
        private Dictionary<string, int> _scanAheadMiscCounts;
        private Dictionary<string, int> _scanAheadSourcesCounts;
        private string _libraryPath;

        internal enum RoutingConfidence
        {
            Certain,   // Known artist folder; genre override; ATD routing. Auto-route, no prompt.
            Likely,    // Scan-ahead new artist folder; route is reasonable but folder is new. Auto-route, no prompt.
            Uncertain  // Reserved. Previously used for Misc fallback (Y/N prompt). Misc now routes as Certain.
        }

        /// <summary>Per-file integration result for the log.</summary>
        private class LogEntry
        {
            public string Filename;
            public string Title;
            public string Artists;
            public string Album;
            public string Destination;
            public List<string> TagChanges = new List<string>();
            public string Status; // "moved", "would-move", "skipped", "error"
            public string Detail; // reason or error message
        }

        /// <summary>Data about a duplicate found during pre-scan.</summary>
        private class DupData
        {
            public string DuplicatePath;
            public string LibraryFilePath;
            public string RelLibraryPath;
            public string RelMirrorPath;
            public string RelNewPath;
            public string DisplayNewFilename;
            public string MirrorTrack;
            public string MirrorAlbum;
            public string DupReason;
            public char RecommendedKey; // 'D', 'L', or '\0'
            public string OptionsLine;
            public char Decision; // set by PresentDuplicateAndDecide: 'D', 'L', 'K', 'Q'
            public bool SkipRouting; // true for D-decided and dry-run L-decided duplicates
        }

        /// <summary>A file pre-scanned from NewMusic with its cleaned tags and duplicate data.</summary>
        private class ScannedFile
        {
            public string SourcePath;
            public Track Track;
            public bool IsReadable;
            public string ReadError;
            public Exception ReadException;
            public LogEntry LogEntry;
            public DupData Duplicate; // null if no duplicate found
            public bool InBatchDuplicate; // true if another file in the batch shares normalised artist+title
        }

        /// <summary>
        /// Construct and run the music integrator
        /// </summary>
        /// <param name="dryRun">If true, print planned actions without executing any file moves.</param>
        /// <param name="noInput">If true, skip all interactive prompts and auto-accept recommended decisions.</param>
        public MusicIntegrator(bool dryRun = false, bool noInput = false)
        {
            this.dryRun = dryRun;
            this.noInput = noInput;
            _libraryPath = Constants.AudioFolderPath;
            string modeLabel = dryRun ? " [DRY RUN - no files will be moved]" : "";
            Console.WriteLine($"\nIntegrating new music...{modeLabel}");

            int movedCount = 0;
            int skippedCount = 0;
            var logEntries = new List<LogEntry>();
            int totalFiles = 0;

            try
            {
                // Step 1: Fix tags in NewMusic folder
                var tagFixer = new TagFixer(dryRun);
                var tagChanges = tagFixer.FileChanges;

                var files = Directory.Exists(Constants.NewMusicPath)
                    ? Directory.GetFiles(Constants.NewMusicPath, "*.mp3", SearchOption.AllDirectories)
                    : Array.Empty<string>();

                totalFiles = files.Length;

                if (files.Length == 0)
                {
                    Console.WriteLine(" - Nothing to integrate!");
                    return;
                }

                // Scan-ahead: identify artists that will hit 3+ threshold
                var newArtistFolders = RunScanAhead(files);

                // Pre-scan all files: read + clean tags, find duplicates (no UI)
                var scannedFiles = PreScanFiles(files, tagChanges);

                // Flag in-batch duplicates (same normalised artist+title appearing twice in NewMusic)
                MarkInBatchDuplicates(scannedFiles);

                // Step 2: Batch duplicate review - all duplicates presented together before routing
                var duplicateFiles = scannedFiles.Where(sf => sf.Duplicate != null).ToList();
                if (duplicateFiles.Count > 0)
                {
                    Console.WriteLine($"\nReviewing {duplicateFiles.Count} duplicate(s)...");
                    foreach (var sf in duplicateFiles)
                    {
                        if (!PresentDuplicateAndDecide(sf))
                        {
                            sf.LogEntry.Status = "quit";
                            logEntries.Add(sf.LogEntry);
                            return;
                        }
                    }
                }

                // Step 3: Route files (all duplicate decisions already made)
                int routeableCount = scannedFiles.Count(sf => sf.IsReadable && sf.Duplicate?.SkipRouting != true);
                Console.WriteLine("\n===========================================================================");
                Console.WriteLine(dryRun ? "Routing (Dry Run)" : "Routing");
                Console.WriteLine("===========================================================================");
                Console.WriteLine($"Files in batch: {routeableCount}");

                // Dry-run: pre-compute route distribution and show before per-file output
                // Lets user spot anomalies (e.g. unexpectedly high Misc count) before reading 100 lines
                if (dryRun && routeableCount > 1)
                {
                    var routeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var sf in scannedFiles.Where(s => s.IsReadable && s.Duplicate?.SkipRouting != true))
                    {
                        string preDestDir = GetDestDir(sf.Track, newArtistFolders, out string preReason, out RoutingConfidence preConf);
                        string cat = GetRouteCategory(preDestDir);
                        routeCounts[cat] = routeCounts.ContainsKey(cat) ? routeCounts[cat] + 1 : 1;
                    }
                    var summaryParts = routeCounts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}: {kv.Value}");
                    Console.WriteLine($"Routes: {string.Join("  |  ", summaryParts)}");
                }
                Console.WriteLine();

                // 3a: Execute all duplicate decisions together so their outputs are grouped
                //     before routing output begins. D/L outputs appear consecutively here;
                //     K files produce no output and are routed normally in 3b.
                foreach (var sf in scannedFiles.Where(sf2 => sf2.Duplicate != null))
                {
                    var entry = sf.LogEntry;
                    var dup = sf.Duplicate;
                    Track track = sf.Track;
                    char decision = dup.Decision;

                    // Safety net: Q in Step 2 already exits the constructor, but guard anyway
                    if (decision == 'Q')
                    {
                        entry.Status = "quit";
                        logEntries.Add(entry);
                        return;
                    }

                    if (decision == 'D')
                    {
                        if (dryRun)
                        {
                            Console.WriteLine($"[DRY RUN] Would delete from NewMusic: {dup.RelNewPath}");
                            entry.Status = "would-delete";
                            entry.Detail = "duplicate (would delete)";
                        }
                        else
                        {
                            File.Delete(sf.SourcePath);
                            Console.WriteLine($"  Deleted from NewMusic: {dup.RelNewPath}");
                            entry.Status = "deleted";
                            entry.Detail = "duplicate (deleted)";
                        }
                        logEntries.Add(entry); skippedCount++;
                        Console.WriteLine();
                        dup.SkipRouting = true;
                    }
                    else if (decision == 'L')
                    {
                        if (dryRun)
                        {
                            Console.WriteLine($"[DRY RUN] Would delete from library: {dup.RelLibraryPath}");
                            Console.WriteLine($"[DRY RUN] Would keep new file: {dup.DisplayNewFilename}");
                            entry.Status = "would-replace";
                            entry.Detail = "duplicate (would replace)";
                            logEntries.Add(entry); skippedCount++;
                            Console.WriteLine();
                            dup.SkipRouting = true;
                        }
                        else
                        {
                            if (File.Exists(dup.LibraryFilePath))
                            {
                                File.Delete(dup.LibraryFilePath);
                                Console.WriteLine($"  Deleted from library: {dup.RelLibraryPath}");
                                Console.WriteLine($"  Integrating replacement: {dup.RelNewPath}");
                                entry.Detail = "duplicate (library replaced)";
                                Console.WriteLine();
                                // SkipRouting stays false: new file is routed in 3b
                            }
                            else
                            {
                                Console.WriteLine($"  [WARN] Library file not found: {dup.RelLibraryPath}");
                                entry.Status = "error";
                                entry.Detail = "duplicate (library file not found)";
                                logEntries.Add(entry); skippedCount++;
                                Console.WriteLine();
                                dup.SkipRouting = true;
                            }
                        }
                    }
                    // K: no output in 3a; routed normally in 3b
                }

                // 3b: Route all files. D-decided and dry-run L-decided duplicates already handled
                //     above; real-mode L files and K files fall through to routing here.
                // In dry-run: collect outputs and print sorted by destination path after the loop.
                var dryRunRoutingOutputs = dryRun ? new List<(string destPath, string block)>() : null;
                var routingStopwatch = System.Diagnostics.Stopwatch.StartNew();
                foreach (var sf in scannedFiles)
                {
                    var entry = sf.LogEntry;

                    if (!sf.IsReadable)
                    {
                        Console.WriteLine();
                        Console.WriteLine("===========================================================================");
                        Console.WriteLine("INTEGRATION FAILED");
                        Console.WriteLine("===========================================================================");
                        Console.WriteLine();
                        Console.WriteLine($"Error processing file: {Path.GetFileName(sf.SourcePath)}");
                        Console.WriteLine($"Full path: {sf.SourcePath}");
                        Console.WriteLine();
                        Console.WriteLine($"Error details: {sf.ReadError}");
                        if (sf.ReadException != null && !string.IsNullOrEmpty(sf.ReadException.StackTrace))
                            Console.WriteLine($"\nStack trace:\n{sf.ReadException.StackTrace}");
                        Console.WriteLine("\n===========================================================================");
                        Console.WriteLine("\nIntegration halted. Please fix the error above and retry.");
                        if (!noInput) { Console.WriteLine("Press any key to exit..."); Console.ReadKey(); }
                        throw new InvalidOperationException($"Integration error on file '{Path.GetFileName(sf.SourcePath)}': {sf.ReadError}", sf.ReadException);
                    }

                    // Skip files fully resolved during duplicate execution (3a)
                    if (sf.Duplicate?.SkipRouting == true) continue;

                    try
                    {
                        Track track = sf.Track;
                        string primaryArtist = track.PrimaryArtist;

                        // Skip if un-routable (tags are already clean from TagFixer)
                        if (track.Title.Equals("Missing") || track.Artists.Equals("Missing") || primaryArtist.Equals("Missing"))
                        {
                            Console.WriteLine($"- Skipped '{Path.GetFileName(sf.SourcePath)}': missing required tag");
                            entry.Status = "skipped"; entry.Detail = "missing required tag";
                            logEntries.Add(entry); skippedCount++;
                            continue;
                        }

                        // Build destination filename
                        string sanitisedArtists = Reflector.SanitiseFilename(track.Artists);
                        string sanitisedTitle = Reflector.SanitiseFilename(track.Title);
                        string destFilename = sanitisedArtists + " - " + sanitisedTitle + ".mp3";

                        // Determine destination directory, reason, and routing confidence
                        string destDir = GetDestDir(track, newArtistFolders, out string reason, out RoutingConfidence confidence);

                        string destPath = Path.Combine(destDir, destFilename);

                        // Compute relative destination path for display
                        string relativeDest = destPath;
                        if (destPath.StartsWith(Constants.AudioFolderPath, StringComparison.OrdinalIgnoreCase))
                        {
                            int startIndex = Constants.AudioFolderPath.Length;
                            if (startIndex <= destPath.Length)
                            {
                                relativeDest = destPath.Substring(startIndex);
                                if (relativeDest.StartsWith("\\") || relativeDest.StartsWith("/"))
                                    relativeDest = relativeDest.Substring(1);
                            }
                        }

                        entry.Destination = relativeDest;
                        string routeSummary = GetRouteSummary(relativeDest);

                        if (confidence != RoutingConfidence.Uncertain)
                        {
                            // Auto-route: no confirmation prompt for Certain/Likely routes
                            string autoLabel = confidence == RoutingConfidence.Certain ? "AUTO" : "AUTO (likely)";

                            if (!dryRun && File.Exists(destPath))
                            {
                                Console.WriteLine($"[SKIP] {track.Artists} - {track.Title}: already exists at destination");
                                Console.WriteLine();
                                entry.Status = "skipped"; entry.Detail = "already exists at destination";
                                logEntries.Add(entry); skippedCount++;
                            }
                            else if (dryRun)
                            {
                                var sb = new StringBuilder();
                                if (sf.InBatchDuplicate)
                                    sb.AppendLine($"[WARN: IN-BATCH DUPLICATE] {Path.GetFileName(sf.SourcePath)}");
                                sb.AppendLine($"[{autoLabel}] {track.Artists} - {track.Title}");
                                foreach (var change in entry.TagChanges)
                                    sb.AppendLine($" > {change}");
                                sb.AppendLine($" Route: {routeSummary}");
                                sb.AppendLine($" Reason: {reason}");
                                sb.AppendLine($" Path: {relativeDest}");
                                sb.AppendLine();
                                dryRunRoutingOutputs.Add((relativeDest, sb.ToString()));
                                entry.Status = "would-move";
                                logEntries.Add(entry); movedCount++;
                            }
                            else
                            {
                                if (sf.InBatchDuplicate)
                                    Console.WriteLine($"[WARN: IN-BATCH DUPLICATE] {Path.GetFileName(sf.SourcePath)}");
                                Directory.CreateDirectory(destDir);
                                File.Move(sf.SourcePath, destPath);
                                movedCount++;
                                Console.WriteLine($"[{autoLabel}] {track.Artists} - {track.Title}");
                                foreach (var change in entry.TagChanges)
                                    Console.WriteLine($" > {change}");
                                Console.WriteLine($" Route: {routeSummary}");
                                Console.WriteLine($" Reason: {reason}");
                                Console.WriteLine($" Path: {relativeDest}");
                                Console.WriteLine();
                                entry.Status = "moved";
                                logEntries.Add(entry);
                            }
                        }
                        else
                        {
                            // Uncertain: show full track info and require confirmation
                            Console.WriteLine();
                            Console.WriteLine("===========================================================================");
                            Console.WriteLine($"{track.Artists} - {track.Title}");
                            Console.WriteLine("===========================================================================");
                            Console.WriteLine();
                            Console.WriteLine($"Album:   {track.Album}");
                            Console.WriteLine($"Year:    {track.Year}");
                            Console.WriteLine($"Genres:  {track.Genres}");
                            if (entry.TagChanges.Count > 0)
                            {
                                Console.WriteLine();
                                Console.WriteLine("Tags fixed:");
                                foreach (var change in entry.TagChanges)
                                    Console.WriteLine($" > {change}");
                            }
                            Console.WriteLine();
                            Console.WriteLine($"Proposed: {routeSummary}");
                            Console.WriteLine($"Path:     {relativeDest}");
                            Console.WriteLine($"Reason:   {reason}");
                            Console.WriteLine();
                            Console.WriteLine("---------------------------------------------------------------------------");
                            Console.WriteLine("[Y] Accept   [N] Decline   [Q] Quit");
                            Console.WriteLine("---------------------------------------------------------------------------");

                            if (noInput)
                            {
                                // Uncertain routes are dead code (Misc routes as Certain since 2026-05-25)
                                // but guard anyway: auto-accept in non-interactive mode
                                Console.WriteLine($"  [AUTO] Y (auto-accepted proposed route)");
                                Console.WriteLine();
                                if (dryRun) { entry.Status = "would-move"; logEntries.Add(entry); movedCount++; }
                                else { Directory.CreateDirectory(destDir); File.Move(sf.SourcePath, destPath); movedCount++; entry.Status = "moved"; logEntries.Add(entry); }
                                continue;
                            }

                            while (true)
                            {
                                var key = ReadMenuKey();
                                if (key == ConsoleKey.Y)
                                {
                                    if (!dryRun && File.Exists(destPath))
                                    {
                                        Console.WriteLine($"[SKIP] '{Path.GetFileName(sf.SourcePath)}': already exists at destination");
                                        entry.Status = "skipped"; entry.Detail = "already exists at destination";
                                        logEntries.Add(entry); skippedCount++;
                                    }
                                    else if (dryRun)
                                    {
                                        Console.WriteLine($"[DRY RUN] Would move to: {relativeDest}");
                                        entry.Status = "would-move";
                                        logEntries.Add(entry); movedCount++;
                                    }
                                    else
                                    {
                                        Directory.CreateDirectory(destDir);
                                        File.Move(sf.SourcePath, destPath);
                                        movedCount++;
                                        Console.WriteLine($"Moved to: {relativeDest}");
                                        Console.WriteLine();
                                        entry.Status = "moved";
                                        logEntries.Add(entry);
                                    }
                                    break;
                                }
                                else if (key == ConsoleKey.Q)
                                {
                                    Console.WriteLine("\n - Quit. Remaining files left for next run.");
                                    entry.Status = "quit"; logEntries.Add(entry);
                                    return;
                                }
                                else if (key == ConsoleKey.N)
                                {
                                    if (dryRun)
                                    {
                                        Console.WriteLine("[DRY RUN] Would decline (leave in NewMusic)");
                                        entry.Status = "would-decline";
                                        entry.Detail = "user declined";
                                        logEntries.Add(entry); skippedCount++;
                                    }
                                    else
                                    {
                                        Console.WriteLine("Declined. File left in NewMusic for next run.");
                                        entry.Status = "declined";
                                        entry.Detail = "user declined";
                                        logEntries.Add(entry); skippedCount++;
                                    }
                                    Console.WriteLine();
                                    break;
                                }
                                // ignore other keys
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine();
                        Console.WriteLine("===========================================================================");
                        Console.WriteLine("INTEGRATION FAILED");
                        Console.WriteLine("===========================================================================");
                        Console.WriteLine();
                        Console.WriteLine($"Error processing file: {Path.GetFileName(sf.SourcePath)}");
                        Console.WriteLine($"Full path: {sf.SourcePath}");
                        Console.WriteLine();
                        Console.WriteLine($"Error details: {ex.Message}");
                        if (!string.IsNullOrEmpty(ex.StackTrace))
                            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
                        Console.WriteLine("\n===========================================================================");
                        Console.WriteLine("\nIntegration halted. Please fix the error above and retry.");
                        if (!noInput) { Console.WriteLine("Press any key to exit..."); Console.ReadKey(); }
                        throw new InvalidOperationException($"Integration error on file '{Path.GetFileName(sf.SourcePath)}': {ex.Message}", ex);
                    }
                }

                routingStopwatch.Stop();

                // Print dry-run routing outputs sorted by destination path
                if (dryRun && dryRunRoutingOutputs != null)
                {
                    foreach (var (_, block) in dryRunRoutingOutputs.OrderBy(x => x.destPath, StringComparer.OrdinalIgnoreCase))
                        Console.Write(block);
                }

                // Routing section footer
                var routingErrors = logEntries.Where(e => e.Status == "error").ToList();
                if (routingErrors.Count > 0)
                {
                    Console.WriteLine($"[ERRORS: {routingErrors.Count}]");
                    foreach (var e in routingErrors) Console.WriteLine($" {e.Filename}: {e.Detail}");
                    Console.WriteLine();
                }
                Console.WriteLine(dryRun ? $"Routed: {movedCount}  |  Skipped: {skippedCount}" : $"Moved: {movedCount}  |  Skipped: {skippedCount}");
                Console.WriteLine();
                Console.WriteLine($"Routing - time taken: {Doer.ConvertTimeSpanToString(routingStopwatch.Elapsed)}");

                if (!dryRun)
                    PrintConfidenceReport(logEntries, totalFiles, movedCount, skippedCount);

                RunMiscMigration();

                Console.WriteLine("\n============================================================");
                Console.WriteLine("Finished");
                Console.WriteLine("============================================================");
            }
            finally
            {
                Finished(); // records time for Doer.PrintTotalTimeTaken(), no separate console output
            }
        }

        /// <summary>Test-only constructor. Does not run the integration pipeline.</summary>
        internal MusicIntegrator(string testLibraryPath)
        {
            _libraryPath = testLibraryPath;
            _scanAheadBatchCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _scanAheadMiscCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _scanAheadSourcesCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Pre-scans the batch to find artists that will hit the 3-song threshold.
        /// Checks existing Misc songs in AudioMirror XML + batch count.
        /// Returns a set of primary artist names that need a new Artists/ folder.
        /// Prints a preview of what will happen for those artists.
        /// </summary>
        private HashSet<string> RunScanAhead(string[] batchFiles)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Console.Write($"\nScan-ahead: reading {batchFiles.Length} file(s)...");

            // Count artists in the incoming batch
            var batchCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in batchFiles)
            {
                try
                {
                    using (TagLib.File tf = TagLib.File.Create(f))
                    {
                        string artists = tf.Tag.JoinedPerformers;
                        if (string.IsNullOrEmpty(artists)) continue;
                        string primary = Track.ProcessProperty(artists)[0].Trim();
                        if (string.IsNullOrEmpty(primary)) continue;
                        batchCounts[primary] = batchCounts.ContainsKey(primary) ? batchCounts[primary] + 1 : 1;
                    }
                }
                catch { /* skip unreadable files */ }
            }

            // Count existing Misc songs by artist from AudioMirror XML
            var miscCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var miscFilesByArtist = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            string mirrorMiscPath = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, Constants.MirrorFolderPath, Constants.MiscDir));
            if (Directory.Exists(mirrorMiscPath))
            {
                var miscXmlFiles = Directory.GetFiles(mirrorMiscPath, "*.xml");
                Console.Write($" checking Misc ({miscXmlFiles.Length})...");
                foreach (var xmlFile in miscXmlFiles)
                {
                    try
                    {
                        var xmlDoc = new System.Xml.XmlDocument();
                        xmlDoc.Load(xmlFile);
                        var artistsEl = xmlDoc.SelectSingleNode("//Artists");
                        if (artistsEl == null) continue;
                        string primary = Track.ProcessProperty(artistsEl.InnerText)[0].Trim();
                        if (string.IsNullOrEmpty(primary)) continue;
                        miscCounts[primary] = miscCounts.ContainsKey(primary) ? miscCounts[primary] + 1 : 1;
                        if (!miscFilesByArtist.ContainsKey(primary))
                            miscFilesByArtist[primary] = new List<string>();
                        miscFilesByArtist[primary].Add(xmlFile);
                    }
                    catch { /* skip malformed XML */ }
                }
            }

            // Count existing Sources/ songs by artist (Films, Shows, Anime) from AudioMirror XML.
            // Sources/ tracks count toward the 3-song threshold but are NOT migrated - they stay
            // in Sources/ regardless of whether the artist gets a new Artists/ folder.
            var sourcesCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string mirrorSourcesPath = Path.Combine(Constants.MirrorFolderPath, Constants.SourcesDir);
            if (Directory.Exists(mirrorSourcesPath))
            {
                var sourcesXmlFiles = Directory.GetFiles(mirrorSourcesPath, "*.xml", SearchOption.AllDirectories);
                Console.Write($" checking Sources ({sourcesXmlFiles.Length})...");
                foreach (var xmlFile in sourcesXmlFiles)
                {
                    try
                    {
                        var xmlDoc = new System.Xml.XmlDocument();
                        xmlDoc.Load(xmlFile);
                        var artistsEl = xmlDoc.SelectSingleNode("//Artists");
                        if (artistsEl == null) continue;
                        string primary = Track.ProcessProperty(artistsEl.InnerText)[0].Trim();
                        if (string.IsNullOrEmpty(primary)) continue;
                        sourcesCounts[primary] = sourcesCounts.ContainsKey(primary) ? sourcesCounts[primary] + 1 : 1;
                    }
                    catch { /* skip malformed XML */ }
                }
            }
            Console.WriteLine(" done.");

            // Find artists that will hit 3+ threshold and don't already have an Artists/ folder
            var previewLines = new List<string>();
            foreach (var kvp in batchCounts)
            {
                string artist = kvp.Key;
                int batchCount = kvp.Value;
                int miscCount = miscCounts.ContainsKey(artist) ? miscCounts[artist] : 0;
                int sourcesCount = sourcesCounts.ContainsKey(artist) ? sourcesCounts[artist] : 0;
                int total = batchCount + miscCount + sourcesCount;

                string artistFolder = Path.Combine(Constants.AudioFolderPath, Constants.ArtistsDir, SanitiseFolderName(artist));
                string musivArtistFolder = Path.Combine(Constants.AudioFolderPath, Constants.MusivDir, SanitiseFolderName(artist));
                bool hasExistingFolder = Directory.Exists(artistFolder) || Directory.Exists(musivArtistFolder);
                if (total >= 3 && !hasExistingFolder)
                {
                    result.Add(artist);
                    string note;
                    if (miscCount > 0 && sourcesCount > 0)
                        note = $"{batchCount} in batch + {miscCount} in Misc + {sourcesCount} in Sources = {total} total -> new Artists/{artist}/";
                    else if (miscCount > 0)
                        note = $"{batchCount} in batch + {miscCount} in Misc = {total} total -> new Artists/{artist}/";
                    else if (sourcesCount > 0)
                        note = $"{batchCount} in batch + {sourcesCount} in Sources = {total} total -> new Artists/{artist}/";
                    else
                        note = $"{batchCount} in batch = {total} total -> new Artists/{artist}/";
                    if (miscCount > 0)
                        note += $" ({miscCount} existing Misc song(s) will be auto-migrated)";
                    previewLines.Add($"  - {note}");
                }
            }

            if (result.Count > 0)
            {
                Console.WriteLine($"\nScan-ahead: {result.Count} artist(s) will hit 3-song threshold:");
                foreach (var line in previewLines) Console.WriteLine(line);
                Console.WriteLine();
            }

            // Track Misc XMLs for promoted artists so RunMiscMigration can move them post-routing
            foreach (var artist in result)
            {
                if (miscFilesByArtist.TryGetValue(artist, out var xmlPaths) && xmlPaths.Count > 0)
                    _miscMigrationCandidates[artist] = xmlPaths;
            }

            // Store counts so GetDestDir can build a full routing reason for Misc fallback
            _scanAheadBatchCounts = batchCounts;
            _scanAheadMiscCounts = miscCounts;
            _scanAheadSourcesCounts = sourcesCounts;

            return result;
        }

        /// <summary>
        /// Migrates existing Misc songs to Artists/{artist}/Singles/ for artists promoted this batch.
        /// Runs in both dry-run (shows what would happen) and real mode (moves files).
        /// No-op if no artists were promoted with existing Misc songs.
        /// </summary>
        private void RunMiscMigration()
        {
            if (_miscMigrationCandidates.Count == 0) return;

            Console.WriteLine("\n===========================================================================");
            Console.WriteLine(dryRun ? "Misc Migration (Dry Run)" : "Misc Migration");
            Console.WriteLine("===========================================================================");

            int totalCount = 0;
            foreach (var kvp in _miscMigrationCandidates)
            {
                string artist = kvp.Key;
                var xmlPaths = kvp.Value;
                string destFolder = Path.Combine(Constants.AudioFolderPath, Constants.ArtistsDir, SanitiseFolderName(artist), Constants.SinglesDir);
                string relDest = $"Artists\\{SanitiseFolderName(artist)}\\{Constants.SinglesDir}";

                Console.WriteLine($"\n  {artist}: {xmlPaths.Count} song(s) -> {relDest}");

                foreach (var xmlPath in xmlPaths)
                {
                    string libPath = DeriveLibraryPathFromMirrorPath(xmlPath);
                    string fileName = Path.GetFileName(libPath);

                    if (!File.Exists(libPath))
                    {
                        Console.WriteLine($"  [WARN] Not found in library (stale mirror?): {fileName}");
                        continue;
                    }

                    if (dryRun)
                    {
                        Console.WriteLine($"[DRY RUN] Would move: {fileName}");
                        totalCount++;
                    }
                    else
                    {
                        Directory.CreateDirectory(destFolder);
                        File.Move(libPath, Path.Combine(destFolder, fileName));
                        Console.WriteLine($"  Moved: {fileName}");
                        totalCount++;
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine(dryRun ? $"Would migrate: {totalCount} Misc song(s)" : $"Migrated: {totalCount} Misc song(s)");
        }

        /// <summary>
        /// Prints a confidence report to the console after integration.
        /// Covers: count check, per-file table, new folders, destination sanity check, errors.
        /// </summary>
        private void PrintConfidenceReport(List<LogEntry> entries, int totalFiles, int movedCount, int skippedCount)
        {
            Console.WriteLine("\n===========================================================================");
            Console.WriteLine(dryRun ? "  CONFIDENCE REPORT (Dry Run)" : "  CONFIDENCE REPORT");
            Console.WriteLine("===========================================================================\n");

            // 1. Count check
            int expectedMoved = totalFiles - skippedCount;
            bool countOk = dryRun || (movedCount == expectedMoved);
            string countLine = $"  Files in NewMusic: {totalFiles}  |  Moved: {movedCount}  |  Skipped: {skippedCount}";
            Console.WriteLine(countLine);
            if (!countOk)
                Console.WriteLine($"  [ERROR] Count mismatch! Expected {expectedMoved} moved, got {movedCount}.");

            // 2. Per-file table
            foreach (var e in entries)
            {
                string tagNote = e.TagChanges?.Count > 0 ? $" | Tags: {string.Join(", ", e.TagChanges)}" : "";
                string destNote = !string.IsNullOrEmpty(e.Destination) ? $"\n    -> {e.Destination}" : "";
                string detailNote = !string.IsNullOrEmpty(e.Detail) ? $" ({e.Detail})" : "";
                Console.WriteLine($"  [{e.Status?.ToUpper()}] {e.Filename}{tagNote}{detailNote}{destNote}");
            }

            // 3. New folders created
            if (!dryRun)
            {
                var movedEntries = entries.Where(e => e.Status == "moved" && !string.IsNullOrEmpty(e.Destination)).ToList();
                var newFolders = new HashSet<string>();
                foreach (var e in movedEntries)
                {
                    string destFolder = Path.GetDirectoryName(e.Destination);
                    if (!string.IsNullOrEmpty(destFolder))
                    {
                        string fullDir = Path.Combine(Constants.AudioFolderPath, destFolder);
                        if (Directory.Exists(fullDir) && Directory.GetFiles(fullDir).Length == 1)
                            newFolders.Add(destFolder);
                    }
                }
                if (newFolders.Count > 0)
                {
                    Console.WriteLine("\n  --- New folders created ---");
                    foreach (var f in newFolders) Console.WriteLine($"  + {f}");
                }
            }

            // 4. Destination sanity check (re-read each moved file)
            if (!dryRun)
            {
                var failedSanity = new List<string>();
                foreach (var e in entries.Where(en => en.Status == "moved"))
                {
                    if (string.IsNullOrEmpty(e.Destination)) continue;
                    string fullPath = Path.Combine(Constants.AudioFolderPath, e.Destination);
                    if (!File.Exists(fullPath))
                    {
                        failedSanity.Add($"  [MISSING] {e.Destination}");
                        continue;
                    }
                    try
                    {
                        using (TagLib.File tf = TagLib.File.Create(fullPath))
                        {
                            // Readable = OK
                        }
                    }
                    catch
                    {
                        failedSanity.Add($"  [UNREADABLE] {e.Destination}");
                    }
                }
                if (failedSanity.Count > 0)
                {
                    Console.WriteLine("\n  [ERROR] Destination sanity check FAILED:");
                    foreach (var f in failedSanity) Console.WriteLine(f);
                }
                else if (movedCount > 0)
                {
                    Console.WriteLine($"\n  Sanity check: all {movedCount} moved file(s) exist and are readable.");
                }
            }

            // 5. Error summary
            var errors = entries.Where(e => e.Status == "error").ToList();
            if (errors.Count > 0)
            {
                Console.WriteLine($"[ERRORS: {errors.Count}]");
                foreach (var e in errors) Console.WriteLine($"- {e.Filename}: {e.Detail}");
            }
            else
            {
                Console.WriteLine("No errors.");
            }

            Console.WriteLine("\n===========================================================================");
        }

        /// <summary>
        /// Builds an in-memory index of all AudioMirror XMLs: normalised "primary\0title" -> xmlPath.
        /// Called once before PreScanFiles so duplicate detection is O(1) per file instead of O(mirror_size).
        /// Prints a progress message so the user sees activity during what was previously a silent hang.
        /// </summary>
        private Dictionary<string, string> BuildMirrorIndex()
        {
            var index = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!Directory.Exists(Constants.MirrorFolderPath)) return index;

            var xmlFiles = Directory.GetFiles(Constants.MirrorFolderPath, "*.xml", SearchOption.AllDirectories);
            Console.Write($"\nIndexing AudioMirror for duplicates ({xmlFiles.Length} tracks)...");

            foreach (var xmlFile in xmlFiles)
            {
                try
                {
                    var xmlDoc = new System.Xml.XmlDocument();
                    xmlDoc.Load(xmlFile);
                    var artistsEl = xmlDoc.SelectSingleNode("//Artists");
                    var titleEl = xmlDoc.SelectSingleNode("//Title");
                    if (artistsEl == null || titleEl == null) continue;
                    string primary = Track.ProcessProperty(artistsEl.InnerText)[0].Trim();
                    string title = titleEl.InnerText.Trim();
                    if (string.IsNullOrEmpty(primary) || string.IsNullOrEmpty(title)) continue;
                    string key = primary.ToLowerInvariant() + "\0" + title.ToLowerInvariant();
                    if (!index.ContainsKey(key))
                        index[key] = xmlFile;
                }
                catch { /* skip malformed XML */ }
            }

            Console.WriteLine(" done.");
            return index;
        }

        /// <summary>
        /// Looks up an existing track in the pre-built mirror index by primary artist + title (case-insensitive).
        /// Returns the matching XML path, or null if not found.
        /// </summary>
        private string FindDuplicateInMirror(Track track, Dictionary<string, string> mirrorIndex)
        {
            string primaryArtist = track.PrimaryArtist;
            if (string.IsNullOrEmpty(primaryArtist) || primaryArtist.Equals("Missing")) return null;
            string title = track.Title;
            if (string.IsNullOrEmpty(title) || title.Equals("Missing")) return null;
            string key = primaryArtist.ToLowerInvariant() + "\0" + title.ToLowerInvariant();
            return mirrorIndex.TryGetValue(key, out string xmlPath) ? xmlPath : null;
        }

        /// <summary>
        /// Derives the actual library file path from an AudioMirror XML path.
        /// AudioMirror: C:\...\AudioMirror\AUDIO_MIRROR\Artist\Album\Track.xml
        /// Library:     C:\Users\David\Audio\Artist\Album\Track.mp3
        /// </summary>
        private string DeriveLibraryPathFromMirrorPath(string mirrorXmlPath)
        {
            try
            {
                string mirrorBaseFolder = Constants.MirrorFolderPath;
                if (!mirrorXmlPath.StartsWith(mirrorBaseFolder, StringComparison.OrdinalIgnoreCase))
                    return mirrorXmlPath;

                string relativePath = mirrorXmlPath.Substring(mirrorBaseFolder.Length).TrimStart(Path.DirectorySeparatorChar);

                if (relativePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    relativePath = relativePath.Substring(0, relativePath.Length - 4) + ".mp3";

                return Path.Combine(Constants.AudioFolderPath, relativePath);
            }
            catch
            {
                return mirrorXmlPath;
            }
        }

        /// <summary>
        /// Extracts a display category name from a full destination directory path.
        /// Used to build the route distribution summary at the top of a dry-run routing section.
        /// </summary>
        private static string GetRouteCategory(string destDir)
        {
            if (string.IsNullOrEmpty(destDir)) return "Unknown";
            string rel = destDir.StartsWith(Constants.AudioFolderPath, StringComparison.OrdinalIgnoreCase)
                ? destDir.Substring(Constants.AudioFolderPath.Length).TrimStart('\\', '/')
                : destDir;
            string first = rel.Split(new[] { '\\', '/' }, 2)[0];
            return first.Equals("Miscellaneous Songs", StringComparison.OrdinalIgnoreCase) ? "Misc" : first;
        }

        /// <summary>
        /// Derives a short routing summary from a relative destination path.
        /// Strips the top-level category folder and filename; formats as "A / B / C".
        /// E.g. "Artists\Dizzy Wright\Singles\Track.mp3" -> "Dizzy Wright / Singles"
        /// </summary>
        private string GetRouteSummary(string relativeDest)
        {
            string dirPath = Path.GetDirectoryName(relativeDest) ?? "";
            if (string.IsNullOrEmpty(dirPath))
                return relativeDest;
            var parts = dirPath.Split(new[] { Path.DirectorySeparatorChar, '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return relativeDest;
            if (parts.Length == 1)
                return parts[0].Equals("Miscellaneous Songs", StringComparison.OrdinalIgnoreCase) ? "Misc" : parts[0];
            return string.Join(" / ", parts.Skip(1));
        }

        /// <summary>
        /// Reads Track and Album fields from an AudioMirror XML file for display purposes.
        /// Falls back to null on any failure.
        /// </summary>
        private void ReadMirrorTrackInfo(string xmlPath, out string trackDisplay, out string album)
        {
            trackDisplay = null;
            album = null;
            try
            {
                var xmlDoc = new System.Xml.XmlDocument();
                xmlDoc.Load(xmlPath);
                string title = xmlDoc.SelectSingleNode("//Title")?.InnerText?.Trim();
                string artists = xmlDoc.SelectSingleNode("//Artists")?.InnerText?.Trim();
                string albumStr = xmlDoc.SelectSingleNode("//Album")?.InnerText?.Trim();
                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(artists))
                    trackDisplay = $"{Track.ProcessProperty(artists)[0].Trim()} - {title}";
                if (!string.IsNullOrEmpty(albumStr))
                    album = albumStr;
            }
            catch { /* fall through to null */ }
        }

        /// <summary>
        /// Detects whether the album containing the given XML mirror file is a compilation,
        /// by reading all XML files in the same folder and collecting every distinct artist
        /// across all positions (not just primary). Returns true if 3 or more distinct artists
        /// are found, which catches both traditional compilations (many primary artists) and
        /// ATD-style albums (one primary artist featuring many different people).
        /// </summary>
        private bool IsAlbumFolderCompilation(string xmlPath)
        {
            try
            {
                string albumFolder = Path.GetDirectoryName(xmlPath);
                if (string.IsNullOrEmpty(albumFolder) || !Directory.Exists(albumFolder))
                    return false;

                var xmlFiles = Directory.GetFiles(albumFolder, "*.xml");
                if (xmlFiles.Length < 3)
                    return false;

                var distinctArtists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in xmlFiles)
                {
                    try
                    {
                        var doc = new XmlDocument();
                        doc.Load(f);
                        var artistsEl = doc.SelectSingleNode("//Artists");
                        if (artistsEl == null) continue;
                        foreach (string artist in Track.ProcessProperty(artistsEl.InnerText))
                        {
                            string trimmed = artist.Trim();
                            if (!string.IsNullOrEmpty(trimmed))
                                distinctArtists.Add(trimmed);
                        }
                    }
                    catch { /* skip malformed XML */ }
                }

                return distinctArtists.Count >= 3;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Flags ScannedFile entries where the same normalised artist+title appears more than once
        /// in the batch. Sets InBatchDuplicate = true on all files sharing a key. Non-blocking.
        /// </summary>
        private void MarkInBatchDuplicates(List<ScannedFile> scannedFiles)
        {
            var keyGroups = new Dictionary<string, List<ScannedFile>>(StringComparer.Ordinal);
            foreach (var sf in scannedFiles.Where(f => f.IsReadable && f.Track != null))
            {
                string key = sf.Track.PrimaryArtist.ToLowerInvariant() + "\0" + sf.Track.Title.ToLowerInvariant();
                if (!keyGroups.ContainsKey(key)) keyGroups[key] = new List<ScannedFile>();
                keyGroups[key].Add(sf);
            }
            foreach (var group in keyGroups.Values.Where(g => g.Count > 1))
                foreach (var sf in group)
                    sf.InBatchDuplicate = true;
        }

        /// <summary>
        /// Pre-scans all files in the batch: reads and cleans tags, finds duplicates.
        /// No UI interaction. Returns a ScannedFile list for the duplicate review and routing phases.
        /// </summary>
        private List<ScannedFile> PreScanFiles(string[] files, Dictionary<string, List<string>> tagChanges)
        {
            var mirrorIndex = BuildMirrorIndex();
            var result = new List<ScannedFile>();
            foreach (var sourcePath in files)
            {
                var entry = new LogEntry { Filename = Path.GetFileName(sourcePath) };
                var sf = new ScannedFile { SourcePath = sourcePath, LogEntry = entry };
                try
                {
                    Track track = new Track();
                    using (TagLib.File tagFile = TagLib.File.Create(sourcePath))
                    {
                        Tag tag = tagFile.Tag;
                        track.Title = string.IsNullOrEmpty(tag.Title) ? "Missing" : tag.Title;
                        track.Artists = string.IsNullOrEmpty(tag.JoinedPerformers) ? "Missing" : tag.JoinedPerformers;
                        track.Album = string.IsNullOrEmpty(tag.Album) ? "Missing" : tag.Album;
                        track.Genres = string.IsNullOrEmpty(tag.JoinedGenres) ? "Missing" : tag.JoinedGenres;
                        track.Year = (tag.Year == 0) ? "Missing" : tag.Year.ToString();
                    }

                    if (dryRun && !track.Title.Equals("Missing") && !track.Artists.Equals("Missing"))
                    {
                        string rawTitle = track.Title;
                        var simArtistList = TagFixer.ExtractAndFixArtists(rawTitle, track.Artists);
                        track.Title = TagFixer.RemoveParentheticals(rawTitle);
                        track.Artists = string.Join(";", simArtistList);
                        if (!track.Album.Equals("Missing"))
                            track.Album = TagFixer.StripAlbumSuffixes(TagFixer.RemoveParentheticals(track.Album));
                        if (TagFixer.ShouldFixGenre(track.Artists, track.Genres))
                            track.Genres = TagFixer.DetermineGenre(track.Artists);
                    }

                    entry.Title = track.Title;
                    entry.Artists = track.Artists;
                    entry.Album = track.Album;
                    sf.Track = track;
                    sf.IsReadable = true;

                    // Populate tag changes for combined routing display
                    string filename = Path.GetFileName(sourcePath);
                    if (tagChanges != null && tagChanges.TryGetValue(filename, out var changes))
                        entry.TagChanges.AddRange(changes);

                    string duplicatePath = FindDuplicateInMirror(track, mirrorIndex);
                    if (!string.IsNullOrEmpty(duplicatePath))
                        sf.Duplicate = BuildDupData(sourcePath, track, duplicatePath);
                }
                catch (Exception ex)
                {
                    sf.IsReadable = false;
                    sf.ReadError = ex.Message;
                    sf.ReadException = ex;
                }
                result.Add(sf);
            }
            return result;
        }

        /// <summary>
        /// Builds a DupData object for a duplicate found during pre-scan.
        /// Computes all display strings and the [L]/[D] recommendation.
        /// </summary>
        private DupData BuildDupData(string sourcePath, Track track, string duplicatePath)
        {
            string libraryFilePath = DeriveLibraryPathFromMirrorPath(duplicatePath);
            string relLibraryPath = libraryFilePath.StartsWith(Constants.AudioFolderPath, StringComparison.OrdinalIgnoreCase)
                ? libraryFilePath.Substring(Constants.AudioFolderPath.Length).TrimStart('\\', '/')
                : libraryFilePath;
            string relMirrorPath = duplicatePath.StartsWith(Constants.MirrorFolderPath, StringComparison.OrdinalIgnoreCase)
                ? duplicatePath.Substring(Constants.MirrorFolderPath.Length).TrimStart('\\', '/')
                : Path.GetFileName(duplicatePath);
            string relNewPath = Path.GetFileName(sourcePath);

            bool libraryIsSingle = relLibraryPath.IndexOf("\\Singles\\", StringComparison.OrdinalIgnoreCase) >= 0
                                || relLibraryPath.StartsWith("Singles\\", StringComparison.OrdinalIgnoreCase);
            bool libraryIsCompilation = relMirrorPath.StartsWith("Compilations\\", StringComparison.OrdinalIgnoreCase)
                                     || relMirrorPath.StartsWith("Compilations/", StringComparison.OrdinalIgnoreCase)
                                     || IsAlbumFolderCompilation(duplicatePath);
            bool newIsAlbum = !string.IsNullOrEmpty(track.Album)
                           && !track.Album.Equals("Missing", StringComparison.OrdinalIgnoreCase)
                           && !track.Album.Equals(track.Title, StringComparison.OrdinalIgnoreCase);

            string libraryAlbum = Path.GetFileName(Path.GetDirectoryName(duplicatePath));
            bool sameAlbum = newIsAlbum && libraryAlbum.Equals(track.Album, StringComparison.OrdinalIgnoreCase);

            string dupReason = "";
            char recommendedKey = '\0';
            if (sameAlbum)
            {
                recommendedKey = 'D';
                dupReason = $"Same song from same album ('{track.Album}') - already in library";
            }
            else if (libraryIsSingle && newIsAlbum)
            {
                recommendedKey = 'L';
                dupReason = $"Library has single; new file is from album '{track.Album}' - album preferred";
            }
            else if (libraryIsCompilation && newIsAlbum)
            {
                recommendedKey = 'L';
                dupReason = $"Library copy is from a compilation; new file is from artist album '{track.Album}'";
            }

            string optD = "[D] Delete NewMusic copy (keep library)";
            string optL = "[L] Delete library copy (keep new file)";
            string optK = "[K] Keep both";
            string optQ = "[Q] Quit";
            if (recommendedKey == 'L') optL += " (recommended)";
            else if (recommendedKey == 'D') optD += " (recommended)";
            string optionsLine = recommendedKey == 'L'
                ? $"  {optL}   {optD}   {optK}   {optQ}"
                : $"  {optD}   {optL}   {optK}   {optQ}";

            string displayNewFilename = $"{track.Artists} - {track.Title}.mp3";
            ReadMirrorTrackInfo(duplicatePath, out string mirrorTrack, out string mirrorAlbum);

            return new DupData
            {
                DuplicatePath = duplicatePath,
                LibraryFilePath = libraryFilePath,
                RelLibraryPath = relLibraryPath,
                RelMirrorPath = relMirrorPath,
                RelNewPath = relNewPath,
                DisplayNewFilename = displayNewFilename,
                MirrorTrack = mirrorTrack,
                MirrorAlbum = mirrorAlbum,
                DupReason = dupReason,
                RecommendedKey = recommendedKey,
                OptionsLine = optionsLine
            };
        }

        /// <summary>
        /// Presents one duplicate to the user and collects their D/L/K/Q decision.
        /// Sets sf.Duplicate.Decision. Returns false if user pressed Q, true otherwise.
        /// </summary>
        private bool PresentDuplicateAndDecide(ScannedFile sf)
        {
            var dup = sf.Duplicate;
            Track track = sf.Track;

            string dupProposed = dup.RecommendedKey == 'L'
                ? "Delete library copy, keep new file"
                : dup.RecommendedKey == 'D'
                    ? "Delete NewMusic copy, keep library"
                    : "No version preference";

            Console.WriteLine();
            Console.WriteLine("===========================================================================");
            Console.WriteLine("  DUPLICATE FOUND");
            Console.WriteLine("===========================================================================");
            Console.WriteLine();
            Console.WriteLine($"  In AudioMirror: {dup.RelMirrorPath}");
            if (!string.IsNullOrEmpty(dup.MirrorTrack))
                Console.WriteLine($"  Track:          {dup.MirrorTrack}");
            if (!string.IsNullOrEmpty(dup.MirrorAlbum))
                Console.WriteLine($"  Album:          {dup.MirrorAlbum}");
            Console.WriteLine();
            Console.WriteLine($"  New file:   {dup.DisplayNewFilename}");
            Console.WriteLine($"  Album:      {track.Album}");
            Console.WriteLine();
            Console.WriteLine($"  Proposed:   {dupProposed}");
            if (!string.IsNullOrEmpty(dup.DupReason))
                Console.WriteLine($"  Reason:     {dup.DupReason}");
            Console.WriteLine();
            Console.WriteLine("---------------------------------------------------------------------------");
            Console.WriteLine(dup.OptionsLine);
            Console.WriteLine("---------------------------------------------------------------------------");

            if (noInput)
            {
                char autoDecision = dup.RecommendedKey != '\0' ? dup.RecommendedKey : 'K';
                dup.Decision = autoDecision;
                string autoReason = dup.RecommendedKey != '\0'
                    ? $"{autoDecision} (auto-accepted recommended)"
                    : "K (no recommendation - defaulting to Keep Both)";
                Console.WriteLine($"  [AUTO] {autoReason}");
                Console.WriteLine();
                return true;
            }

            while (true)
            {
                var key = ReadMenuKey();
                if (key == ConsoleKey.D) { dup.Decision = 'D'; Console.WriteLine(); return true; }
                else if (key == ConsoleKey.L) { dup.Decision = 'L'; Console.WriteLine(); return true; }
                else if (key == ConsoleKey.K) { dup.Decision = 'K'; Console.WriteLine(); return true; }
                else if (key == ConsoleKey.Q) { dup.Decision = 'Q'; Console.WriteLine(); return false; }
            }
        }

        /// <summary>
        /// Sanitises a tag value (e.g. Album, Artist) for use as a Windows filesystem folder name.
        /// Tag values can contain characters illegal in paths (e.g. ? in "WHAT IF?").
        /// Must be called on any tag value used in Path.Combine - never on display strings.
        /// </summary>
        private static string SanitiseFolderName(string component)
        {
            return Reflector.SanitiseFilename(component ?? "");
        }

        /// <summary>
        /// Determines the destination directory for a track based on its metadata.
        /// </summary>
        /// <param name="track">The track to route.</param>
        /// <param name="newArtistFolders">Scan-ahead result: artists getting new folders this batch.</param>
        /// <param name="reason">Output: human-readable reason for the proposed destination.</param>
        /// <returns>The full destination directory path.</returns>
        internal string GetDestDir(Track track, HashSet<string> newArtistFolders, out string reason, out RoutingConfidence confidence)
        {
            // Special case: Akira The Don (check by artist name, not genre, since genre may not be set in dry-run)
            string primaryArtist = track.PrimaryArtist;
            if (primaryArtist.IndexOf("Akira The Don", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Extract sampled person from secondary artist (after first semicolon)
                var artists = Track.ProcessProperty(track.Artists).ToList();
                if (artists.Count > 1)
                {
                    string sampledPerson = artists[1]; // Second artist is the sampled person

                    // Use existing library folder casing if one already exists (e.g. "Scott Adams" not "Scott adams")
                    string peopleParent = Path.Combine(_libraryPath, Constants.MusivDir, "Akira The Don", "People");
                    if (Directory.Exists(peopleParent))
                    {
                        foreach (var dir in Directory.GetDirectories(peopleParent))
                        {
                            string dirName = Path.GetFileName(dir);
                            if (dirName.Equals(sampledPerson, StringComparison.OrdinalIgnoreCase))
                            {
                                sampledPerson = dirName; // match library casing
                                break;
                            }
                        }
                    }

                    // Count songs for this sampled person (3+ gets own People/ folder, <3 goes to ATD Singles)
                    int personSongCount = CountAkiraTheDonPersonSongs(sampledPerson);
                    if (personSongCount >= 3)
                    {
                        string peopleFolder = Path.Combine(peopleParent, SanitiseFolderName(sampledPerson));

                        // Within People/{person}/, apply same album-vs-singles rule as Artists/ routing
                        if (!track.Album.Equals("Missing") && !track.Album.Equals(primaryArtist, StringComparison.OrdinalIgnoreCase))
                        {
                            int albumCount = CountAkiraTheDonPersonAlbumSongs(sampledPerson, track.Album);
                            if (albumCount >= 2)
                            {
                                reason = $"{albumCount} songs from album '{track.Album}'";
                                confidence = RoutingConfidence.Certain;
                                return Path.Combine(peopleFolder, SanitiseFolderName(track.Album));
                            }
                            else
                            {
                                reason = $"{personSongCount} song(s) from {sampledPerson}, only {albumCount} from this album";
                                confidence = RoutingConfidence.Certain;
                                return Path.Combine(peopleFolder, Constants.SinglesDir);
                            }
                        }
                        else
                        {
                            reason = $"{personSongCount} song(s) from {sampledPerson}, no distinct album";
                            confidence = RoutingConfidence.Certain;
                            return Path.Combine(peopleFolder, Constants.SinglesDir);
                        }
                    }
                    else
                    {
                        reason = $"{personSongCount} song(s) from {sampledPerson}, below People threshold";
                        confidence = RoutingConfidence.Certain;
                        return Path.Combine(_libraryPath, Constants.MusivDir, "Akira The Don", Constants.SinglesDir);
                    }
                }
                else
                {
                    // No sampled person listed, use Singles
                    reason = "No sampled person listed";
                    confidence = RoutingConfidence.Certain;
                    return Path.Combine(_libraryPath, Constants.MusivDir, "Akira The Don", "Singles");
                }
            }

            // Genres-based rules (after artist checks)
            if (!track.Genres.Equals("Missing") && track.Genres.IndexOf(Constants.MusivDir, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                reason = "Genre is Musivation";
                confidence = RoutingConfidence.Certain;
                return Path.Combine(_libraryPath, Constants.MusivDir);
            }

            if (!track.Genres.Equals("Missing") && track.Genres.IndexOf(Constants.MotivDir, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                reason = "Genre is Motivation";
                confidence = RoutingConfidence.Certain;
                return Path.Combine(_libraryPath, Constants.MotivDir);
            }

            string primaryArtist2 = track.PrimaryArtist;
            string artistFolder = Path.Combine(_libraryPath, Constants.ArtistsDir, SanitiseFolderName(primaryArtist2));

            // Artist folder exists OR scan-ahead says this artist needs a new one
            bool existingArtistFolder = Directory.Exists(artistFolder);
            bool scanAheadNewFolder = newArtistFolders.Contains(primaryArtist2);
            bool routeToArtists = existingArtistFolder || scanAheadNewFolder;
            if (routeToArtists)
            {
                string scanNote = scanAheadNewFolder ? " [new via scan-ahead]" : "";
                // Existing folder = Certain; scan-ahead new folder = Likely (creating new structure)
                RoutingConfidence artistConfidence = existingArtistFolder ? RoutingConfidence.Certain : RoutingConfidence.Likely;

                if (!track.Album.Equals("Missing") && !track.Album.Equals(primaryArtist2))
                {
                    // Count songs from this album (holistic: library + batch combined)
                    int albumCount = CountAlbumSongs(primaryArtist2, track.Album);
                    if (albumCount >= 2)
                    {
                        reason = $"Artist folder{scanNote}; {albumCount} songs from album -> album subfolder";
                        confidence = artistConfidence;
                        return Path.Combine(artistFolder, SanitiseFolderName(track.Album));
                    }
                    else
                    {
                        reason = $"Artist folder{scanNote}; only {albumCount} {(albumCount == 1 ? "song" : "songs")} from album -> Singles/";
                        confidence = artistConfidence;
                        return Path.Combine(artistFolder, "Singles");
                    }
                }
                else
                {
                    reason = $"Artist folder{scanNote}; no distinct album -> Singles/";
                    confidence = artistConfidence;
                    return Path.Combine(artistFolder, "Singles");
                }
            }

            int scanBatch = (_scanAheadBatchCounts != null && _scanAheadBatchCounts.TryGetValue(primaryArtist2, out int sb2)) ? sb2 : 0;
            int scanMisc = (_scanAheadMiscCounts != null && _scanAheadMiscCounts.TryGetValue(primaryArtist2, out int sm)) ? sm : 0;
            int scanSources = (_scanAheadSourcesCounts != null && _scanAheadSourcesCounts.TryGetValue(primaryArtist2, out int ss)) ? ss : 0;
            int scanTotal = scanBatch + scanMisc + scanSources;
            reason = scanTotal > 0
                ? $"no artist folder; {scanBatch} in batch + {scanMisc + scanSources} in library = {scanTotal} total, below threshold 3 -> Misc"
                : "No artist folder found in library";
            confidence = RoutingConfidence.Certain;
            return Path.Combine(_libraryPath, Constants.MiscDir);
        }

        /// <summary>
        /// Count total songs from an album (library + new batch combined).
        /// Returns the count of existing library songs + all songs from this album in the current files array.
        /// </summary>
        private int CountAlbumSongs(string artist, string album)
        {
            int count = 0;

            // Count in library (scan artist folder for album subfolders)
            string artistFolder = Path.Combine(_libraryPath, Constants.ArtistsDir, SanitiseFolderName(artist));
            string albumFolder = Path.Combine(artistFolder, SanitiseFolderName(album));
            if (Directory.Exists(albumFolder))
            {
                count += Directory.GetFiles(albumFolder, "*.mp3", SearchOption.AllDirectories).Length;
            }

            // Count in new batch (scan NewMusic for files with matching artist + album)
            if (Directory.Exists(Constants.NewMusicPath))
            {
                var newMusicFiles = Directory.GetFiles(Constants.NewMusicPath, "*.mp3", SearchOption.AllDirectories);
                foreach (var filePath in newMusicFiles)
                {
                    try
                    {
                        using (TagLib.File tagFile = TagLib.File.Create(filePath))
                        {
                            var tag = tagFile.Tag;
                            string fileArtists = tag.JoinedPerformers ?? "";
                            string fileAlbum = tag.Album ?? "";

                            if (!fileAlbum.Equals("Missing", StringComparison.OrdinalIgnoreCase) &&
                                fileAlbum.Equals(album, StringComparison.OrdinalIgnoreCase) &&
                                fileArtists.IndexOf(artist, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                count++;
                            }
                        }
                    }
                    catch
                    {
                        // Skip files that can't be read
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Count songs for a sampled person in Akira The Don catalog (library + new batch combined).
        /// Used to determine if person gets their own People/{person}/ folder (3+ songs) or Singles/.
        /// </summary>
        private int CountAkiraTheDonPersonSongs(string sampledPerson)
        {
            int count = 0;

            // Count in library (scan Musivation/Akira The Don/People/{person}/)
            string personFolder = Path.Combine(Constants.AudioFolderPath, Constants.MusivDir, "Akira The Don", "People", SanitiseFolderName(sampledPerson));
            if (Directory.Exists(personFolder))
            {
                count += Directory.GetFiles(personFolder, "*.mp3", SearchOption.AllDirectories).Length;
            }

            // Count in library Singles folder
            string singlesFolder = Path.Combine(Constants.AudioFolderPath, Constants.MusivDir, "Akira The Don", "Singles");
            if (Directory.Exists(singlesFolder))
            {
                var singlesFiles = Directory.GetFiles(singlesFolder, "*.mp3", SearchOption.AllDirectories);
                foreach (var filePath in singlesFiles)
                {
                    try
                    {
                        using (TagLib.File tagFile = TagLib.File.Create(filePath))
                        {
                            var tag = tagFile.Tag;
                            string fileArtists = tag.JoinedPerformers ?? "";
                            // Check if this file has sampledPerson as secondary artist
                            var artistList = Track.ProcessProperty(fileArtists).ToList();
                            if (artistList.Count > 1 && artistList[1].Equals(sampledPerson, StringComparison.OrdinalIgnoreCase))
                            {
                                count++;
                            }
                        }
                    }
                    catch
                    {
                        // Skip files that can't be read
                    }
                }
            }

            // Count in new batch (scan NewMusic for Akira The Don files with matching sampled person)
            if (Directory.Exists(Constants.NewMusicPath))
            {
                var newMusicFiles = Directory.GetFiles(Constants.NewMusicPath, "*.mp3", SearchOption.AllDirectories);
                foreach (var filePath in newMusicFiles)
                {
                    try
                    {
                        using (TagLib.File tagFile = TagLib.File.Create(filePath))
                        {
                            var tag = tagFile.Tag;
                            string fileArtists = tag.JoinedPerformers ?? "";

                            // Check if file is Akira The Don + has matching sampled person.
                            // Don't require Musivation genre - genre isn't set yet in dry-run (TagFixer hasn't run).
                            // Artist name check is sufficient for batch holistic counting.
                            if (fileArtists.IndexOf("Akira The Don", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                var artistList = Track.ProcessProperty(fileArtists).ToList();
                                if (artistList.Count > 1 && artistList[1].Equals(sampledPerson, StringComparison.OrdinalIgnoreCase))
                                {
                                    count++;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip files that can't be read
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Count songs from a specific album for a sampled person in People/{sampledPerson}/{album}/ (library + batch).
        /// Mirrors CountAlbumSongs but for the ATD People/ folder structure.
        /// Used to decide: People/{person}/{album}/ if 2+ songs, else People/{person}/Singles/.
        /// </summary>
        private int CountAkiraTheDonPersonAlbumSongs(string sampledPerson, string album)
        {
            int count = 0;

            // Count in library: Musivation/Akira The Don/People/{sampledPerson}/{album}/
            string albumFolder = Path.Combine(
                Constants.AudioFolderPath, Constants.MusivDir, "Akira The Don", "People", SanitiseFolderName(sampledPerson), SanitiseFolderName(album));
            if (Directory.Exists(albumFolder))
            {
                count += Directory.GetFiles(albumFolder, "*.mp3", SearchOption.AllDirectories).Length;
            }

            // Count in new batch: ATD songs with matching sampled person + same album
            if (Directory.Exists(Constants.NewMusicPath))
            {
                foreach (var filePath in Directory.GetFiles(Constants.NewMusicPath, "*.mp3", SearchOption.AllDirectories))
                {
                    try
                    {
                        using (TagLib.File tagFile = TagLib.File.Create(filePath))
                        {
                            var tag = tagFile.Tag;
                            string fileArtists = tag.JoinedPerformers ?? "";
                            string fileAlbum = tag.Album ?? "";

                            if (fileArtists.IndexOf("Akira The Don", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                fileAlbum.Equals(album, StringComparison.OrdinalIgnoreCase))
                            {
                                var artistList = Track.ProcessProperty(fileArtists).ToList();
                                if (artistList.Count > 1 && artistList[1].Equals(sampledPerson, StringComparison.OrdinalIgnoreCase))
                                    count++;
                            }
                        }
                    }
                    catch { /* skip unreadable files */ }
                }
            }

            return count;
        }

        /// <summary>
        /// Reads a menu key using ReadLine so the user must press Enter to confirm.
        /// Prevents right-click paste in Windows Terminal from silently triggering actions.
        /// </summary>
        private ConsoleKey ReadMenuKey()
        {
            while (true)
            {
                Console.Write("> ");
                string input = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
                switch (input)
                {
                    case "Y": return ConsoleKey.Y;
                    case "N": return ConsoleKey.N;
                    case "D": return ConsoleKey.D;
                    case "L": return ConsoleKey.L;
                    case "K": return ConsoleKey.K;
                    case "Q": return ConsoleKey.Q;
                }
            }
        }

    }
}
