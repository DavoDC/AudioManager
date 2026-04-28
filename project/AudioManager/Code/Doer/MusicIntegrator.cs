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

        private void PrintTimestamped(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        /// <summary>
        /// Construct and run the music integrator
        /// </summary>
        /// <param name="dryRun">If true, print planned actions without executing any file moves.</param>
        public MusicIntegrator(bool dryRun = false)
        {
            this.dryRun = dryRun;
            string modeLabel = dryRun ? " [DRY RUN - no files will be moved]" : "";
            Console.WriteLine($"\nIntegrating new music...{modeLabel}");

            int movedCount = 0;
            int skippedCount = 0;
            var logEntries = new List<LogEntry>();
            var decisionLog = new DecisionLog(dryRun);
            int totalFiles = 0;

            try
            {
                // Step 1: Fix tags in NewMusic folder (comprehensive tag cleanup via TagFixer)
                Console.WriteLine("\n[Step 1/2] Fixing tags...");
                new TagFixer(dryRun);

                // Step 2: Route files into library (after tags are clean)
                Console.WriteLine("\n[Step 2/2] Routing files...");

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

                foreach (var sourcePath in files)
                {
                    var entry = new LogEntry { Filename = Path.GetFileName(sourcePath) };
                    try
                    {
                        // Read tags directly from file
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

                        entry.Title = track.Title;
                        entry.Artists = track.Artists;
                        entry.Album = track.Album;

                        // Determine primary artist via Track.ProcessProperty inside PrimaryArtist getter
                        string primaryArtist = track.PrimaryArtist;

                        // Check for duplicate: same artist + title already in library
                        string duplicatePath = FindDuplicateInMirror(track);
                        if (!string.IsNullOrEmpty(duplicatePath))
                        {
                            // Derive actual library file path from AudioMirror XML path
                            // AudioMirror: C:\...\AudioMirror\AUDIO_MIRROR\Artist\Album\Track.xml
                            // Library:     C:\Users\David\Audio\Artist\Album\Track.mp3
                            string libraryFilePath = DeriveLibraryPathFromMirrorPath(duplicatePath);

                            // Clear and show the duplicate
                            Console.Clear();
                            Console.WriteLine("============================================================");
                            Console.WriteLine("  DUPLICATE FOUND");
                            Console.WriteLine("============================================================\n");
                            Console.WriteLine($"  In library: {libraryFilePath}");
                            Console.WriteLine($"  New file:   {sourcePath}");
                            Console.WriteLine($"\n  Track: {track.Artists} - {track.Title}");
                            Console.WriteLine($"  Album: {track.Album}");
                            Console.WriteLine("\n------------------------------------------------------------");
                            Console.WriteLine("  [D] Delete from NewMusic   [L] Delete from Library");
                            Console.WriteLine("  [K] Keep both             [Q] Quit");
                            Console.WriteLine("------------------------------------------------------------");

                            // Wait for input
                            while (true)
                            {
                                var key = Console.ReadKey(intercept: true).Key;
                                if (key == ConsoleKey.D)
                                {
                                    // Delete from NewMusic
                                    if (dryRun)
                                    {
                                        Console.WriteLine($"  [DRY RUN] Would delete: {Path.GetFileName(sourcePath)}");
                                        entry.Status = "would-delete";
                                        entry.Detail = "duplicate (would delete)";
                                        logEntries.Add(entry); skippedCount++;
                                    }
                                    else
                                    {
                                        File.Delete(sourcePath);
                                        Console.WriteLine($"  Deleted: {Path.GetFileName(sourcePath)}");
                                        entry.Status = "deleted";
                                        entry.Detail = "duplicate (deleted)";
                                        logEntries.Add(entry); skippedCount++;
                                    }
                                    Console.Clear();
                                    break;
                                }
                                else if (key == ConsoleKey.L)
                                {
                                    // Delete from Library (replace old with new album version)
                                    if (dryRun)
                                    {
                                        Console.WriteLine($"  [DRY RUN] Would delete from library: {libraryFilePath}");
                                        Console.WriteLine($"  [DRY RUN] Would keep new file: {Path.GetFileName(sourcePath)}");
                                        entry.Status = "would-replace";
                                        entry.Detail = "duplicate (would replace)";
                                        logEntries.Add(entry); skippedCount++;
                                    }
                                    else
                                    {
                                        if (File.Exists(libraryFilePath))
                                        {
                                            File.Delete(libraryFilePath);
                                            Console.WriteLine($"  Deleted from library: {libraryFilePath}");
                                            Console.WriteLine($"  Integrating new version: {Path.GetFileName(sourcePath)}");
                                            entry.Status = "replaced";
                                            entry.Detail = "duplicate (library replaced)";
                                            // Fall through to integration
                                        }
                                        else
                                        {
                                            Console.WriteLine($"  [WARN] Library file not found: {libraryFilePath}");
                                            entry.Status = "error";
                                            entry.Detail = "duplicate (library file not found)";
                                            logEntries.Add(entry); skippedCount++;
                                        }
                                    }
                                    if (dryRun || entry.Status == "error")
                                    {
                                        Console.Clear();
                                        break;
                                    }
                                    // If real run and successful delete, continue to integration
                                    break;
                                }
                                else if (key == ConsoleKey.K)
                                {
                                    // Keep and continue
                                    Console.WriteLine($"  Keeping both - proceeding with integration");
                                    break;
                                }
                                else if (key == ConsoleKey.Q)
                                {
                                    Console.WriteLine("\n - Quit. Remaining files left for next run.");
                                    entry.Status = "quit"; logEntries.Add(entry);
                                    return; // exits foreach, hits finally
                                }
                                // ignore other keys
                            }

                            // If we deleted from NewMusic, skip to next file
                            if (entry.Status == "deleted" || entry.Status == "would-delete")
                                continue;

                            // If duplicate error, skip to next file
                            if (entry.Status == "error")
                                continue;
                        }

                        // Skip if un-routable (tags are already clean from TagFixer)
                        if (track.Title.Equals("Missing") || track.Artists.Equals("Missing") || primaryArtist.Equals("Missing"))
                        {
                            PrintTimestamped($"- Skipped '{Path.GetFileName(sourcePath)}': missing required tag");
                            entry.Status = "skipped"; entry.Detail = "missing required tag";
                            logEntries.Add(entry); skippedCount++;
                            continue;
                        }

                        // Build destination filename
                        string sanitisedArtists = Reflector.SanitiseFilename(track.Artists);
                        string sanitisedTitle = Reflector.SanitiseFilename(track.Title);
                        string destFilename = sanitisedArtists + " - " + sanitisedTitle + ".mp3";

                        // Determine destination directory and reason
                        string destDir = GetDestDir(track, newArtistFolders, out string reason);

                        // Ensure destination directory exists for relative path computation
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
                                {
                                    relativeDest = relativeDest.Substring(1);
                                }
                            }
                        }

                        entry.Destination = relativeDest;

                        // Print track header
                        Console.WriteLine();
                        PrintTimestamped("============================================================");
                        PrintTimestamped($"  {track.Artists} - {track.Title}");
                        PrintTimestamped("============================================================");
                        Console.WriteLine();
                        PrintTimestamped($"  Album:   {track.Album}");
                        PrintTimestamped($"  Year:    {track.Year}");
                        PrintTimestamped($"  Genres:  {track.Genres}");
                        Console.WriteLine();
                        PrintTimestamped($"  Proposed: {relativeDest}");
                        PrintTimestamped($"  Reason:   {reason}");
                        Console.WriteLine();
                        PrintTimestamped("------------------------------------------------------------");

                        // Both dry-run and real mode: show confirmation prompts to user
                        PrintTimestamped("  [Y] Accept   [N] Choose folder   [Q] Quit");
                        PrintTimestamped("------------------------------------------------------------");

                        // Wait for input
                        while (true)
                        {
                            var key = Console.ReadKey(intercept: true).Key;
                            if (key == ConsoleKey.Y)
                            {
                                // Accept proposed destination
                                if (!dryRun && File.Exists(destPath))
                                {
                                    PrintTimestamped($"  - Skipped '{Path.GetFileName(sourcePath)}': already exists at destination");
                                    entry.Status = "skipped"; entry.Detail = "already exists at destination";
                                    logEntries.Add(entry); skippedCount++;
                                }
                                else if (dryRun)
                                {
                                    // Dry run: log what would happen without moving
                                    PrintTimestamped($"  [DRY RUN] Would move to: {relativeDest}");
                                    decisionLog.LogDecision(track, Path.GetFileName(sourcePath), relativeDest, reason);
                                    entry.Status = "would-move";
                                    logEntries.Add(entry); movedCount++;
                                }
                                else
                                {
                                    // Real integration: actually move the file
                                    Directory.CreateDirectory(destDir);
                                    File.Move(sourcePath, destPath);
                                    movedCount++;
                                    PrintTimestamped($"  Moved to: {relativeDest}");
                                    // Log the routing decision
                                    decisionLog.LogDecision(track, Path.GetFileName(sourcePath), relativeDest, reason);
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
                                return; // exits foreach, hits finally
                            }
                            else if (key == ConsoleKey.N)
                            {
                                // Choose alternative folder
                                string chosen = PickFolder();
                                if (string.IsNullOrEmpty(chosen))
                                {
                                    // cancelled selection, redisplay prompt
                                    Console.WriteLine("  Folder selection cancelled.");
                                    continue;
                                }

                                string newDestDir = chosen;
                                string newDestPath = Path.Combine(newDestDir, destFilename);
                                if (!dryRun)
                                {
                                    Directory.CreateDirectory(newDestDir);
                                }

                                if (!dryRun && File.Exists(newDestPath))
                                {
                                    Console.WriteLine($"  - Skipped '{Path.GetFileName(sourcePath)}': already exists at destination");
                                    entry.Status = "skipped"; entry.Detail = "already exists at destination";
                                    logEntries.Add(entry); skippedCount++;
                                }
                                else if (dryRun)
                                {
                                    // Dry run: log user's manual selection without moving
                                    string rel = newDestPath;
                                    if (newDestPath.StartsWith(Constants.AudioFolderPath, StringComparison.OrdinalIgnoreCase))
                                    {
                                        int startIdx = Constants.AudioFolderPath.Length;
                                        if (startIdx <= newDestPath.Length)
                                        {
                                            rel = newDestPath.Substring(startIdx);
                                            if (rel.StartsWith("\\") || rel.StartsWith("/")) rel = rel.Substring(1);
                                        }
                                    }
                                    PrintTimestamped($"  [DRY RUN] Would move to: {rel}");
                                    decisionLog.LogDecision(track, Path.GetFileName(sourcePath), rel, "User manual folder selection");
                                    entry.Destination = rel;
                                    entry.Status = "would-move";
                                    logEntries.Add(entry); movedCount++;
                                }
                                else
                                {
                                    // Real integration: actually move to user's chosen folder
                                    File.Move(sourcePath, newDestPath);
                                    movedCount++;
                                    string rel = newDestPath;
                                    if (newDestPath.StartsWith(Constants.AudioFolderPath, StringComparison.OrdinalIgnoreCase))
                                    {
                                        int startIdx = Constants.AudioFolderPath.Length;
                                        if (startIdx <= newDestPath.Length)
                                        {
                                            rel = newDestPath.Substring(startIdx);
                                            if (rel.StartsWith("\\") || rel.StartsWith("/")) rel = rel.Substring(1);
                                        }
                                    }
                                    PrintTimestamped($"  Moved to: {rel}");
                                    // Log the routing decision (user manually selected folder)
                                    decisionLog.LogDecision(track, Path.GetFileName(sourcePath), rel, "User manual folder selection");
                                    Console.WriteLine();
                                    entry.Destination = rel;
                                    entry.Status = "moved";
                                    logEntries.Add(entry);
                                }

                                break;
                            }
                            // ignore other keys
                        }
                    }
                    catch (Exception ex)
                    {
                        // Fail fast on integration errors - do not silently skip files
                        // Silent skips could mask data corruption or library corruption
                        Console.WriteLine();
                        PrintTimestamped("============================================================");
                        PrintTimestamped("INTEGRATION FAILED");
                        PrintTimestamped("============================================================");
                        Console.WriteLine();
                        PrintTimestamped($"Error processing file: {Path.GetFileName(sourcePath)}");
                        PrintTimestamped($"Full path: {sourcePath}");
                        Console.WriteLine();
                        PrintTimestamped($"Error details: {ex.Message}");
                        if (!string.IsNullOrEmpty(ex.StackTrace))
                            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
                        Console.WriteLine("\n============================================================");
                        Console.WriteLine("\nIntegration halted. Please fix the error above and retry.");
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadKey();
                        throw new InvalidOperationException($"Integration error on file '{Path.GetFileName(sourcePath)}': {ex.Message}", ex);
                    }
                }

                Console.Clear();
                Console.WriteLine("============================================================");
                Console.WriteLine(dryRun ? "  Dry Run complete (no files moved)" : "  Integration complete");
                Console.WriteLine("============================================================\n");
                Console.WriteLine(dryRun ? $"  Would move: {movedCount}" : $"  Moved:   {movedCount}");
                Console.WriteLine($"  Skipped: {skippedCount}");
                Console.WriteLine("\n------------------------------------------------------------");

                // Print confidence report to console + save to log file
                PrintConfidenceReport(logEntries, totalFiles, movedCount, skippedCount);
                SaveLog(logEntries, totalFiles, movedCount, skippedCount);

                // Save routing decisions to XML for pattern analysis and audit trail
                decisionLog.Save();
            }
            finally
            {
                FinishAndPrintTimeTaken();
            }
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
            string mirrorMiscPath = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, Constants.MirrorFolderPath, Constants.MiscDir));
            if (Directory.Exists(mirrorMiscPath))
            {
                foreach (var xmlFile in Directory.GetFiles(mirrorMiscPath, "*.xml"))
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
                    }
                    catch { /* skip malformed XML */ }
                }
            }

            // Find artists that will hit 3+ threshold and don't already have an Artists/ folder
            var previewLines = new List<string>();
            foreach (var kvp in batchCounts)
            {
                string artist = kvp.Key;
                int batchCount = kvp.Value;
                int miscCount = miscCounts.ContainsKey(artist) ? miscCounts[artist] : 0;
                int total = batchCount + miscCount;

                string artistFolder = Path.Combine(Constants.AudioFolderPath, Constants.ArtistsDir, artist);
                if (total >= 3 && !Directory.Exists(artistFolder))
                {
                    result.Add(artist);
                    string note = miscCount > 0
                        ? $"{batchCount} in batch + {miscCount} in Misc = {total} total -> new Artists/{artist}/"
                        : $"{batchCount} in batch = {total} total -> new Artists/{artist}/";
                    if (miscCount > 0)
                        note += $" (NOTE: {miscCount} existing Misc song(s) need manual migration)";
                    previewLines.Add($"  - {note}");
                }
            }

            if (result.Count > 0)
            {
                Console.WriteLine($"\nScan-ahead: {result.Count} artist(s) will hit 3-song threshold:");
                foreach (var line in previewLines) Console.WriteLine(line);
                Console.WriteLine();
            }

            return result;
        }

        /// <summary>
        /// Prints a confidence report to the console after integration.
        /// Covers: count check, per-file table, new folders, destination sanity check, errors.
        /// </summary>
        private void PrintConfidenceReport(List<LogEntry> entries, int totalFiles, int movedCount, int skippedCount)
        {
            Console.WriteLine("\n============================================================");
            Console.WriteLine(dryRun ? "  CONFIDENCE REPORT (Dry Run)" : "  CONFIDENCE REPORT");
            Console.WriteLine("============================================================\n");

            // 1. Count check
            int expectedMoved = totalFiles - skippedCount;
            bool countOk = dryRun || (movedCount == expectedMoved);
            string countLine = $"  Files in NewMusic: {totalFiles}  |  Moved: {movedCount}  |  Skipped: {skippedCount}";
            Console.WriteLine(countLine);
            if (!countOk)
                Console.WriteLine($"  [ERROR] Count mismatch! Expected {expectedMoved} moved, got {movedCount}.");

            // 2. Per-file table
            Console.WriteLine("\n  --- Per-file results ---");
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
                        // Only report folders that were just created (i.e. didn't exist before this run)
                        // We approximate: if this is the only file in the folder, it's new
                        if (Directory.Exists(fullDir) && Directory.GetFiles(fullDir).Length == 1)
                        {
                            newFolders.Add(destFolder);
                        }
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
                            // Readable = OK (just opening it is enough to verify)
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
                PrintTimestamped($"[ERRORS: {errors.Count}]");
                foreach (var e in errors) PrintTimestamped($"- {e.Filename}: {e.Detail}");
            }
            else
            {
                PrintTimestamped("No errors.");
            }

            Console.WriteLine("\n============================================================");
        }

        /// <summary>
        /// Saves a formatted integration log to logs/integration-YYYYMMDD.md in markdown format.
        /// </summary>
        private void SaveLog(List<LogEntry> entries, int totalFiles, int movedCount, int skippedCount)
        {
            try
            {
                string logsDir = Constants.LogsPath;
                Directory.CreateDirectory(logsDir);

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
                string suffix = dryRun ? "-dryrun" : "";
                string logPath = Path.Combine(logsDir, $"integration-{timestamp}{suffix}.md");

                var sb = new StringBuilder();
                sb.AppendLine($"# {(dryRun ? "Integration Dry Run" : "Integration Log")}");
                sb.AppendLine();
                sb.AppendLine($"**Date:** {DateTime.Now:yyyy-MM-dd HH:mm}");
                sb.AppendLine();
                sb.AppendLine($"**Results:** {movedCount} moved, {skippedCount} skipped (total {totalFiles} files)");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();

                foreach (var e in entries)
                {
                    sb.AppendLine($"## [{e.Status?.ToUpper()}] {e.Filename}");
                    sb.AppendLine();
                    sb.AppendLine($"- **Artist:** {e.Artists}");
                    sb.AppendLine($"- **Title:** {e.Title}");
                    sb.AppendLine($"- **Album:** {e.Album}");
                    if (!string.IsNullOrEmpty(e.Destination))
                        sb.AppendLine($"- **Destination:** `{e.Destination}`");
                    if (e.TagChanges?.Count > 0)
                        sb.AppendLine($"- **Tags:** {string.Join(", ", e.TagChanges)}");
                    if (!string.IsNullOrEmpty(e.Detail))
                        sb.AppendLine($"- **Note:** {e.Detail}");
                    sb.AppendLine();
                }

                File.WriteAllText(logPath, sb.ToString());
                Console.WriteLine($"\nLog saved: logs\\integration-{timestamp}{suffix}.md");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [WARN] Could not save integration log: {ex.Message}");
            }
        }

        /// <summary>
        /// Searches AudioMirror XML files for an existing track with the same primary artist and title.
        /// Returns the path to the matching track's XML file if found, null otherwise.
        /// Search is case-insensitive and whitespace-trimmed.
        /// </summary>
        private string FindDuplicateInMirror(Track track)
        {
            try
            {
                string primaryArtist = track.PrimaryArtist;
                if (string.IsNullOrEmpty(primaryArtist) || primaryArtist.Equals("Missing"))
                    return null;

                string title = track.Title;
                if (string.IsNullOrEmpty(title) || title.Equals("Missing"))
                    return null;

                // Search all XML files in the mirror for a match
                if (!Directory.Exists(Constants.MirrorFolderPath))
                    return null;

                foreach (var xmlFile in Directory.GetFiles(Constants.MirrorFolderPath, "*.xml", SearchOption.AllDirectories))
                {
                    try
                    {
                        var xmlDoc = new System.Xml.XmlDocument();
                        xmlDoc.Load(xmlFile);

                        var artistsEl = xmlDoc.SelectSingleNode("//Artists");
                        var titleEl = xmlDoc.SelectSingleNode("//Title");

                        if (artistsEl == null || titleEl == null)
                            continue;

                        // Extract primary artist from the XML (may have multiple artists)
                        string mirrorArtistsRaw = artistsEl.InnerText;
                        if (string.IsNullOrEmpty(mirrorArtistsRaw))
                            continue;

                        string mirrorPrimary = Track.ProcessProperty(mirrorArtistsRaw)[0].Trim();
                        string mirrorTitle = titleEl.InnerText.Trim();

                        // Case-insensitive, whitespace-trimmed comparison
                        if (mirrorPrimary.Equals(primaryArtist, StringComparison.OrdinalIgnoreCase) &&
                            mirrorTitle.Equals(title, StringComparison.OrdinalIgnoreCase))
                        {
                            return xmlFile;
                        }
                    }
                    catch { /* skip malformed XML */ }
                }
            }
            catch { /* skip errors */ }

            return null;
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
                // Extract relative path from AUDIO_MIRROR folder
                string mirrorBaseFolder = Path.Combine(Constants.MirrorFolderPath, "AUDIO_MIRROR");
                if (!mirrorXmlPath.StartsWith(mirrorBaseFolder, StringComparison.OrdinalIgnoreCase))
                    return mirrorXmlPath; // fallback: return as-is

                // Remove AUDIO_MIRROR prefix to get relative path
                string relativePath = mirrorXmlPath.Substring(mirrorBaseFolder.Length).TrimStart(Path.DirectorySeparatorChar);

                // Replace .xml with .mp3
                if (relativePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    relativePath = relativePath.Substring(0, relativePath.Length - 4) + ".mp3";

                // Construct full library path
                return Path.Combine(Constants.AudioFolderPath, relativePath);
            }
            catch
            {
                return mirrorXmlPath; // fallback
            }
        }

        /// <summary>
        /// Determines the destination directory for a track based on its metadata.
        /// </summary>
        /// <param name="track">The track to route.</param>
        /// <param name="newArtistFolders">Scan-ahead result: artists getting new folders this batch.</param>
        /// <param name="reason">Output: human-readable reason for the proposed destination.</param>
        /// <returns>The full destination directory path.</returns>
        private string GetDestDir(Track track, HashSet<string> newArtistFolders, out string reason)
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

                    // Count songs for this sampled person (3+ gets own folder, <3 goes to Singles)
                    int personSongCount = CountAkiraTheDonPersonSongs(sampledPerson);
                    if (personSongCount >= 3)
                    {
                        string peopleFolder = Path.Combine(Constants.AudioFolderPath, Constants.MusivDir, "Akira The Don", "People", sampledPerson);
                        reason = $"Akira The Don -> People/{sampledPerson} ({personSongCount} songs)";
                        return peopleFolder;
                    }
                    else
                    {
                        reason = $"Akira The Don -> Singles ({personSongCount} songs from {sampledPerson})";
                        return Path.Combine(Constants.AudioFolderPath, Constants.MusivDir, "Akira The Don", "Singles");
                    }
                }
                else
                {
                    // No sampled person listed, use Singles
                    reason = "Akira The Don -> Singles (no sampled person)";
                    return Path.Combine(Constants.AudioFolderPath, Constants.MusivDir, "Akira The Don", "Singles");
                }
            }

            // Genres-based rules (after artist checks)
            if (!track.Genres.Equals("Missing") && track.Genres.IndexOf(Constants.MusivDir, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                reason = "Genre is Musivation";
                return Path.Combine(Constants.AudioFolderPath, Constants.MusivDir);
            }

            if (!track.Genres.Equals("Missing") && track.Genres.IndexOf(Constants.MotivDir, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                reason = "Genre is Motivation";
                return Path.Combine(Constants.AudioFolderPath, Constants.MotivDir);
            }

            string primaryArtist2 = track.PrimaryArtist;
            string artistFolder = Path.Combine(Constants.AudioFolderPath, Constants.ArtistsDir, primaryArtist2);

            // Artist folder exists OR scan-ahead says this artist needs a new one
            bool routeToArtists = Directory.Exists(artistFolder) || newArtistFolders.Contains(primaryArtist2);
            if (routeToArtists)
            {
                string scanNote = newArtistFolders.Contains(primaryArtist2) ? " [new via scan-ahead]" : "";
                if (!track.Album.Equals("Missing") && !track.Album.Equals(primaryArtist2))
                {
                    // Count songs from this album (holistic: library + batch combined)
                    int albumCount = CountAlbumSongs(primaryArtist2, track.Album);
                    if (albumCount >= 2)
                    {
                        reason = $"Artist folder{scanNote}; {albumCount} songs from album -> album subfolder";
                        return Path.Combine(artistFolder, track.Album);
                    }
                    else
                    {
                        reason = $"Artist folder{scanNote}; only {albumCount} song(s) from album -> Singles/";
                        return Path.Combine(artistFolder, "Singles");
                    }
                }
                else
                {
                    reason = $"Artist folder{scanNote}; no distinct album -> Singles/";
                    return Path.Combine(artistFolder, "Singles");
                }
            }

            reason = "No artist folder found in library";
            return Path.Combine(Constants.AudioFolderPath, Constants.MiscDir);
        }

        /// <summary>
        /// Count total songs from an album (library + new batch combined).
        /// Returns the count of existing library songs + all songs from this album in the current files array.
        /// </summary>
        private int CountAlbumSongs(string artist, string album)
        {
            int count = 0;

            // Count in library (scan artist folder for album subfolders)
            string artistFolder = Path.Combine(Constants.AudioFolderPath, Constants.ArtistsDir, artist);
            string albumFolder = Path.Combine(artistFolder, album);
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
            string personFolder = Path.Combine(Constants.AudioFolderPath, Constants.MusivDir, "Akira The Don", "People", sampledPerson);
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
                            string genres = tag.JoinedGenres ?? "";

                            // Check if file is Akira The Don + has matching sampled person
                            if (fileArtists.IndexOf("Akira The Don", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                genres.IndexOf(Constants.MusivDir, StringComparison.OrdinalIgnoreCase) >= 0)
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
        /// Presents a navigable list of folders under the Audio library and allows the user to pick one.
        /// Includes a "New folder" option which creates a new folder under a chosen parent.
        /// Returns the chosen folder full path, or null/empty if selection cancelled.
        /// </summary>
        private string PickFolder()
        {
            // Get all directories under AudioFolderPath
            var dirs = Directory.Exists(Constants.AudioFolderPath)
                ? Directory.GetDirectories(Constants.AudioFolderPath, "*", SearchOption.AllDirectories).ToList()
                : new System.Collections.Generic.List<string>();

            // Prepare display names (relative to AudioFolderPath)
            var display = new System.Collections.Generic.List<string>();
            foreach (var d in dirs)
            {
                string rel = d;
                int startIdx = Constants.AudioFolderPath.Length;
                if (startIdx <= d.Length && d.StartsWith(Constants.AudioFolderPath, StringComparison.OrdinalIgnoreCase))
                {
                    rel = d.Substring(startIdx);
                    if (rel.StartsWith("\\") || rel.StartsWith("/")) rel = rel.Substring(1);
                }
                display.Add(rel);
            }

            // Add New folder option
            display.Add("[New folder]");

            // Arrow-key menu selection
            int selected = 0;
            while (true)
            {
                Console.Clear();
                Console.WriteLine("Select destination folder (use arrows, Enter to choose):\n");
                for (int i = 0; i < display.Count; i++)
                {
                    Console.WriteLine(i == selected ? $"  > {display[i]}" : $"    {display[i]}");
                }

                var key = Console.ReadKey(intercept: true).Key;
                if (key == ConsoleKey.UpArrow) selected = (selected == 0) ? display.Count - 1 : selected - 1;
                if (key == ConsoleKey.DownArrow) selected = (selected == display.Count - 1) ? 0 : selected + 1;
                if (key == ConsoleKey.Enter)
                {
                    // New folder selected
                    if (selected == display.Count - 1)
                    {
                        // Choose parent
                        int parentSelected = 0;
                        var parentDisplay = new System.Collections.Generic.List<string>(display);
                        parentDisplay.RemoveAt(parentDisplay.Count - 1); // remove New folder
                        parentDisplay.Insert(0, "[Root]");

                        while (true)
                        {
                            Console.Clear();
                            Console.WriteLine("Select parent folder for new folder (Enter to choose):\n");
                            for (int j = 0; j < parentDisplay.Count; j++)
                            {
                                Console.WriteLine(j == parentSelected ? $"  > {parentDisplay[j]}" : $"    {parentDisplay[j]}");
                            }
                            var pk = Console.ReadKey(intercept: true).Key;
                            if (pk == ConsoleKey.UpArrow) parentSelected = (parentSelected == 0) ? parentDisplay.Count - 1 : parentSelected - 1;
                            if (pk == ConsoleKey.DownArrow) parentSelected = (parentSelected == parentDisplay.Count - 1) ? 0 : parentSelected + 1;
                            if (pk == ConsoleKey.Enter)
                            {
                                string parentPath;
                                if (parentSelected == 0)
                                {
                                    parentPath = Constants.AudioFolderPath;
                                }
                                else
                                {
                                    parentPath = Path.Combine(Constants.AudioFolderPath, parentDisplay[parentSelected]);
                                }

                                Console.WriteLine("\nEnter new folder name:");
                                string newName = Console.ReadLine()?.Trim();
                                if (string.IsNullOrEmpty(newName))
                                {
                                    return null; // cancel
                                }
                                string newPath = Path.Combine(parentPath, newName);
                                Directory.CreateDirectory(newPath);
                                return newPath;
                            }
                        }
                    }

                    // Existing folder selected
                    string chosenRel = display[selected];
                    string chosenFull = Path.Combine(Constants.AudioFolderPath, chosenRel);
                    return chosenFull;
                }
            }
        }
    }
}
