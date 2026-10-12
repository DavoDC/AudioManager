using AudioManager.Code.Modules;
using System.Collections.Generic;
using System.IO;

namespace AudioManager
{
    /// <summary>
    /// Characterisation tests for INT-3 (shared RulesEngine). They pin what GetDestDir and
    /// LibChecker do TODAY on the rules the two must agree on, so the extraction into
    /// RulesEngine can be shown to change nothing. They go through the public entry points
    /// (TagFixer normalizer, GetDestDir, LibChecker), never through RulesEngine itself.
    /// </summary>
    internal static class RulesCharacterisationTests
    {
        private static string Norm(string album) =>
            TagFixer.StripAlbumSuffixes(TagFixer.RemoveParentheticals(album));

        private static TrackTag Tag(string relPath, string title, string artist, string album) =>
            new TrackTag(relPath, title, artist, album, "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");

        // ---- album-version normalizer ----

        public static void Rules_AlbumNormalizer_StripsVersionAndEditionMarkers()
        {
            Assert.Equal("The Blueprint", Norm("The Blueprint (Explicit Version)"), "explicit version");
            Assert.Equal("Life After Death", Norm("Life After Death (2014 Remastered Edition)"), "remastered edition");
            Assert.Equal("Album", Norm("Album (Deluxe Edition) (2019)"), "deluxe plus year");
            Assert.Equal("Album", Norm("Album (Deluxe)"), "deluxe bare");
            Assert.Equal("Ready to Die", Norm("Ready to Die (Bonus Tracks)"), "bonus tracks");
            Assert.Equal("Album", Norm("Album (Mono)"), "mono");
            Assert.Equal("Album", Norm("Album (feat. Someone)"), "feat parenthetical");
            Assert.Equal("Live", Norm("Live (Acoustic Version)"), "acoustic version");
        }

        public static void Rules_AlbumNormalizer_LeavesPlainAlbumsAndNulls()
        {
            Assert.Equal("Plain Album", Norm("Plain Album"), "plain unchanged");
            Assert.Equal("Greatest Hits (Volume 2)", Norm("Greatest Hits (Volume 2)"), "non-edition parenthetical kept");
            Assert.Equal("", Norm(""), "empty stays empty");
            Assert.True(Norm(null) == null, "null stays null");
        }

        // ---- GetDestDir album-folder matching uses the same normalizer ----

        public static void Rules_GetDestDir_MatchesVersionSuffixedLibraryFolder()
        {
            string lib = RoutingFixtures.CreateLibraryFixture(artistFolders: new[] { "Known Artist" });
            try
            {
                RoutingFixtures.AddAlbumFiles(lib, "Known Artist", "Life After Death (2014 Remastered Edition)", fileCount: 2);
                var integrator = new MusicIntegrator(lib);
                var track = new Track { Artists = "Known Artist", Title = "New One", Album = "Life After Death", Genres = "Missing" };
                string dest = integrator.GetDestDir(track, new HashSet<string>(), new HashSet<string>(), out _, out _);
                string expected = Path.Combine(lib, Constants.ArtistsDir, "Known Artist", "Life After Death (2014 Remastered Edition)");
                Assert.Equal(expected, dest, "clean album tag routes into the version-suffixed on-disk folder");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        // ---- LibChecker thresholds that mirror the routing thresholds ----

        public static void Rules_LibChecker_OneSongInSingles_IsClean()
        {
            var tag = Tag("\\Artists\\Artist Alpha\\Singles\\Artist Alpha - Song A.xml", "Song A", "Artist Alpha", "Real Album");
            Assert.True(new LibChecker(new List<TrackTag> { tag }).IsClean, "1 album song in Singles/ is the rule");
        }

        public static void Rules_LibChecker_CompilationWithTwoArtists_IsNotGenuine()
        {
            // Exactly 2 distinct primary artists is below the 3-artist compilation threshold.
            var folderTag = Tag("\\Artists\\Artist Alpha\\Singles\\Artist Alpha - Song A.xml", "Song A", "Artist Alpha", "Single Album");
            var c1 = Tag("\\Compilations\\Mix\\Artist Alpha - Song B.xml", "Song B", "Artist Alpha", "Mix");
            var c2 = Tag("\\Compilations\\Mix\\Artist Beta - Song C.xml", "Song C", "Artist Beta", "Mix");
            var checker = new LibChecker(new List<TrackTag> { folderTag, c1, c2 });
            Assert.True(!checker.IsClean, "2 distinct artists is not a genuine compilation: artist with a folder is flagged");
        }

        public static void Rules_LibChecker_AlbumGroupingUsesRawAlbumTag_KnownDivergence()
        {
            // KNOWN DIVERGENCE (recorded for David, not fixed by INT-3): GetDestDir counts album songs
            // by the NORMALISED album, LibChecker groups by the RAW album tag. Two library files whose
            // tags differ only by an edition suffix are 1+1 to LibChecker, so each is "only 1 song".
            var a = Tag("\\Artists\\Artist Alpha\\Real Album\\Artist Alpha - Song A.xml", "Song A", "Artist Alpha", "Real Album");
            var b = Tag("\\Artists\\Artist Alpha\\Real Album\\Artist Alpha - Song B.xml", "Song B", "Artist Alpha", "Real Album (Deluxe Edition)");
            var checker = new LibChecker(new List<TrackTag> { a, b });
            Assert.True(!checker.IsClean, "raw-album grouping flags suffix-differing album tags (pinned current behaviour)");
        }
    }
}
