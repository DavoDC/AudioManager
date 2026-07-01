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

        // ---- RemoveParentheticals - additional patterns ----

        public static void RemoveParentheticals_StripsWithArtist()
        {
            string result = TagFixer.RemoveParentheticals("Song Title (with Guest Artist)");
            Assert.Equal("Song Title", result, "(with X) should be stripped");
        }

        public static void RemoveParentheticals_StripsSquareBracketFeat()
        {
            string result = TagFixer.RemoveParentheticals("Song Title [feat. Featured Artist]");
            Assert.Equal("Song Title", result, "[feat. X] square bracket form should be stripped");
        }

        public static void RemoveParentheticals_StripsEdit()
        {
            string result = TagFixer.RemoveParentheticals("Song Title (Edit)");
            Assert.Equal("Song Title", result, "(Edit) should be stripped");
        }

        public static void RemoveParentheticals_StripsRadioEdit()
        {
            string result = TagFixer.RemoveParentheticals("Song Title (Radio Edit)");
            Assert.Equal("Song Title", result, "(Radio Edit) should be stripped");
        }

        public static void RemoveParentheticals_StripsSingleVersion()
        {
            string result = TagFixer.RemoveParentheticals("Song Title (Single Version)");
            Assert.Equal("Song Title", result, "(Single Version) should be stripped");
        }

        public static void RemoveParentheticals_MultipleParentheticals_BothStripped()
        {
            string result = TagFixer.RemoveParentheticals("Song Title (feat. Artist) (Explicit)");
            Assert.Equal("Song Title", result, "both parentheticals should be stripped");
        }

        // ---- StripAlbumSuffixes - additional patterns ----

        public static void StripAlbumSuffixes_Strips2011Remaster()
        {
            string result = TagFixer.StripAlbumSuffixes("Album Name (2011 Remaster)");
            Assert.Equal("Album Name", result, "(2011 Remaster) should be stripped");
        }

        public static void StripAlbumSuffixes_StripsInternationalVersion()
        {
            string result = TagFixer.StripAlbumSuffixes("Album Name (International Version)");
            Assert.Equal("Album Name", result, "(International Version) should be stripped");
        }

        public static void StripAlbumSuffixes_StripsDeluxeWithoutEdition()
        {
            string result = TagFixer.StripAlbumSuffixes("Album Name (Deluxe)");
            Assert.Equal("Album Name", result, "(Deluxe) without Edition suffix should be stripped");
        }

        public static void StripAlbumSuffixes_StripsBonusTrack()
        {
            string result = TagFixer.StripAlbumSuffixes("Album Name (Bonus Track)");
            Assert.Equal("Album Name", result, "(Bonus Track) should be stripped");
        }

        // ---- ShouldFixGenre - additional cases ----

        public static void ShouldFixGenre_LootBryonSmith_MissingMusivation_NeedsFix()
        {
            bool result = TagFixer.ShouldFixGenre("Loot Bryon Smith", "Hip-Hop");
            Assert.True(result, "Loot Bryon Smith without Musivation genre should need fix");
        }

        public static void ShouldFixGenre_MotivationalGenre_NeedsNormalization()
        {
            // "Motivational" contains "Motivation" but is not exactly "Motivation"
            bool result = TagFixer.ShouldFixGenre("Some Speaker", "Motivational");
            Assert.True(result, "Non-exact Motivation variant (Motivational) should need normalization");
        }

        // ---- ExtractAndFixArtists - additional patterns ----

        public static void ExtractAndFixArtists_WithPattern_ExtractsFeaturedArtist()
        {
            var result = TagFixer.ExtractAndFixArtists("Song (with Guest Artist)", "Primary Artist");
            Assert.True(
                result.Contains("Guest Artist") ||
                result.Exists(a => a.Equals("Guest Artist", System.StringComparison.OrdinalIgnoreCase)),
                "(with X) pattern should extract the featured artist");
        }

        public static void ExtractAndFixArtists_SquareBracketFeat_ExtractsFeaturedArtist()
        {
            var result = TagFixer.ExtractAndFixArtists("Song [feat. Featured Artist]", "Primary Artist");
            Assert.True(
                result.Exists(a => a.Equals("Featured Artist", System.StringComparison.OrdinalIgnoreCase)),
                "[feat. X] square bracket form should extract the featured artist");
        }

        public static void ExtractAndFixArtists_NormalizesLowercaseArtistToTitleCase()
        {
            // Artists not in the overrides config get ToTitleCase applied
            List<string> result = TagFixer.ExtractAndFixArtists("Song Title", "lowercase artist name");
            Assert.True(
                result.Exists(a => a.Equals("Lowercase Artist Name", System.StringComparison.Ordinal)),
                "Lowercase artist should be normalized to title case");
        }

        // ---- Artist name variant override (Unicode diacritics -> canonical) ----

        public static void ExtractAndFixArtists_VariantArtistName_NormalizesToCanonical()
        {
            // JAŸ-Z (Ÿ = U+0178) is a Unicode-stylized variant of Jay-Z used by some providers.
            // The overrides config maps it to canonical "Jay-Z" via the variant attribute.
            List<string> result = TagFixer.ExtractAndFixArtists("Song Title", "JAŸ-Z");
            Assert.True(
                result.Any(a => a.Equals("Jay-Z", StringComparison.OrdinalIgnoreCase)),
                "Variant artist name JAŸ-Z should normalize to canonical Jay-Z via override");
            Assert.True(
                !result.Any(a => a.IndexOf("Ÿ", StringComparison.Ordinal) >= 0 || a.IndexOf("ÿ", StringComparison.Ordinal) >= 0),
                "No diaeresis form (Ÿ or ÿ) should appear in output - variant must resolve to canonical");
        }

        public static void ExtractAndFixArtists_VariantArtistAndFeatCanonical_NoDuplicate()
        {
            // Real-world case: "The Notorious B.I.G.; Angela Winbush; JAŸ-Z" tag
            // + title "(feat. Jay-Z & Angela Winbush)" -> must NOT produce Jay-Z;Jay-Z
            // (JAŸ-Z in tag normalizes to Jay-Z via variant override, dedup removes the feat. Jay-Z copy)
            List<string> result = TagFixer.ExtractAndFixArtists(
                "I Love The Dough (feat. Jay-Z & Angela Winbush)",
                "The Notorious B.I.G.;Angela Winbush;JAŸ-Z");
            int jayzCount = result.Count(a => a.Equals("Jay-Z", StringComparison.OrdinalIgnoreCase));
            Assert.True(jayzCount == 1,
                $"Jay-Z should appear exactly once (got {jayzCount}): variant JAŸ-Z in tag + Jay-Z in feat must deduplicate");
        }

        // ---- Null/empty input guards ----

        public static void RemoveParentheticals_EmptyString_ReturnsEmpty()
        {
            string result = TagFixer.RemoveParentheticals("");
            Assert.Equal("", result, "empty input should return empty (null guard)");
        }

        public static void StripAlbumSuffixes_EmptyString_ReturnsEmpty()
        {
            string result = TagFixer.StripAlbumSuffixes("");
            Assert.Equal("", result, "empty input should return empty (null guard)");
        }

        public static void ShouldFixGenre_EmptyArtists_ReturnsFalse()
        {
            bool result = TagFixer.ShouldFixGenre("", "Hip-Hop");
            Assert.True(!result, "empty artists string should not need genre fix");
        }

        public static void DetermineGenre_EmptyArtists_ReturnsEmptyString()
        {
            string result = TagFixer.DetermineGenre("");
            Assert.Equal("", result, "empty artists string should return empty genre");
        }

        // ---- StripAlbumSuffixes - version-suffix normalization (B.I.G. bug 2026-06-28) ----

        public static void StripAlbumSuffixes_StripsYearRemasteredEdition()
        {
            // Root case: "Life After Death (2014 Remastered Edition)" was not stripped before this fix.
            // The remaster pattern stopped at Remaster(ed) without the trailing Edition word.
            string result = TagFixer.StripAlbumSuffixes("Life After Death (2014 Remastered Edition)");
            Assert.Equal("Life After Death", result, "(YYYY Remastered Edition) should be stripped");
        }

        public static void StripAlbumSuffixes_StripsRemasteredEdition()
        {
            string result = TagFixer.StripAlbumSuffixes("Album Name (Remastered Edition)");
            Assert.Equal("Album Name", result, "(Remastered Edition) without year should be stripped");
        }

        public static void StripAlbumSuffixes_StripsYearRemasterEdition()
        {
            string result = TagFixer.StripAlbumSuffixes("Album Name (2011 Remaster Edition)");
            Assert.Equal("Album Name", result, "(YYYY Remaster Edition) should be stripped");
        }

        public static void StripAlbumSuffixes_StripsMono()
        {
            string result = TagFixer.StripAlbumSuffixes("Album Name (Mono)");
            Assert.Equal("Album Name", result, "(Mono) should be stripped");
        }

        public static void StripAlbumSuffixes_StripsStereo()
        {
            string result = TagFixer.StripAlbumSuffixes("Album Name (Stereo)");
            Assert.Equal("Album Name", result, "(Stereo) should be stripped");
        }

        // ---- Intentionally NOT stripped (documents design boundaries) ----

        public static void RemoveParentheticals_LivePattern_NotStripped()
        {
            // (Live) is not in the strip patterns - live performances keep their label
            string result = TagFixer.RemoveParentheticals("Song Title (Live)");
            Assert.Equal("Song Title (Live)", result, "(Live) should NOT be stripped - not in strip patterns");
        }

        public static void RemoveParentheticals_AcousticPattern_NotStripped()
        {
            // (Acoustic) is also not in the strip patterns
            string result = TagFixer.RemoveParentheticals("Song Title (Acoustic)");
            Assert.Equal("Song Title (Acoustic)", result, "(Acoustic) should NOT be stripped - not in strip patterns");
        }

        public static void StripAlbumSuffixes_LivePattern_NotStripped()
        {
            // Album "(Live at Wembley)" should not be stripped - only specific edition/remaster patterns are removed
            string result = TagFixer.StripAlbumSuffixes("Album Name (Live at Wembley)");
            Assert.Equal("Album Name (Live at Wembley)", result, "(Live at ...) should NOT be stripped from album names");
        }

        // ---- ExtractAndFixArtists - casing bugs ----

        public static void ExtractAndFixArtists_MixedCaseAbbreviation_Preserved()
        {
            // Bug fix: "PJ Simas".ToLower() = "pj simas" -> ToTitleCase = "Pj Simas" (wrong).
            // Mixed-case names (upper + lower) must be preserved without ToLower() normalization.
            List<string> result = TagFixer.ExtractAndFixArtists("Ocean Drop", "PJ Simas");
            Assert.True(
                result.Any(a => a.Equals("PJ Simas", StringComparison.Ordinal)),
                "Mixed-case abbreviation 'PJ Simas' must be preserved as-is (not lowercased to 'Pj Simas')");
        }

        public static void ExtractAndFixArtists_AllCapsAbbreviation_InOverrides_Preserved()
        {
            // "XV" is all-caps and in the artist-name-overrides.xml config.
            // The override lookup must fire before the ToLower+ToTitleCase normalization destroys it.
            // Note: this test only passes if the overrides config file exists and contains "XV".
            List<string> result = TagFixer.ExtractAndFixArtists("Mirror's Edge", "XV");
            Assert.True(
                result.Any(a => a.Equals("XV", StringComparison.Ordinal)),
                "All-caps override 'XV' must be preserved via artist-name-overrides.xml (not normalized to 'Xv')");
        }

        public static void ExtractAndFixArtists_AllCapsFullName_NormalizedToTitleCase()
        {
            // All-caps non-override names should still be title-cased (normal normalization behavior).
            // "SCOTT ADAMS" should become "Scott Adams", not stay all-caps.
            List<string> result = TagFixer.ExtractAndFixArtists("Some Track", "SCOTT ADAMS");
            Assert.True(
                result.Any(a => a.Equals("Scott Adams", StringComparison.Ordinal)),
                "All-caps full name without override must be title-cased: 'SCOTT ADAMS' -> 'Scott Adams'");
        }

        // ---- ProcessFile (full pipeline: real MP3 file I/O, not pure functions) ----

        public static void ProcessFile_MessyTags_WritesCleanedId3TagsToDisk()
        {
        }

        public static void ProcessFile_MessyTags_RenamesFileToArtistDashTitle()
        {
        }

        public static void ProcessFile_CleanTags_SkipsWithNoChanges()
        {
        }

        public static void ProcessFile_SetsCompilationFlagOnSave()
        {
        }
    }
}
