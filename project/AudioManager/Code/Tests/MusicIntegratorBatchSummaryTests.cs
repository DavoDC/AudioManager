using System.Collections.Generic;

namespace AudioManager
{
    /// <summary>
    /// Coverage for the batch-level "summary" block MusicIntegrator.BuildJson emits alongside the
    /// per-file rows (docs/Development/IDEAS.md "Scan-ahead batch context is invisible"): the route
    /// distribution, the Misc auto-migration counts, the detected compilation albums, and the
    /// per-entry compilationAlbum flag. Pure string assertions against BuildJson - no filesystem,
    /// no console interaction.
    /// </summary>
    internal static class MusicIntegratorBatchSummaryTests
    {
        private static MusicIntegrator.LogEntry Entry(string filename, string destination) =>
            new MusicIntegrator.LogEntry { Filename = filename, Destination = destination };

        public static void BuildJson_RootIsAnObjectWithSummaryAndFiles()
        {
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry> { Entry("A.mp3", "Artists\\X\\A.mp3") });
            Assert.True(json.TrimStart().StartsWith("{"), "root is a JSON object, not a bare array");
            Assert.True(json.Contains("\"summary\": {"), "summary block present");
            Assert.True(json.Contains("\"files\": ["), "files array present");
            Assert.True(json.Contains("\"filename\": \"A.mp3\""), "per-file rows still emitted inside files[]");
        }

        public static void BuildJson_RouteDistributionCountsByTopLevelDestinationFolder()
        {
            var entries = new List<MusicIntegrator.LogEntry>
            {
                Entry("a.mp3", "Artists\\Jay-Z\\The Blueprint\\a.mp3"),
                Entry("b.mp3", "Artists\\Nas\\Singles\\b.mp3"),
                Entry("c.mp3", "Compilations\\Now 42\\c.mp3"),
            };
            string json = MusicIntegrator.BuildJson(entries);
            Assert.True(json.Contains("\"Artists\": 2"), "two files under Artists/ counted together");
            Assert.True(json.Contains("\"Compilations\": 1"), "Compilations counted separately");
        }

        public static void BuildJson_RouteDistributionSkipsEntriesWithNoDestination()
        {
            // Skipped and errored entries never get a destination - they must not pollute the
            // distribution with an "Unknown" bucket the GUI would then have to render.
            var entries = new List<MusicIntegrator.LogEntry>
            {
                Entry("a.mp3", "Artists\\Jay-Z\\a.mp3"),
                new MusicIntegrator.LogEntry { Filename = "bad.mp3", Status = "error", Destination = null },
                new MusicIntegrator.LogEntry { Filename = "skip.mp3", Status = "skipped", Destination = "" },
            };
            string json = MusicIntegrator.BuildJson(entries);
            Assert.True(json.Contains("\"Artists\": 1"), "only the routed file is counted");
            Assert.True(!json.Contains("\"Unknown\""), "no Unknown bucket from destination-less entries");
        }

        public static void BuildJson_MiscMigrationsEmittedWithPerArtistCountsAndTotal()
        {
            var summary = new MusicIntegrator.BatchSummary();
            summary.MiscAutoMigrations["Dizzy Wright"] = 2;
            summary.MiscAutoMigrations["Hopsin"] = 3;
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry>(), summary);
            Assert.True(json.Contains("{\"artist\": \"Hopsin\", \"count\": 3}"), "highest-count artist emitted");
            Assert.True(json.Contains("{\"artist\": \"Dizzy Wright\", \"count\": 2}"), "second artist emitted");
            Assert.True(json.Contains("\"miscAutoMigrationTotal\": 5"), "total is the sum of per-artist counts");
        }

        public static void BuildJson_NoSummary_EmitsEmptyBatchContextNotMissingKeys()
        {
            // The contract shape must be constant so the GUI never has to branch on key presence.
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry>());
            Assert.True(json.Contains("\"miscAutoMigrations\": []"), "empty migrations list still emitted");
            Assert.True(json.Contains("\"miscAutoMigrationTotal\": 0"), "zero total still emitted");
            Assert.True(json.Contains("\"compilationAlbums\": []"), "empty compilation list still emitted");
            Assert.True(json.Contains("\"routes\": {}"), "empty route distribution still emitted");
        }

        public static void BuildJson_ZeroCountMigrationsAreOmitted()
        {
            var summary = new MusicIntegrator.BatchSummary();
            summary.MiscAutoMigrations["Nobody"] = 0;
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry>(), summary);
            Assert.True(!json.Contains("Nobody"), "an artist with no Misc songs is not announced as migrating");
            Assert.True(json.Contains("\"miscAutoMigrationTotal\": 0"), "total stays zero");
        }

        public static void BuildJson_CompilationAlbumsListedInSummary()
        {
            var summary = new MusicIntegrator.BatchSummary();
            summary.CompilationAlbums.Add("Now That's What I Call Music 42");
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry>(), summary);
            Assert.True(json.Contains("\"compilationAlbums\": [\"Now That's What I Call Music 42\"]"),
                "detected compilation album named in the summary");
        }

        public static void BuildJson_PerEntryCompilationAlbumFlagSerializes()
        {
            var comp = new MusicIntegrator.LogEntry { Filename = "c.mp3", CompilationAlbum = true };
            var plain = new MusicIntegrator.LogEntry { Filename = "p.mp3", CompilationAlbum = false };
            Assert.True(MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry> { comp })
                        .Contains("\"compilationAlbum\": true"), "flag true for a compilation-album file");
            Assert.True(MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry> { plain })
                        .Contains("\"compilationAlbum\": false"), "flag false for an ordinary file");
        }

        public static void BuildJson_CompilationAlbumFlagIsIndependentOfInBatchDuplicate()
        {
            // Two separate signals on the same card - a regression that aliased them would show up here.
            var entry = new MusicIntegrator.LogEntry { Filename = "x.mp3", CompilationAlbum = true, InBatchDuplicate = false };
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry> { entry });
            Assert.True(json.Contains("\"compilationAlbum\": true"), "compilationAlbum true");
            Assert.True(json.Contains("\"inBatchDuplicate\": false"), "inBatchDuplicate untouched");
        }

        public static void BuildJson_SummaryStringsAreJsonEscaped()
        {
            var summary = new MusicIntegrator.BatchSummary();
            summary.CompilationAlbums.Add("A \"quoted\" album");
            summary.MiscAutoMigrations["Back\\slash"] = 1;
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry>(), summary);
            Assert.True(json.Contains("A \\\"quoted\\\" album"), "quotes escaped in album names");
            Assert.True(json.Contains("Back\\\\slash"), "backslashes escaped in artist names");
        }
    }
}
