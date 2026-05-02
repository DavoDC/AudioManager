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

                        // Dry-run: simulate post-TagFixer state so routing and display use cleaned tags.
                        // In real mode TagFixer has already written to disk; in dry-run it hasn't,
                        // so we apply the same transforms in-memory (feat. removal, casing, genre).
                        if (dryRun && !track.Title.Equals("Missing") && !track.Artists.Equals("Missing"))
                        {
                            string rawTitle = track.Title; // keep for feat. extraction before title is cleaned
                            var simArtistList = TagFixer.ExtractAndFixArtists(rawTitle, track.Artists);
                            track.Title = TagFixer.RemoveParentheticals(rawTitle);
                            track.Artists = string.Join(";", simArtistList);
                            if (!track.Album.Equals("Missing"))
                                track.Album = TagFixer.RemoveParentheticals(track.Album);
                            if (TagFixer.ShouldFixGenre(track.Artists, track.Genres))
                                track.Genres = TagFixer.DetermineGenre(track.Artists);
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

                            // Show duplicate - same style as routing proposals (no Console.Clear, timestamps, relative paths)
                            // relLibraryPath: relative to Audio folder (for the MP3 in library)
                            // relMirrorPath: relative to AUDIO_MIRROR (for the XML in AudioMirror)
                            string relLibraryPath = libraryFilePath.StartsWith(Constants.AudioFolderPath, StringComparison.OrdinalIgnoreCase)
                                ? libraryFilePath.Substring(Constants.AudioFolderPath.Length).TrimStart('\\', '/')
                                : libraryFilePath;
                            string relMirrorPath = duplicatePath.StartsWith(Constants.MirrorFolderPath, StringComparison.OrdinalIgnoreCase)
                                ? duplicatePath.Substring(Constants.MirrorFolderPath.Length).TrimStart('\\', '/')
                                : Path.GetFileName(duplicatePath);
                            string relNewPath = Path.GetFileName(sourcePath);

                            // Album preference rules - recommend [L] (replace library) when:
                            // 1. Library has single + new file has real album (album version preferred over single)
                            // 2. Library has compilation track + new file has artist album (artist album preferred over compilation)
                            bool libraryIsSingle = relLibraryPath.IndexOf("\\Singles\\", StringComparison.OrdinalIgnoreCase) >= 0
                                                || relLibraryPath.StartsWith("Singles\\", StringComparison.OrdinalIgnoreCase);
                            bool libraryIsCompilation = relMirrorPath.StartsWith("Compilations\\", StringComparison.OrdinalIgnoreCase)
                                                     || relMirrorPath.StartsWith("Compilations/", StringComparison.OrdinalIgnoreCase);
                            bool newIsAlbum = !string.IsNullOrEmpty(track.Album)
                                           && !track.Album.Equals("Missing", StringComparison.OrdinalIgnoreCase)
                                           && !track.Album.Equals(track.Title, StringComparison.OrdinalIgnoreCase);

                            string dupReason = "";
                            char recommendedKey = '\0';
                            if (libraryIsSingle && newIsAlbum)
                            {
                                recommendedKey = 'L';
                                dupReason = $"Library has single; new file is from album '{track.Album}' - album preferred";
                            }
                            else if (libraryIsCompilation && newIsAlbum)
                            {
                                recommendedKey = 'L';
                                dupReason = $"Library has compilation track; new file is from artist album '{track.Album}' - artist album preferred";
                            }

                            // Build options line: recommended option is leftmost with "(recommended)" label
                            string optD = "[D] Delete NewMusic copy (keep library)";
                            string optL = "[L] Delete library copy (keep new file)";
                            string optK = "[K] Keep both";
                            string optQ = "[Q] Quit";
                            if (recommendedKey == 'L') optL += " (recommended)";
                            else if (recommendedKey == 'D') optD += " (recommended)";
                            string optionsLine = recommendedKey == 'L'
                                ? $"  {optL}   {optD}   {optK}   {optQ}"
                                : $"  {optD}   {optL}   {optK}   {optQ}";

                            // Corrected display filename from cleaned tags (tags already cleaned by TagFixer or dry-run simulation)
                            string displayNewFilename = $"{track.Artists} - {track.Title}.mp3";

                            // Proposed action summary based on recommendation
                            string dupProposed = recommendedKey == 'L'
                                ? "Delete library copy, keep new file (album version preferred)"
                                : recommendedKey == 'D'
                                    ? "Delete NewMusic copy, keep library version"
                                    : "No version preference - choose D or L based on quality";

                            Console.WriteLine();
                            PrintTimestamped("============================================================");
                            PrintTimestamped("  DUPLICATE FOUND");
                            PrintTimestamped("============================================================");
                            Console.WriteLine();
                            PrintTimestamped($"  In AudioMirror: {relMirrorPath}");
                            Console.WriteLine();
                            PrintTimestamped($"  New file:   {displayNewFilename}");
                            PrintTimestamped($"  Track:      {track.Artists} - {track.Title}");
                            PrintTimestamped($"  Album:      {track.Album}");
                            Console.WriteLine();
                            PrintTimestamped($"  Proposed:   {dupProposed}");
                            if (!string.IsNullOrEmpty(dupReason))
                                PrintTimestamped($"  Reason:     {dupReason}");
                            Console.WriteLine();
                            PrintTimestamped("------------------------------------------------------------");
                            PrintTimestamped(optionsLine);
                            PrintTimestamped("------------------------------------------------------------");

                            // Wait for input
                            while (true)
                            {
                                var key = ReadMenuKey();
                                if (key == ConsoleKey.D)
                                {
                                    // Delete from NewMusic
                                    string dReason = "User kept library copy" + (recommendedKey == 'L' ? $" (overrode [L]: {dupReason})" : "");
                                    decisionLog.LogDecision(track, Path.GetFileName(sourcePath), "duplicate-kept-library", dReason);
                                    if (dryRun)
                                    {
                                        PrintTimestamped($"  [DRY RUN] Would delete from NewMusic: {relNewPath}");
                                        entry.Status = "would-delete";
                                        entry.Detail = "duplicate (would delete)";
                                        logEntries.Add(entry); skippedCount++;
                                    }
                                    else
                                    {
                                        File.Delete(sourcePath);
                                        PrintTimestamped($"  Deleted from NewMusic: {relNewPath}");
                                        entry.Status = "deleted";
                                        entry.Detail = "duplicate (deleted)";
                                        logEntries.Add(entry); skippedCount++;
                                    }
                                    Console.WriteLine();
                                    break;
                                }
                                else if (key == ConsoleKey.L)
                                {
                                    // Delete from Library (replace old with new album version)
                                    string lReason = !string.IsNullOrEmpty(dupReason) ? dupReason : "User chose to replace library copy";
                                    decisionLog.LogDecision(track, Path.GetFileName(sourcePath), "duplicate-replaced-library", lReason);
                                    if (dryRun)
                                    {
                                        PrintTimestamped($"  [DRY RUN] Would delete from library: {relLibraryPath}");
                                        PrintTimestamped($"  [DRY RUN] Would keep new file: {displayNewFilename}");
                                        entry.Status = "would-replace";
                                        entry.Detail = "duplicate (would replace)";
                                        logEntries.Add(entry); skippedCount++;
                                        Console.WriteLine();
                                        break;
                                    }
                                    else
                                    {
                                        if (File.Exists(libraryFilePath))
                                        {
                                            File.Delete(libraryFilePath);
                                            PrintTimestamped($"  Deleted from library: {relLibraryPath}");
                                            PrintTimestamped($"  Integrating new version: {relNewPath}");
                                            entry.Status = "replaced";
                                            entry.Detail = "duplicate (library replaced)";
                                            Console.WriteLine();
                                            // Fall through to integration
                                        }
                                        else
                                        {
                                            PrintTimestamped($"  [WARN] Library file not found: {relLibraryPath}");
                                            entry.Status = "error";
                                            entry.Detail = "duplicate (library file not found)";
                                            logEntries.Add(entry); skippedCount++;
                                            Console.WriteLine();
                                        }
                                        break;
                                    }
                                }
                                else if (key == ConsoleKey.K)
                                {
                                    // Keep and continue
                                    PrintTimestamped("  Keeping both - proceeding with integration");
                                    Console.WriteLine();
                                    break;
                                }
                                else if (key == ConsoleKey.Q)
                                {
                                    PrintTimestamped("  Quit. Remaining files left for next run.");
                                    entry.Status = "quit"; logEntries.Add(entry);
                                    return; // exits foreach, hits finally
                                }
                                // ignore other keys
                            }

                            // If we deleted from NewMusic, or dry-run chose [L] (would-replace), skip to next file
                            if (entry.Status == "deleted" || entry.Status == "would-delete" || entry.Status == "would-replace")
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

                        // All routes require confirmation
                        PrintTimestamped("  [Y] Accept   [N] Decline   [Q] Quit");
                        PrintTimestamped("------------------------------------------------------------");

                        while (true)
                        {
                            var key = ReadMenuKey();
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
                                // Decline this file - leave in NewMusic for next run
                                if (dryRun)
                                {
                                    PrintTimestamped("  [DRY RUN] Would decline (leave in NewMusic)");
                                    decisionLog.LogDecision(track, Path.GetFileName(sourcePath), "declined", "User declined routing");
                                    entry.Status = "would-decline";
                                    entry.Detail = "user declined";
                                    logEntries.Add(entry); skippedCount++;
                                }
                                else
                                {
                                    PrintTimestamped("  Declined. File left in NewMusic for next run.");
                                    decisionLog.LogDecision(track, Path.GetFileName(sourcePath), "declined", "User declined routing");
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
                // Extract relative path from AUDIO_MIRROR folder (MirrorFolderPath already points to AUDIO_MIRROR)
                string mirrorBaseFolder = Constants.MirrorFolderPath;
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

                    // Use existing library folder casing if one already exists (e.g. "Scott Adams" not "Scott adams")
                    string peopleParent = Path.Combine(Constants.AudioFolderPath, Constants.MusivDir, "Akira The Don", "People");
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
                        string peopleFolder = Path.Combine(peopleParent, sampledPerson);

                        // Within People/{person}/, apply same album-vs-singles rule as Artists/ routing
                        if (!track.Album.Equals("Missing") && !track.Album.Equals(primaryArtist, StringComparison.OrdinalIgnoreCase))
                        {
                            int albumCount = CountAkiraTheDonPersonAlbumSongs(sampledPerson, track.Album);
                            if (albumCount >= 2)
                            {
                                reason = $"Akira The Don -> People/{sampledPerson}/{track.Album} ({albumCount} songs from album)";
                                return Path.Combine(peopleFolder, track.Album);
                            }
                            else
                            {
                                reason = $"Akira The Don -> People/{sampledPerson}/{Constants.SinglesDir} ({personSongCount} songs, only {albumCount} from album)";
                                return Path.Combine(peopleFolder, Constants.SinglesDir);
                            }
                        }
                        else
                        {
                            reason = $"Akira The Don -> People/{sampledPerson}/{Constants.SinglesDir} ({personSongCount} songs, no distinct album)";
                            return Path.Combine(peopleFolder, Constants.SinglesDir);
                        }
                    }
                    else
                    {
                        reason = $"Akira The Don -> Singles ({personSongCount} songs from {sampledPerson})";
                        return Path.Combine(Constants.AudioFolderPath, Constants.MusivDir, "Akira The Don", Constants.SinglesDir);
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
                Constants.AudioFolderPath, Constants.MusivDir, "Akira The Don", "People", sampledPerson, album);
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
                Console.Write("             > ");
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
