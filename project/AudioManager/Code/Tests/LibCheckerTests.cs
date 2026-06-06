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
                "00:03:00.0000000", albumCoverCount, compilation, "1200", "1200");
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
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var tagB = new TrackTag("\\Artists\\Artist Alpha\\Singles\\Artist Alpha - Song A (2).xml",
                "Song A", "Artist Alpha", "Test Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var checker = new LibChecker(new List<TrackTag> { tagA, tagB });
            Assert.True(!checker.IsClean, "Duplicate title+artist should be dirty");
        }

        public static void LibChecker_UnwantedFeatInTitle_IsDirty()
        {
            // Title not cleaned by TagFixer - LibChecker should catch this
            var tag = new TrackTag(
                "\\Artists\\Known Artist\\Singles\\Known Artist - Song (feat. Someone).xml",
                "Song (feat. Someone)", "Known Artist", "Test Album", "2020", "1",
                "Hip-Hop", "00:03:00.0000000", "1", "True", "1200", "1200");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(!checker.IsClean, "feat. remaining in title should be dirty");
        }

        public static void LibChecker_RadioEditInTitle_IsDirty()
        {
            // "(Radio Edit)" not stripped - should still be flagged
            var tag = ArtistTag(title: "Some Song (Radio Edit)");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(!checker.IsClean, "(Radio Edit) remaining in title should be dirty");
        }

        public static void LibChecker_AlbumWithEditionWord_IsClean()
        {
            // "Edition" in album name contains "edit" as substring - must NOT be flagged
            var tag = ArtistTag(album: "25th Anniversary Deluxe Edition");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(checker.IsClean, "album with 'Edition' word should be clean (not a false positive)");
        }

        public static void LibChecker_TwoAlbumSongsInSingles_IsDirty()
        {
            // 2+ songs from same album must be in an album subfolder, not Singles/
            var tagA = new TrackTag("\\Artists\\Artist Alpha\\Singles\\Artist Alpha - Song A.xml",
                "Song A", "Artist Alpha", "Real Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var tagB = new TrackTag("\\Artists\\Artist Alpha\\Singles\\Artist Alpha - Song B.xml",
                "Song B", "Artist Alpha", "Real Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var checker = new LibChecker(new List<TrackTag> { tagA, tagB });
            Assert.True(!checker.IsClean, "Two songs from same album in Singles/ should be dirty (use album subfolder)");
        }

        public static void LibChecker_OneSongInAlbumSubfolder_IsDirty()
        {
            // 1 song in album subfolder - should be in Singles/ instead
            var tag = new TrackTag("\\Artists\\Artist Alpha\\Real Album\\Artist Alpha - Song A.xml",
                "Song A", "Artist Alpha", "Real Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
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
                "00:03:00.0000000", "1", "True", "1200", "1200");
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
                    "00:03:00.0000000", "1", "True", "1200", "1200"),
                new TrackTag("\\Miscellaneous Songs\\Artist Beta - Song B.xml",
                    "Song B", "Artist Beta", "Test Album", "2020", "1", "Hip-Hop",
                    "00:03:00.0000000", "1", "True", "1200", "1200"),
                new TrackTag("\\Miscellaneous Songs\\Artist Beta - Song C.xml",
                    "Song C", "Artist Beta", "Test Album", "2020", "1", "Hip-Hop",
                    "00:03:00.0000000", "1", "True", "1200", "1200"),
            };
            var checker = new LibChecker(tags);
            Assert.True(!checker.IsClean, "3 songs from same artist in Misc should be dirty (needs Artists/ folder)");
        }

        public static void LibChecker_ArtistHasFolderButSongInMisc_IsDirty()
        {
            // Artist has an Artists/ folder but one song sits in Misc/ - routing gap
            var artistTag = new TrackTag("\\Artists\\Artist Alpha\\Singles\\Artist Alpha - Song A.xml",
                "Song A", "Artist Alpha", "Test Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var miscTag = new TrackTag("\\Miscellaneous Songs\\Artist Alpha - Song B.xml",
                "Song B", "Artist Alpha", "Test Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var checker = new LibChecker(new List<TrackTag> { artistTag, miscTag });
            Assert.True(!checker.IsClean, "Artist with folder but song in Misc should be dirty");
        }

        // ---- CheckSourcesFolder ----

        public static void LibChecker_SourcesFolderMovieAlbumWithoutOST_IsDirty()
        {
            // Album mentions the source folder name but doesn't include "OST"
            var tag = new TrackTag("\\Sources\\Films\\Peacemaker\\Artist - Song A.xml",
                "Song A", "Artist", "Peacemaker Adventures", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(!checker.IsClean, "Sources film album mentioning movie name without OST should be dirty");
        }

        public static void LibChecker_SourcesFolderMovieAlbumWithOST_IsClean()
        {
            // Album mentions the source folder name AND includes "OST" - correct
            var tag = new TrackTag("\\Sources\\Films\\Peacemaker\\Artist - Song A.xml",
                "Song A", "Artist", "Peacemaker OST", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(checker.IsClean, "Sources film album with OST in name should be clean");
        }

        // ---- CheckArtistFolder: loose file ----

        public static void LibChecker_LooseFileInArtistRoot_IsDirty()
        {
            // Song directly in artist folder (no Singles/ or album subfolder) - violates subfolder-before-song rule
            var tag = new TrackTag("\\Artists\\Artist Alpha\\Artist Alpha - Song A.xml",
                "Song A", "Artist Alpha", "Test Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(!checker.IsClean, "Song directly in artist root (no subfolder) should be dirty");
        }

        public static void LibChecker_LooseFileInArtistRoot_SelfTitledAlbum_IsClean()
        {
            // Song in artist root where album name matches artist name (self-titled) - intentionally skipped
            // This covers the edge case: Artists/Band/Band - Song.xml where Album="Band"
            var tag = new TrackTag("\\Artists\\Artist Alpha\\Artist Alpha - Song A.xml",
                "Song A", "Artist Alpha", "Artist Alpha", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(checker.IsClean, "Self-titled album in artist root should be skipped (clean)");
        }

        // ---- Album subfolder rule: clean baseline ----

        public static void LibChecker_TwoSongsInAlbumSubfolder_IsClean()
        {
            // 2 songs from same album both in the album subfolder (not Singles/) - correct placement
            var tagA = new TrackTag("\\Artists\\Artist Alpha\\Great Album\\Artist Alpha - Song A.xml",
                "Song A", "Artist Alpha", "Great Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var tagB = new TrackTag("\\Artists\\Artist Alpha\\Great Album\\Artist Alpha - Song B.xml",
                "Song B", "Artist Alpha", "Great Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var checker = new LibChecker(new List<TrackTag> { tagA, tagB });
            Assert.True(checker.IsClean, "2 songs from same album in album subfolder should be clean");
        }

        // ---- Genre-vs-folder: clean baselines ----

        public static void LibChecker_MusivationFolderWithMusivationGenre_IsClean()
        {
            // Track in Musivation/ with Musivation genre - correct placement
            var tag = new TrackTag("\\Musivation\\Some Artist\\Singles\\Some Artist - Song A.xml",
                "Song A", "Some Artist", "Test Album", "2020", "1", "Musivation",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(checker.IsClean, "Musivation genre in Musivation/ folder should be clean");
        }

        public static void LibChecker_MotivationFolderWithMotivationGenre_IsClean()
        {
            // Track in Motivation/ with Motivation genre - correct placement
            var tag = new TrackTag("\\Motivation\\Some Artist\\Singles\\Some Artist - Song A.xml",
                "Song A", "Some Artist", "Test Album", "2020", "1", "Motivation",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(checker.IsClean, "Motivation genre in Motivation/ folder should be clean");
        }

        // ---- Filename case-sensitivity (documented footgun) ----

        public static void LibChecker_FilenameArtistCaseMismatch_IsDirty()
        {
            // CheckFilenameForStr uses case-sensitive Contains(): artist tag "Artist Beta" vs
            // filename "artist beta - song a.xml" -> the lowercase filename won't contain "Artist Beta"
            var tag = new TrackTag("\\Artists\\Artist Beta\\Singles\\artist beta - song a.xml",
                "song a", "Artist Beta", "Test Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(!checker.IsClean, "Filename casing mismatch with artist tag should be dirty (case-sensitive check)");
        }

        // ---- Sources: Anime has no album rule ----

        public static void LibChecker_MiscWithTwoArtistSongs_IsClean()
        {
            // 2 songs from same artist in Misc is fine (trio rule fires at 3+)
            var tags = new List<TrackTag>
            {
                new TrackTag("\\Miscellaneous Songs\\Artist Beta - Song A.xml",
                    "Song A", "Artist Beta", "Test Album", "2020", "1", "Hip-Hop",
                    "00:03:00.0000000", "1", "True", "1200", "1200"),
                new TrackTag("\\Miscellaneous Songs\\Artist Beta - Song B.xml",
                    "Song B", "Artist Beta", "Test Album", "2020", "1", "Hip-Hop",
                    "00:03:00.0000000", "1", "True", "1200", "1200"),
            };
            var checker = new LibChecker(tags);
            Assert.True(checker.IsClean, "2 songs from same artist in Misc should be clean (below 3-song threshold)");
        }

        public static void LibChecker_SourcesFolderAnime_NoAlbumRule_IsClean()
        {
            // Anime subfolder has no OST requirement - intentional per CheckSourcesFolder code
            var tag = new TrackTag("\\Sources\\Anime\\Dragon Ball Z\\Artist - Song A.xml",
                "Song A", "Artist", "Dragon Ball Z Filler Arc", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(checker.IsClean, "Anime sources folder with any album name should be clean (no OST rule)");
        }

        // ---- CheckCompilationsFolder ----

        public static void LibChecker_CompilationTrack_NoArtistFolder_IsClean()
        {
            // Track in Compilations/ whose artist has no Artists/ folder - correct routing
            var tag = new TrackTag("\\Compilations\\Barbie The Album\\Nicki Minaj - Barbie World.xml",
                "Barbie World", "Nicki Minaj", "Barbie The Album", "2023", "5", "Pop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(checker.IsClean, "Compilations/ track with no corresponding Artists/ folder should be clean");
        }

        public static void LibChecker_CompilationTrack_ArtistHasFolder_IsDirty()
        {
            // Artist has an Artists/ folder AND a track in Compilations/ - should have gone to Artists/
            // The album has only 1 distinct artist so it is NOT a genuine compilation -> dirty.
            var artistFolderTag = new TrackTag("\\Artists\\Nicki Minaj\\Singles\\Nicki Minaj - Super Bass.xml",
                "Super Bass", "Nicki Minaj", "Pink Friday", "2010", "1", "Rap",
                "00:03:30.0000000", "1", "True", "1200", "1200");
            var compilationTag = new TrackTag("\\Compilations\\Barbie The Album\\Nicki Minaj - Barbie World.xml",
                "Barbie World", "Nicki Minaj", "Barbie The Album", "2023", "5", "Pop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
            var checker = new LibChecker(new List<TrackTag> { artistFolderTag, compilationTag });
            Assert.True(!checker.IsClean, "Artist has Artists/ folder but track is in Compilations/ (single-artist album) -> should be dirty");
        }

        public static void LibChecker_GenuineCompilation_ArtistHasFolder_IsClean()
        {
            // Genuine various-artist compilation (3+ distinct primary artists in the album folder).
            // Artist A has an Artists/ folder but the compilation album is legitimately various-artist.
            // Should NOT be flagged: routing to Compilations/ was correct.
            var artistFolderTag = new TrackTag("\\Artists\\2Pac\\Singles\\2Pac - Me Against The World.xml",
                "Me Against The World", "2Pac", "Me Against The World", "1995", "1", "Rap",
                "00:05:00.0000000", "1", "True", "800", "800");
            var compTag1 = new TrackTag("\\Compilations\\Now Hip Hop Vol1\\2Pac - Hit Em Up.xml",
                "Hit Em Up", "2Pac", "Now Hip Hop Vol1", "2000", "1", "Rap",
                "00:05:00.0000000", "1", "True", "800", "800");
            var compTag2 = new TrackTag("\\Compilations\\Now Hip Hop Vol1\\Eminem - Lose Yourself.xml",
                "Lose Yourself", "Eminem", "Now Hip Hop Vol1", "2000", "2", "Rap",
                "00:05:00.0000000", "1", "True", "800", "800");
            var compTag3 = new TrackTag("\\Compilations\\Now Hip Hop Vol1\\Kanye West - Gold Digger.xml",
                "Gold Digger", "Kanye West", "Now Hip Hop Vol1", "2000", "3", "Rap",
                "00:05:00.0000000", "1", "True", "800", "800");
            var checker = new LibChecker(new List<TrackTag> { artistFolderTag, compTag1, compTag2, compTag3 });
            Assert.True(checker.IsClean, "Genuine compilation (3+ distinct artists) is correctly in Compilations/ even if one artist has Artists/ folder");
        }

        // ---- Malformed RelPath (bounds guard) ----

        public static void LibChecker_MalformedRelPath_DoesNotThrow()
        {
            // "\\file.xml".Split('\\') gives ["", "file.xml"] - only 2 parts.
            // Without the bounds guard, GetRelPathPart(tag, 2) throws IndexOutOfRangeException.
            // With the guard it returns "" and path-based rules silently skip the tag.
            var shortPathTag = new TrackTag("\\file.xml", "Song", "Artist", "Album",
                "2020", "1", "Hip-Hop", "00:03:00.0000000", "1", "True", "1200", "1200");
            new LibChecker(new List<TrackTag> { shortPathTag });
        }

        // ---- CheckAlbumCoverDimensions ----

        public static void LibChecker_LowResCover_IsDirty()
        {
            var tag = new TrackTag("\\Artists\\Known Artist\\Singles\\Known Artist - Song A.xml",
                "Song A", "Known Artist", "Test Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "500", "500");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(!checker.IsClean, "500x500 cover art should be dirty (below 800px standard)");
        }

        public static void LibChecker_ExactThresholdCover_IsClean()
        {
            var tag = new TrackTag("\\Artists\\Known Artist\\Singles\\Known Artist - Song A.xml",
                "Song A", "Known Artist", "Test Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "800", "800");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(checker.IsClean, "800x800 cover art (exactly at threshold) should be clean");
        }

        public static void LibChecker_NonSquareLowRes_IsDirty()
        {
            // min(600, 1200) = 600 < 800 - dirty even if one dimension is large
            var tag = new TrackTag("\\Artists\\Known Artist\\Singles\\Known Artist - Song A.xml",
                "Song A", "Known Artist", "Test Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "600", "1200");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(!checker.IsClean, "600x1200 cover art should be dirty (min dimension 600 is below 800)");
        }

        public static void LibChecker_UnknownFormatCover_IsClean()
        {
            // "Unknown" dimensions (unrecognised image format) must not be flagged
            var tag = new TrackTag("\\Artists\\Known Artist\\Singles\\Known Artist - Song A.xml",
                "Song A", "Known Artist", "Test Album", "2020", "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "Unknown", "Unknown");
            var checker = new LibChecker(new List<TrackTag> { tag });
            Assert.True(checker.IsClean, "Cover with Unknown dimensions should be clean (unrecognised format, skip)");
        }
    }
}
