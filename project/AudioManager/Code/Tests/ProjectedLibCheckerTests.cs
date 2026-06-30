using AudioManager.Code.Modules;
using System.Collections.Generic;

namespace AudioManager
{
    /// <summary>
    /// Tests for MusicIntegrator.BuildProjection - the pure-function core of dry-run projected LibChecker.
    /// Validates that the projected tag list correctly models: added files, removed library copies,
    /// and Misc migration moves - all without touching the filesystem.
    /// </summary>
    internal static class ProjectedLibCheckerTests
    {
        private static TrackTag ArtistTag(string artist = "Jay-Z", string title = "Song",
            string album = "The Blueprint", string relPath = null)
        {
            string path = relPath ?? $"\\Artists\\{artist}\\{album}\\{artist} - {title}.xml";
            return new TrackTag(path, title, artist, album, "2001", "1", "Hip-Hop",
                "00:03:30.0000000", "1", "True", "1200", "1200");
        }

        private static TrackTag MiscTag(string artist = "New Artist", string title = "Song")
        {
            string path = $"\\Miscellaneous Songs\\{artist} - {title}.xml";
            return new TrackTag(path, title, artist, "Missing", "2020", "0", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "1200", "1200");
        }

        // ---- Add-only scenarios ----

        public static void Projection_EmptyLibrary_AddOneFile_HasOneFile()
        {
            var additions = new List<TrackTag> { ArtistTag() };
            var result = MusicIntegrator.BuildProjection(
                new List<TrackTag>(), additions, new HashSet<string>());
            Assert.Equal("1", result.Count.ToString(), "one addition to empty library -> one file in projection");
        }

        public static void Projection_ExistingFile_NoChanges_SameFilePresent()
        {
            var current = new List<TrackTag> { ArtistTag() };
            var result = MusicIntegrator.BuildProjection(current, new List<TrackTag>(), new HashSet<string>());
            Assert.Equal("1", result.Count.ToString(), "no changes -> same count in projection");
            Assert.Equal("Jay-Z", result[0].Artists, "unchanged tag should survive projection");
        }

        // ---- Removal scenarios ----

        public static void Projection_RemoveExistingFile_NotInResult()
        {
            var tag = ArtistTag();
            var current = new List<TrackTag> { tag };
            var removals = new HashSet<string> { tag.RelPath };
            var result = MusicIntegrator.BuildProjection(current, new List<TrackTag>(), removals);
            Assert.Equal("0", result.Count.ToString(), "removed tag should not appear in projection");
        }

        public static void Projection_RemoveNonExistentRelPath_NoError()
        {
            var current = new List<TrackTag> { ArtistTag() };
            var removals = new HashSet<string> { "\\Artists\\Ghost\\Singles\\Ghost - Missing.xml" };
            var result = MusicIntegrator.BuildProjection(current, new List<TrackTag>(), removals);
            Assert.Equal("1", result.Count.ToString(), "removing unknown RelPath is a no-op");
        }

        // ---- L-decision modelling ----

        public static void Projection_LDecision_RemovesLibraryCopy_AddsNewAtDest()
        {
            // Library has single version; new file is album version (L decision: replace library copy)
            var libraryTag = ArtistTag(relPath: "\\Artists\\Jay-Z\\Singles\\Jay-Z - Song.xml");
            var newTag = ArtistTag(relPath: "\\Artists\\Jay-Z\\The Blueprint\\Jay-Z - Song.xml");
            var current = new List<TrackTag> { libraryTag };
            var removals = new HashSet<string> { libraryTag.RelPath };
            var additions = new List<TrackTag> { newTag };

            var result = MusicIntegrator.BuildProjection(current, additions, removals);

            Assert.Equal("1", result.Count.ToString(), "L decision: library copy removed, new added -> still 1 in projection");
            Assert.Equal("\\Artists\\Jay-Z\\The Blueprint\\Jay-Z - Song.xml", result[0].RelPath,
                "result should have new destination path, not old Singles path");
        }

        // ---- Misc migration modelling ----

        public static void Projection_MiscMigration_RemovesFromMisc_AddsToArtists()
        {
            // Artist promoted: existing Misc tag removed, new Artists/Singles/ tag added
            var miscTag = MiscTag();
            var artistsTag = ArtistTag("New Artist", "Song",
                relPath: "\\Artists\\New Artist\\Singles\\New Artist - Song.xml");
            var current = new List<TrackTag> { miscTag };
            var removals = new HashSet<string> { miscTag.RelPath };
            var additions = new List<TrackTag> { artistsTag };

            var result = MusicIntegrator.BuildProjection(current, additions, removals);

            Assert.Equal("1", result.Count.ToString(), "Misc migration: Misc removed, Artists/Singles/ added -> 1 in projection");
            Assert.True(result[0].RelPath.StartsWith("\\Artists\\"), "migrated file should be in Artists/ in projection");
        }

        // ---- LibChecker on projection ----

        public static void Projection_CleanSingleAddition_PassesLibChecker()
        {
            // A clean new track added to empty library should produce a clean LibChecker result
            var newTag = ArtistTag();
            var projected = MusicIntegrator.BuildProjection(
                new List<TrackTag>(), new List<TrackTag> { newTag }, new HashSet<string>());
            var checker = new LibChecker(projected);
            Assert.True(checker.IsClean, "a clean projected tag should yield IsClean=true from LibChecker");
        }

        public static void Projection_DirtyAddition_FailsLibChecker()
        {
            // A file with 'feat.' remaining in title should make projected LibChecker dirty
            var dirtyTag = ArtistTag(title: "Song (feat. Someone)");
            var projected = MusicIntegrator.BuildProjection(
                new List<TrackTag>(), new List<TrackTag> { dirtyTag }, new HashSet<string>());
            var checker = new LibChecker(projected);
            Assert.True(!checker.IsClean, "tag with feat. in title should make projected LibChecker dirty");
        }

        // ---- Multiple files ----

        public static void Projection_MultipleFilesAdded_AllInResult()
        {
            var tagA = ArtistTag("Jay-Z", "Song A", relPath: "\\Artists\\Jay-Z\\Singles\\Jay-Z - Song A.xml");
            var tagB = ArtistTag("Jay-Z", "Song B", relPath: "\\Artists\\Jay-Z\\Singles\\Jay-Z - Song B.xml");
            var result = MusicIntegrator.BuildProjection(
                new List<TrackTag>(), new List<TrackTag> { tagA, tagB }, new HashSet<string>());
            Assert.Equal("2", result.Count.ToString(), "two additions should both appear in projection");
        }

        public static void Projection_RemoveOneOfTwo_CorrectOneRemains()
        {
            var tagA = ArtistTag("Jay-Z", "Song A", relPath: "\\Artists\\Jay-Z\\Singles\\Jay-Z - Song A.xml");
            var tagB = ArtistTag("Jay-Z", "Song B", relPath: "\\Artists\\Jay-Z\\Singles\\Jay-Z - Song B.xml");
            var current = new List<TrackTag> { tagA, tagB };
            var removals = new HashSet<string> { tagA.RelPath };
            var result = MusicIntegrator.BuildProjection(current, new List<TrackTag>(), removals);
            Assert.Equal("1", result.Count.ToString(), "removing one of two should leave one");
            Assert.Equal("Song B", result[0].Title, "correct file should remain");
        }
    }
}
