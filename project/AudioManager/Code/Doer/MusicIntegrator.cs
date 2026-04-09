using AudioManager.Code.Modules;
using System;
using System.IO;
using System.Linq;
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

            try
            {
                var files = Directory.Exists(Constants.NewMusicPath)
                    ? Directory.GetFiles(Constants.NewMusicPath, "*.mp3", SearchOption.AllDirectories)
                    : Array.Empty<string>();

                if (files.Length == 0)
                {
                    Console.WriteLine(" - Nothing to integrate!");
                    return;
                }

                foreach (var sourcePath in files)
                {
                    try
                    {
                        // Read tags directly from file
                        TagLib.File tagFile = TagLib.File.Create(sourcePath);
                        Tag tag = tagFile.Tag;

                        // Populate Track object
                        Track track = new Track();
                        track.Title = string.IsNullOrEmpty(tag.Title) ? "Missing" : tag.Title;
                        track.Artists = string.IsNullOrEmpty(tag.JoinedPerformers) ? "Missing" : tag.JoinedPerformers;
                        track.Album = string.IsNullOrEmpty(tag.Album) ? "Missing" : tag.Album;
                        track.Genres = string.IsNullOrEmpty(tag.JoinedGenres) ? "Missing" : tag.JoinedGenres;
                        track.Year = (tag.Year == 0) ? "Missing" : tag.Year.ToString();

                        // Determine primary artist via Track.ProcessProperty inside PrimaryArtist getter
                        string primaryArtist = track.PrimaryArtist;

                        // Skip if un-routable
                        if (track.Title.Equals("Missing") || track.Artists.Equals("Missing") || primaryArtist.Equals("Missing"))
                        {
                            Console.WriteLine($" - Skipped '{Path.GetFileName(sourcePath)}': missing required tag");
                            skippedCount++;
                            continue;
                        }

                        // Build destination filename
                        string sanitisedArtists = Reflector.SanitiseFilename(track.Artists);
                        string sanitisedTitle = Reflector.SanitiseFilename(track.Title);
                        string destFilename = sanitisedArtists + " - " + sanitisedTitle + ".mp3";

                        // Determine destination directory and reason
                        string destDir = GetDestDir(track, out string reason);

                        // Ensure destination directory exists for relative path computation
                        string destPath = Path.Combine(destDir, destFilename);

                        // Compute relative destination path for display
                        string relativeDest = destPath;
                        if (destPath.StartsWith(Constants.AudioFolderPath, StringComparison.OrdinalIgnoreCase))
                        {
                            relativeDest = destPath.Substring(Constants.AudioFolderPath.Length);
                            if (relativeDest.StartsWith("\\") || relativeDest.StartsWith("/"))
                            {
                                relativeDest = relativeDest.Substring(1);
                            }
                        }

                        // Interactive screen
                        Console.Clear();
                        Console.WriteLine("============================================================");
                        Console.WriteLine($"  {track.Artists} - {track.Title}");
                        Console.WriteLine("============================================================\n");
                        Console.WriteLine($"  Album:   {track.Album}");
                        Console.WriteLine($"  Year:    {track.Year}");
                        Console.WriteLine($"  Genres:  {track.Genres}");
                        Console.WriteLine($"\n  Proposed: {relativeDest}");
                        Console.WriteLine($"  Reason:   {reason}");
                        Console.WriteLine("\n------------------------------------------------------------");

                        if (dryRun)
                        {
                            // Dry run: print planned action, no file move
                            Console.WriteLine($"  [DRY RUN] Would move to: {relativeDest}");
                            movedCount++;
                        }
                        else
                        {
                            Console.WriteLine("  [Y] Accept   [N] Choose folder   [Q] Quit");
                            Console.WriteLine("------------------------------------------------------------");

                            // Wait for input
                            while (true)
                            {
                                var key = Console.ReadKey(intercept: true).Key;
                                if (key == ConsoleKey.Y)
                                {
                                    // Accept: move
                                    Directory.CreateDirectory(destDir);
                                    File.Move(sourcePath, destPath);
                                    movedCount++;
                                    Console.WriteLine($"  Moved to: {relativeDest}");
                                    Console.Clear();
                                    break;
                                }
                                else if (key == ConsoleKey.Q)
                                {
                                    Console.WriteLine("\n - Quit. Remaining files left for next run.");
                                    return; // exits foreach, hits finally
                                }
                                else if (key == ConsoleKey.N)
                                {
                                    // Folder picker
                                    string chosen = PickFolder();
                                    if (string.IsNullOrEmpty(chosen))
                                    {
                                        // cancelled selection, redisplay prompt
                                        Console.WriteLine("  Folder selection cancelled.");
                                        continue;
                                    }

                                    string newDestDir = chosen;
                                    string newDestPath = Path.Combine(newDestDir, destFilename);
                                    Directory.CreateDirectory(newDestDir);
                                    if (File.Exists(newDestPath))
                                    {
                                        Console.WriteLine($"  - Skipped '{Path.GetFileName(sourcePath)}': already exists at destination");
                                        skippedCount++;
                                    }
                                    else
                                    {
                                        File.Move(sourcePath, newDestPath);
                                        movedCount++;
                                        string rel = newDestPath;
                                        if (newDestPath.StartsWith(Constants.AudioFolderPath, StringComparison.OrdinalIgnoreCase))
                                        {
                                            rel = newDestPath.Substring(Constants.AudioFolderPath.Length);
                                            if (rel.StartsWith("\\") || rel.StartsWith("/")) rel = rel.Substring(1);
                                        }
                                        Console.WriteLine($"  Moved to: {rel}");
                                        Console.Clear();
                                    }

                                    break;
                                }
                                // ignore other keys
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($" - Skipped '{Path.GetFileName(sourcePath)}': error reading file ({ex.Message})");
                        skippedCount++;
                    }
                }

                Console.Clear();
                Console.WriteLine("============================================================");
                Console.WriteLine(dryRun ? "  Dry Run complete (no files moved)" : "  Integration complete");
                Console.WriteLine("============================================================\n");
                Console.WriteLine(dryRun ? $"  Would move: {movedCount}" : $"  Moved:   {movedCount}");
                Console.WriteLine($"  Skipped: {skippedCount}");
                Console.WriteLine("\n------------------------------------------------------------");
            }
            finally
            {
                FinishAndPrintTimeTaken();
            }
        }

        /// <summary>
        /// Determines the destination directory for a track based on its metadata.
        /// </summary>
        /// <param name="track">The track to route.</param>
        /// <param name="reason">Output: human-readable reason for the proposed destination.</param>
        /// <returns>The full destination directory path.</returns>
        private string GetDestDir(Track track, out string reason)
        {
            // Genres-based rules
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

            // Artist folder rules
            string primaryArtist = track.PrimaryArtist;
            string artistFolder = Path.Combine(Constants.AudioFolderPath, Constants.ArtistsDir, primaryArtist);
            if (Directory.Exists(artistFolder))
            {
                if (!track.Album.Equals("Missing") && !track.Album.Equals(primaryArtist))
                {
                    reason = "Artist folder exists; routed into album subfolder";
                    return Path.Combine(artistFolder, track.Album);
                }
                else
                {
                    reason = "Artist folder exists; no distinct album";
                    return artistFolder;
                }
            }

            reason = "No artist folder found in library";
            return Path.Combine(Constants.AudioFolderPath, Constants.MiscDir);
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
                string rel = d.Substring(Constants.AudioFolderPath.Length);
                if (rel.StartsWith("\\") || rel.StartsWith("/")) rel = rel.Substring(1);
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
