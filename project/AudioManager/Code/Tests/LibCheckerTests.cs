using AudioManager.Code.Modules;
using System.Collections.Generic;

namespace AudioManager
{
    internal static class LibCheckerTests
    {
        // A fully-valid Artists/ track. RelPath mirrors real AudioMirror format: leading backslash
        // produces ["", "Artists", artist, subfolder, filename] when split by '\', matching the
        // index offsets that FilterTagsByMainFolder and CheckArtistFolder rely on (pos 1 = main folder).
        private static TrackTag ArtistTag(
            string artist = "Known Artist", string title = "Song A",
            string album = "Test Album", string genres = "Hip-Hop",
            string albumCoverCount = "1", string compilation = "True")
        {
            string filename = $"{artist} - {title}.xml";
            string relPath = $"\\Artists\\{artist}\\Singles\\{filename}";
            return new TrackTag(relPath, title, artist, album, "2020", "1", genres,
                "00:03:00.0000000", albumCoverCount, compilation, "500", "500");
        }

        // ---- IsClean baseline ----

        public static void LibChecker_CleanTag_IsClean()
        {
            var checker = new LibChecker(new List<TrackTag> { ArtistTag() });
            Assert.True(checker.IsClean, "One fully-valid artist tag should produce IsClean=true");
        }

        // ---- Per-rule dirty cases ----

        public static void LibChecker_CompilationNotSet_IsDirty()
        {
            var checker = new LibChecker(new List<TrackTag> { ArtistTag(compilation: "False") });
            Assert.True(!checker.IsClean, "Compilation=False (TCMP not set) should be dirty");
        }

        public static void LibChecker_MissingTitle_IsDirty()
        {
            var checker = new LibChecker(new List<TrackTag> { ArtistTag(title: "Missing") });
            Assert.True(!checker.IsClean, "Title='Missing' should be dirty");
        }

        public static void LibChecker_MissingArtists_IsDirty()
        {
            var checker = new LibChecker(new List<TrackTag> { ArtistTag(artist: "Missing") });
            Assert.True(!checker.IsClean, "Artists='Missing' should be dirty");
        }

        public static void LibChecker_NoAlbumCover_IsDirty()
        {
            var checker = new LibChecker(new List<TrackTag> { ArtistTag(albumCoverCount: "0") });
            Assert.True(!checker.IsClean, "AlbumCoverCount=0 should be dirty");
        }

        public static void LibChecker_MultipleAlbumCovers_IsDirty()
        {
            var checker = new LibChecker(new List<TrackTag> { ArtistTag(albumCoverCount: "3") });
            Assert.True(!checker.IsClean, "AlbumCoverCount=3 should be dirty");
        }

        public static void LibChecker_DuplicateTracks_IsDirty()
        {
            // Same title + artist in two different files = duplicate
            var tagA = new TrackTag("\\Artists\\Artist Alpha\\Singles\\Artist Alpha - Song A.xml",
                "Song A", "Artist Alpha", "Test Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "500", "500");
            var tagB = new TrackTag("\\Artists\\Artist Alpha\\Singles\\Artist Alpha - Song A (2).xml",
                "Song A", "Artist Alpha", "Test Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "500", "500");
            var checker = new LibChecker(new List<TrackTag> { tagA, tagB });
            Assert.True(!checker.IsClean, "Duplicate title+artist should be dirty");
        }

        public static void LibChecker_UnwantedFeatInTitle_IsDirty()
        {
            // Title not cleaned by TagFixer - LibChecker should catch this
            var tag = new TrackTag(
                "\\Artists\\Known Artist\\Singles\\Known Artist - Song (feat. Someone).xml",
                "Song (feat. Someone)", "Known Artist", "Test Album", "2020", "1",
                "Hip-Hop", "00:03:00.0000000", "1", "True", "500", "500");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(!checker.IsClean, "feat. remaining in title should be dirty");
        }

        public static void LibChecker_TwoAlbumSongsInSingles_IsDirty()
        {
            // 2+ songs from same album must be in an album subfolder, not Singles/
            var tagA = new TrackTag("\\Artists\\Artist Alpha\\Singles\\Artist Alpha - Song A.xml",
                "Song A", "Artist Alpha", "Real Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "500", "500");
            var tagB = new TrackTag("\\Artists\\Artist Alpha\\Singles\\Artist Alpha - Song B.xml",
                "Song B", "Artist Alpha", "Real Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "500", "500");
            var checker = new LibChecker(new List<TrackTag> { tagA, tagB });
            Assert.True(!checker.IsClean, "Two songs from same album in Singles/ should be dirty (use album subfolder)");
        }

        public static void LibChecker_OneSongInAlbumSubfolder_IsDirty()
        {
            // 1 song in album subfolder - should be in Singles/ instead
            var tag = new TrackTag("\\Artists\\Artist Alpha\\Real Album\\Artist Alpha - Song A.xml",
                "Song A", "Artist Alpha", "Real Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "500", "500");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(!checker.IsClean, "Single song in album subfolder (should be Singles/) should be dirty");
        }

        public static void LibChecker_MusivationGenreOutsideMusivationFolder_IsDirty()
        {
            // Musivation genre track sitting in Artists/ - should be in Musivation/
            var checker = new LibChecker(new List<TrackTag> { ArtistTag(genres: "Musivation") });
            Assert.True(!checker.IsClean, "Musivation genre outside Musivation/ should be dirty");
        }

        public static void LibChecker_MotivationGenreOutsideMotivationFolder_IsDirty()
        {
            // Motivation genre track sitting in Artists/ - should be in Motivation/
            var checker = new LibChecker(new List<TrackTag> { ArtistTag(genres: "Motivation") });
            Assert.True(!checker.IsClean, "Motivation genre outside Motivation/ should be dirty");
        }

        // ---- CheckArtistFolder ----

        public static void LibChecker_ArtistInWrongFolder_IsDirty()
        {
            // PrimaryArtist in tag doesn't match the folder name in the RelPath
            var tag = new TrackTag("\\Artists\\Wrong Artist\\Singles\\Correct Artist - Song A.xml",
                "Song A", "Correct Artist", "Test Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "500", "500");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(!checker.IsClean, "Artist in wrong folder (tag/folder mismatch) should be dirty");
        }

        // ---- CheckMiscFolder ----

        public static void LibChecker_MiscWithThreeArtistSongs_IsDirty()
        {
            // 3+ songs from same artist in Misc - should have their own Artists/ folder
            var tags = new List<TrackTag>
            {
                new TrackTag("\\Miscellaneous Songs\\Artist Beta - Song A.xml",
                    "Song A", "Artist Beta", "Test Album", "2020", "1", "Hip-Hop",
                    "00:03:00.0000000", "1", "True", "500", "500"),
                new TrackTag("\\Miscellaneous Songs\\Artist Beta - Song B.xml",
                    "Song B", "Artist Beta", "Test Album", "2020", "1", "Hip-Hop",
                    "00:03:00.0000000", "1", "True", "500", "500"),
                new TrackTag("\\Miscellaneous Songs\\Artist Beta - Song C.xml",
                    "Song C", "Artist Beta", "Test Album", "2020", "1", "Hip-Hop",
                    "00:03:00.0000000", "1", "True", "500", "500"),
            };
            var checker = new LibChecker(tags);
            Assert.True(!checker.IsClean, "3 songs from same artist in Misc should be dirty (needs Artists/ folder)");
        }

        public static void LibChecker_ArtistHasFolderButSongInMisc_IsDirty()
        {
            // Artist has an Artists/ folder but one song sits in Misc/ - routing gap
            var artistTag = new TrackTag("\\Artists\\Artist Alpha\\Singles\\Artist Alpha - Song A.xml",
                "Song A", "Artist Alpha", "Test Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "500", "500");
            var miscTag = new TrackTag("\\Miscellaneous Songs\\Artist Alpha - Song B.xml",
                "Song B", "Artist Alpha", "Test Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "500", "500");
            var checker = new LibChecker(new List<TrackTag> { artistTag, miscTag });
            Assert.True(!checker.IsClean, "Artist with folder but song in Misc should be dirty");
        }

        // ---- CheckSourcesFolder ----

        public static void LibChecker_SourcesFolderMovieAlbumWithoutOST_IsDirty()
        {
            // Album mentions the source folder name but doesn't include "OST"
            var tag = new TrackTag("\\Sources\\Films\\Peacemaker\\Artist - Song A.xml",
                "Song A", "Artist", "Peacemaker Adventures", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "500", "500");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(!checker.IsClean, "Sources film album mentioning movie name without OST should be dirty");
        }

        public static void LibChecker_SourcesFolderMovieAlbumWithOST_IsClean()
        {
            // Album mentions the source folder name AND includes "OST" - correct
            var tag = new TrackTag("\\Sources\\Films\\Peacemaker\\Artist - Song A.xml",
                "Song A", "Artist", "Peacemaker OST", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "500", "500");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(checker.IsClean, "Sources film album with OST in name should be clean");
        }
    }
}
