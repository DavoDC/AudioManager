using System;
using System.Collections.Generic;
using System.Linq;

namespace AudioManager
{
    internal static class TagFixerTests
    {
        // ---- RemoveParentheticals ----

        public static void RemoveParentheticals_StripsFeatArtist()
        {
            string result = TagFixer.RemoveParentheticals("Song Title (feat. Some Artist)");
            Assert.Equal("Song Title", result, "feat. parenthetical should be stripped");
        }

        public static void RemoveParentheticals_StripsFtArtist()
        {
            string result = TagFixer.RemoveParentheticals("Song Title (ft. Some Artist)");
            Assert.Equal("Song Title", result, "ft. parenthetical should be stripped");
        }

        public static void RemoveParentheticals_StripsExplicit()
        {
            string result = TagFixer.RemoveParentheticals("Song Title (Explicit)");
            Assert.Equal("Song Title", result, "(Explicit) should be stripped");
        }

        public static void RemoveParentheticals_StripsAlbumVersion()
        {
            string result = TagFixer.RemoveParentheticals("Song Title (Album Version)");
            Assert.Equal("Song Title", result, "(Album Version) should be stripped");
        }

        public static void RemoveParentheticals_LeavesPlainTitle()
        {
            string result = TagFixer.RemoveParentheticals("Plain Song Title");
            Assert.Equal("Plain Song Title", result, "Plain title should be unchanged");
        }

        // ---- StripAlbumSuffixes ----

        public static void StripAlbumSuffixes_StripsDeluxeEdition()
        {
            string result = TagFixer.StripAlbumSuffixes("Album Name (Deluxe Edition)");
            Assert.Equal("Album Name", result, "(Deluxe Edition) should be stripped");
        }

        public static void StripAlbumSuffixes_StripsRemastered()
        {
            string result = TagFixer.StripAlbumSuffixes("Album Name (Remastered)");
            Assert.Equal("Album Name", result, "(Remastered) should be stripped");
        }

        public static void StripAlbumSuffixes_StripsYearSuffix()
        {
            string result = TagFixer.StripAlbumSuffixes("Album Name (2019)");
            Assert.Equal("Album Name", result, "Year suffix should be stripped");
        }

        public static void StripAlbumSuffixes_LeavesPlainAlbum()
        {
            string result = TagFixer.StripAlbumSuffixes("Plain Album Name");
            Assert.Equal("Plain Album Name", result, "Plain album name should be unchanged");
        }

        // ---- ExtractAndFixArtists ----

        public static void ExtractAndFixArtists_ExtractsFeaturedArtist()
        {
            List<string> result = TagFixer.ExtractAndFixArtists("Song (feat. Featured Artist)", "Primary Artist");
            Assert.True(
                result.Any(a => a.Equals("Featured Artist", StringComparison.OrdinalIgnoreCase)),
                "Featured artist should be extracted from (feat. X) parenthetical");
        }

        public static void ExtractAndFixArtists_SplitsAmpersandInFeat()
        {
            // Perry Como class of bug: "(feat. Alpha & Beta)" must produce Alpha and Beta separately
            List<string> result = TagFixer.ExtractAndFixArtists("Song (feat. Artist Alpha & Artist Beta)", "Primary Artist");
            Assert.True(
                result.Any(a => a.Equals("Artist Alpha", StringComparison.OrdinalIgnoreCase)),
                "First artist in '& ' split should be in list");
            Assert.True(
                result.Any(a => a.Equals("Artist Beta", StringComparison.OrdinalIgnoreCase)),
                "Second artist in '& ' split should be in list");
            Assert.True(
                !result.Any(a => a.Equals("Artist Alpha & Artist Beta", StringComparison.OrdinalIgnoreCase)),
                "Compound '& ' form must NOT appear as a single artist entry");
        }

        public static void ExtractAndFixArtists_NoDuplicates()
        {
            // Artist already in the field should not be added again via feat. extraction
            List<string> result = TagFixer.ExtractAndFixArtists("Song (feat. Existing Artist)", "Primary Artist;Existing Artist");
            int count = result.Count(a => a.Equals("Existing Artist", StringComparison.OrdinalIgnoreCase));
            Assert.True(count == 1, $"Existing artist should appear exactly once, appeared {count} times");
        }

        public static void ExtractAndFixArtists_SkipsOfBandNameClarification()
        {
            // "Patrick Stump of Fall Out Boy" when Fall Out Boy already present - "of BandName" is a descriptor
            List<string> result = TagFixer.ExtractAndFixArtists(
                "Song (feat. Macklemore & Patrick Stump of Fall Out Boy)",
                "Primary Artist;Fall Out Boy");
            Assert.True(
                !result.Any(a => a.IndexOf("of Fall Out Boy", StringComparison.OrdinalIgnoreCase) >= 0),
                "'X of BandName' should be skipped when BandName is already in artist list");
        }

        // ---- ShouldFixGenre ----

        public static void ShouldFixGenre_MusivationArtistMissingGenre()
        {
            bool result = TagFixer.ShouldFixGenre("Akira The Don", "Hip-Hop");
            Assert.True(result, "Akira The Don without Musivation genre should need genre fix");
        }

        public static void ShouldFixGenre_MusivationArtistAlreadyHasGenre()
        {
            bool result = TagFixer.ShouldFixGenre("Akira The Don", "Musivation");
            Assert.True(!result, "Akira The Don already having Musivation genre should not need fix");
        }

        public static void ShouldFixGenre_NormalArtistNoChange()
        {
            bool result = TagFixer.ShouldFixGenre("Some Artist", "Hip-Hop");
            Assert.True(!result, "Normal artist with non-Motivation genre should not need fix");
        }

        // ---- DetermineGenre ----

        public static void DetermineGenre_MusivationForAkiraTheDon()
        {
            string result = TagFixer.DetermineGenre("Akira The Don");
            Assert.Equal("Musivation", result, "Akira The Don should get Musivation genre");
        }

        public static void DetermineGenre_MusivationForLootBryonSmith()
        {
            string result = TagFixer.DetermineGenre("Loot Bryon Smith");
            Assert.Equal("Musivation", result, "Loot Bryon Smith should get Musivation genre");
        }

        public static void DetermineGenre_MotivationForOtherArtist()
        {
            string result = TagFixer.DetermineGenre("Generic Motivation Speaker");
            Assert.Equal("Motivation", result, "Non-Musivation artist should get Motivation genre");
        }
    }
}
