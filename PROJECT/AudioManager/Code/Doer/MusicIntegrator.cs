using AudioManager.Code.Modules;
using System;
using System.IO;
using File = System.IO.File;
using TagLib;

namespace AudioManager
{
    /// <summary>
    /// Scans the NewMusic folder, validates files, and sorts them into the Audio library.
    /// </summary>
    internal class MusicIntegrator : Doer
    {
        /// <summary>
        /// Construct and run the music integrator
        /// </summary>
        public MusicIntegrator()
        {
            Console.WriteLine("\nIntegrating new music...");

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

                        // Determine primary artist via Track.ProcessProperty inside PrimaryArtist getter
                        string primaryArtist = track.PrimaryArtist;

                        // Skip if un-routable
                        if (track.Title.Equals("Missing") || track.Artists.Equals("Missing") || primaryArtist.Equals("Missing"))
                        {
                            Console.WriteLine($" - Skipped '{Path.GetFileName(sourcePath)}': missing required tag");
                            skippedCount++;
                            continue;
                        }

                        // Determine destination directory
                        string destDir;
                        if (!track.Genres.Equals("Missing") && track.Genres.IndexOf(Constants.MusivDir, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            destDir = Path.Combine(Constants.AudioFolderPath, Constants.MusivDir);
                        }
                        else if (!track.Genres.Equals("Missing") && track.Genres.IndexOf(Constants.MotivDir, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            destDir = Path.Combine(Constants.AudioFolderPath, Constants.MotivDir);
                        }
                        else
                        {
                            string artistFolder = Path.Combine(Constants.AudioFolderPath, Constants.ArtistsDir, primaryArtist);
                            if (Directory.Exists(artistFolder))
                            {
                                if (!track.Album.Equals("Missing") && !track.Album.Equals(primaryArtist))
                                {
                                    destDir = Path.Combine(artistFolder, track.Album);
                                }
                                else
                                {
                                    destDir = artistFolder;
                                }
                            }
                            else
                            {
                                destDir = Path.Combine(Constants.AudioFolderPath, Constants.MiscDir);
                            }
                        }

                        // Build destination filename
                        string sanitisedArtists = Reflector.SanitiseFilename(track.Artists);
                        string sanitisedTitle = Reflector.SanitiseFilename(track.Title);
                        string destFilename = sanitisedArtists + " - " + sanitisedTitle + ".mp3";
                        Directory.CreateDirectory(destDir);
                        string destPath = Path.Combine(destDir, destFilename);

                        if (File.Exists(destPath))
                        {
                            Console.WriteLine($" - Skipped '{Path.GetFileName(sourcePath)}': already exists at destination");
                            skippedCount++;
                            continue;
                        }

                        // Move file
                        File.Move(sourcePath, destPath);
                        movedCount++;

                        // Compute relative destination path for logging
                        string relativeDest = destPath;
                        if (destPath.StartsWith(Constants.AudioFolderPath, StringComparison.OrdinalIgnoreCase))
                        {
                            relativeDest = destPath.Substring(Constants.AudioFolderPath.Length);
                            if (relativeDest.StartsWith("\\") || relativeDest.StartsWith("/"))
                            {
                                relativeDest = relativeDest.Substring(1);
                            }
                        }

                        Console.WriteLine($" - Moved: '{destFilename}' to '{relativeDest}'");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($" - Skipped '{Path.GetFileName(sourcePath)}': error reading file ({ex.Message})");
                        skippedCount++;
                    }
                }

                Console.WriteLine($" - Done. Moved: {movedCount}, Skipped: {skippedCount}");
            }
            finally
            {
                FinishAndPrintTimeTaken();
            }
        }
    }
}
