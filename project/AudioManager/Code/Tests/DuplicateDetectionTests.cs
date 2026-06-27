using System;
using System.Collections.Generic;
using System.IO;

namespace AudioManager
{
    internal static class DuplicateDetectionTests
    {
        // ---- NormaliseTitleForDedup ----

        public static void NormaliseTitle_NoFeat_Unchanged()
        {
            Assert.Equal("God's Plan", MusicIntegrator.NormaliseTitleForDedup("God's Plan"), "title without feat should be unchanged");
        }

        public static void NormaliseTitle_FeatDot_Stripped()
        {
            Assert.Equal("God's Plan", MusicIntegrator.NormaliseTitleForDedup("God's Plan (feat. Drake)"), "feat. parenthetical should be stripped");
        }

        public static void NormaliseTitle_FtDot_Stripped()
        {
            Assert.Equal("God's Plan", MusicIntegrator.NormaliseTitleForDedup("God's Plan (ft. Drake)"), "ft. parenthetical should be stripped");
        }

        public static void NormaliseTitle_Featuring_Stripped()
        {
            Assert.Equal("God's Plan", MusicIntegrator.NormaliseTitleForDedup("God's Plan (featuring Drake)"), "featuring parenthetical should be stripped");
        }

        public static void NormaliseTitle_Remix_Preserved()
        {
            Assert.Equal("God's Plan (Remix)", MusicIntegrator.NormaliseTitleForDedup("God's Plan (Remix)"), "(Remix) should NOT be stripped - different track");
        }

        public static void NormaliseTitle_CaseInsensitive()
        {
            Assert.Equal("SONG", MusicIntegrator.NormaliseTitleForDedup("SONG (FEAT. ARTIST)"), "feat stripping should be case-insensitive");
        }

        public static void NormaliseTitle_Empty_ReturnsEmpty()
        {
            Assert.Equal("", MusicIntegrator.NormaliseTitleForDedup(""), "empty title should return empty");
        }

        // ---- LoadArtistAliases ----

        public static void LoadArtistAliases_MissingFile_ReturnsEmpty()
        {
            var result = MusicIntegrator.LoadArtistAliases("/nonexistent/path/aliases.xml");
            Assert.Equal("0", result.Count.ToString(), "missing file should return empty dict");
        }

        public static void LoadArtistAliases_ValidFile_ParsesOldToCanonical()
        {
            string tmp = Path.GetTempFileName() + ".xml";
            try
            {
                File.WriteAllText(tmp,
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<ArtistAliases>" +
                    "<Alias from=\"Kanye West\" to=\"Ye\" />" +
                    "<Alias from=\"Jay Z\" to=\"Jay-Z\" />" +
                    "</ArtistAliases>");
                var result = MusicIntegrator.LoadArtistAliases(tmp);
                Assert.Equal("2", result.Count.ToString(), "should parse 2 aliases");
                Assert.Equal("Ye", result["Kanye West"], "Kanye West -> Ye");
                Assert.Equal("Jay-Z", result["Jay Z"], "Jay Z -> Jay-Z");
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }

        public static void LoadArtistAliases_CaseInsensitiveKeys()
        {
            string tmp = Path.GetTempFileName() + ".xml";
            try
            {
                File.WriteAllText(tmp,
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<ArtistAliases><Alias from=\"kanye west\" to=\"Ye\" /></ArtistAliases>");
                var result = MusicIntegrator.LoadArtistAliases(tmp);
                // Lookup by different casing should work
                Assert.True(result.ContainsKey("Kanye West"), "lookup should be case-insensitive");
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }

        // ---- GetAliasExpandedKeys ----

        private static Dictionary<string, string> MakeAliases()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Kanye West", "Ye" }
            };
        }

        private static Dictionary<string, List<string>> MakeReverseAliases()
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Ye", new List<string> { "Kanye West" } }
            };
        }

        public static void AliasExpand_NoAlias_YieldsOnlyOriginalKey()
        {
            var keys = new List<string>(MusicIntegrator.GetAliasExpandedKeys(
                "Drake", "God's Plan", MakeAliases(), MakeReverseAliases()));
            Assert.Equal("1", keys.Count.ToString(), "artist with no alias should yield exactly 1 key");
            Assert.Equal("drake\0god's plan", keys[0], "key should be artist NUL title lowercased");
        }

        public static void AliasExpand_OldName_AddsCanonicalKey()
        {
            // Library has "Kanye West" -> index built under "kanye west\0dam"
            // FindDuplicateInMirror will also look up "ye\0dam" -> should find it
            var keys = new List<string>(MusicIntegrator.GetAliasExpandedKeys(
                "Kanye West", "DAM", MakeAliases(), MakeReverseAliases()));
            Assert.Equal("2", keys.Count.ToString(), "old name should yield 2 keys (original + canonical)");
            Assert.True(keys.Contains("kanye west\0dam"), "should contain original key");
            Assert.True(keys.Contains("ye\0dam"), "should contain canonical key");
        }

        public static void AliasExpand_CanonicalName_AddsOldNameKey()
        {
            // Library has "Ye" -> index built under "ye\0dam"
            // If new batch has "Kanye West" -> finds "kanye west\0dam" -> should also hit
            var keys = new List<string>(MusicIntegrator.GetAliasExpandedKeys(
                "Ye", "DAM", MakeAliases(), MakeReverseAliases()));
            Assert.Equal("2", keys.Count.ToString(), "canonical name should yield 2 keys (original + old name)");
            Assert.True(keys.Contains("ye\0dam"), "should contain canonical key");
            Assert.True(keys.Contains("kanye west\0dam"), "should contain old-name key");
        }

        // ---- IsDeluxeVersionOf ----

        public static void IsDeluxe_DeluxeEdition_IsDeluxeOfBase()
        {
            Assert.True(MusicIntegrator.IsDeluxeVersionOf("BULLY - DELUXE", "BULLY"), "BULLY - DELUXE is deluxe of BULLY");
        }

        public static void IsDeluxe_ExpandedEdition_IsDeluxeOfBase()
        {
            Assert.True(MusicIntegrator.IsDeluxeVersionOf("Some Album (Expanded Edition)", "Some Album"), "expanded edition is deluxe of base");
        }

        public static void IsDeluxe_Remastered_IsDeluxeOfBase()
        {
            Assert.True(MusicIntegrator.IsDeluxeVersionOf("The Blueprint (Remastered)", "The Blueprint"), "remastered is deluxe of base");
        }

        public static void IsDeluxe_BaseNotDeluxeOfDeluxe()
        {
            Assert.True(!MusicIntegrator.IsDeluxeVersionOf("BULLY", "BULLY - DELUXE"), "base is NOT deluxe of deluxe edition");
        }

        public static void IsDeluxe_TwoBothDeluxe_ReturnsFalse()
        {
            Assert.True(!MusicIntegrator.IsDeluxeVersionOf("BULLY - DELUXE", "BULLY - SUPER DELUXE"), "two deluxe editions - no clear hierarchy");
        }

        public static void IsDeluxe_UnrelatedAlbums_ReturnsFalse()
        {
            Assert.True(!MusicIntegrator.IsDeluxeVersionOf("Some Album DELUXE", "Other Album"), "unrelated albums are not deluxe versions of each other");
        }

        public static void IsDeluxe_CaseInsensitive()
        {
            Assert.True(MusicIntegrator.IsDeluxeVersionOf("bully - deluxe", "BULLY"), "deluxe detection is case-insensitive");
        }

        public static void IsDeluxe_NullOrEmpty_ReturnsFalse()
        {
            Assert.True(!MusicIntegrator.IsDeluxeVersionOf(null, "BULLY"), "null candidate returns false");
            Assert.True(!MusicIntegrator.IsDeluxeVersionOf("BULLY - DELUXE", null), "null base returns false");
            Assert.True(!MusicIntegrator.IsDeluxeVersionOf("", ""), "empty strings return false");
        }
    }
}
