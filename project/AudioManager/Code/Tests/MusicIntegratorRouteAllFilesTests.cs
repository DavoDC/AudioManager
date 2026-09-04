using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AudioManager.Code.Modules;

namespace AudioManager
{
    /// <summary>
    /// Regression coverage for RouteAllFiles's "one bad file must not abort the whole batch"
    /// behavior (previously it threw and halted, per docs/Development/IDEAS.md 2026-09-04).
    /// Uses the test-only MusicIntegrator(testLibraryPath) constructor and real temp
    /// files/dirs - never touches the real Constants.AudioFolderPath library.
    /// </summary>
    internal static class MusicIntegratorRouteAllFilesTests
    {
        private static MusicIntegrator.ScannedFile MakeGoodFile(string sourcePath, string artist, string title)
        {
            File.WriteAllBytes(sourcePath, new byte[0]);
            var track = new Track { Artists = artist, Title = title, Album = "Missing", Genres = "Missing" };
            return new MusicIntegrator.ScannedFile
            {
                SourcePath = sourcePath,
                Track = track,
                IsReadable = true,
                LogEntry = new MusicIntegrator.LogEntry { Filename = Path.GetFileName(sourcePath), Title = title, Artists = artist }
            };
        }

        public static void RouteAllFiles_UnreadableFile_IsSkippedNotThrown_RestOfBatchStillRoutes()
        {
            string lib = RoutingFixtures.CreateLibraryFixture();
            string newMusic = Path.Combine(Path.GetTempPath(), "AudioManagerTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(newMusic);
            try
            {
                var integrator = new MusicIntegrator(lib);

                string goodPath = Path.Combine(newMusic, "Good Artist - Good Song.mp3");
                var good = MakeGoodFile(goodPath, "Good Artist", "Good Song");

                string badPath = Path.Combine(newMusic, "Bad Artist - Bad Song.mp3");
                var bad = new MusicIntegrator.ScannedFile
                {
                    SourcePath = badPath,
                    IsReadable = false,
                    ReadError = "simulated unreadable tag data",
                    LogEntry = new MusicIntegrator.LogEntry { Filename = Path.GetFileName(badPath) }
                };

                var scannedFiles = new List<MusicIntegrator.ScannedFile> { bad, good };
                var logEntries = new List<MusicIntegrator.LogEntry>();
                int moved = 0, skipped = 0;

                integrator.RouteAllFiles(scannedFiles, new HashSet<string>(), logEntries, ref moved, ref skipped);

                Assert.Equal("1", moved.ToString(), "unreadable file must not block the good file from moving");
                Assert.Equal("1", skipped.ToString(), "unreadable file counts as skipped, not a thrown exception");
                Assert.Equal("error", bad.LogEntry.Status, "unreadable file's log entry must be marked error");
                Assert.True(!File.Exists(goodPath), "good file should have moved out of NewMusic");
                string expectedDest = Path.Combine(lib, Constants.MiscDir, "Good Artist - Good Song.mp3");
                Assert.True(File.Exists(expectedDest), "good file should have arrived at its destination");
            }
            finally
            {
                RoutingFixtures.Cleanup(lib);
                if (Directory.Exists(newMusic)) Directory.Delete(newMusic, recursive: true);
            }
        }

        public static void RouteAllFiles_UnexpectedMoveException_IsSkippedNotThrown_RestOfBatchStillRoutes()
        {
            string lib = RoutingFixtures.CreateLibraryFixture();
            string newMusic = Path.Combine(Path.GetTempPath(), "AudioManagerTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(newMusic);
            try
            {
                // "Bad Artist" has no artist folder, so GetDestDir routes it to lib/Miscellaneous Songs.
                // Pre-creating a FILE at that exact path makes Directory.CreateDirectory(destDir) throw
                // a real IOException during routing - a deterministic, realistic move-time failure.
                string miscAsFile = Path.Combine(lib, Constants.MiscDir);
                File.WriteAllBytes(miscAsFile, new byte[0]);

                var integrator = new MusicIntegrator(lib);

                string badPath = Path.Combine(newMusic, "Bad Artist - Bad Song.mp3");
                var bad = MakeGoodFile(badPath, "Bad Artist", "Bad Song");

                var scannedFiles = new List<MusicIntegrator.ScannedFile> { bad };
                var logEntries = new List<MusicIntegrator.LogEntry>();
                int moved = 0, skipped = 0;

                integrator.RouteAllFiles(scannedFiles, new HashSet<string>(), logEntries, ref moved, ref skipped);

                Assert.Equal("0", moved.ToString(), "the failing file itself must not count as moved");
                Assert.Equal("1", skipped.ToString(), "move-time exception must be caught and counted as skipped, not thrown");
                Assert.Equal("error", bad.LogEntry.Status, "move failure must be recorded as an error log entry");
                Assert.True(File.Exists(badPath), "source file must be left in place when the move fails");
            }
            finally
            {
                RoutingFixtures.Cleanup(lib);
                if (Directory.Exists(newMusic)) Directory.Delete(newMusic, recursive: true);
            }
        }

        public static void RouteAllFiles_OneBadFileAmongMany_DoesNotStrandLaterGoodFiles()
        {
            string lib = RoutingFixtures.CreateLibraryFixture();
            string newMusic = Path.Combine(Path.GetTempPath(), "AudioManagerTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(newMusic);
            try
            {
                var integrator = new MusicIntegrator(lib);

                var before = MakeGoodFile(Path.Combine(newMusic, "Before - Track.mp3"), "Before", "Track");
                var badPath = Path.Combine(newMusic, "Broken - Track.mp3");
                var bad = new MusicIntegrator.ScannedFile
                {
                    SourcePath = badPath,
                    IsReadable = false,
                    ReadError = "simulated read failure",
                    LogEntry = new MusicIntegrator.LogEntry { Filename = Path.GetFileName(badPath) }
                };
                var after = MakeGoodFile(Path.Combine(newMusic, "After - Track.mp3"), "After", "Track");

                var scannedFiles = new List<MusicIntegrator.ScannedFile> { before, bad, after };
                var logEntries = new List<MusicIntegrator.LogEntry>();
                int moved = 0, skipped = 0;

                integrator.RouteAllFiles(scannedFiles, new HashSet<string>(), logEntries, ref moved, ref skipped);

                // This is the exact bug reported by David: a track positioned AFTER a bad file
                // was previously left NOT RUN because the bad file aborted the whole batch.
                Assert.Equal("2", moved.ToString(), "both the file before and after the bad one must move");
                Assert.Equal("1", skipped.ToString(), "exactly the bad file is skipped");
                Assert.True(logEntries.Any(e => e.Status == "moved" && e.Filename == "After - Track.mp3"),
                    "the file after the bad one must reach 'moved', not be stranded as not-run");
            }
            finally
            {
                RoutingFixtures.Cleanup(lib);
                if (Directory.Exists(newMusic)) Directory.Delete(newMusic, recursive: true);
            }
        }
    }
}
