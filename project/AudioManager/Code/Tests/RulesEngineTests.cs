namespace AudioManager
{
    /// <summary>Direct tests of the shared RulesEngine (INT-3): boundaries and the single normalizer.</summary>
    internal static class RulesEngineTests
    {
        public static void RulesEngine_NormalizeAlbum_MatchesTagFixerComposition()
        {
            string[] inputs = { "The Blueprint (Explicit Version)", "Life After Death (2014 Remastered Edition)",
                "Album (Deluxe Edition) (2019)", "Plain Album", "Greatest Hits (Volume 2)", "", null };
            foreach (var input in inputs)
            {
                string expected = TagFixer.StripAlbumSuffixes(TagFixer.RemoveParentheticals(input));
                Assert.True(RulesEngine.NormalizeAlbum(input) == expected, "NormalizeAlbum equals the TagFixer composition for '" + input + "'");
            }
        }

        public static void RulesEngine_Thresholds_AreTheDocumentedBoundaries()
        {
            Assert.True(!RulesEngine.UsesAlbumSubfolder(1) && RulesEngine.UsesAlbumSubfolder(2), "album subfolder starts at 2");
            Assert.True(!RulesEngine.EarnsArtistFolder(2) && RulesEngine.EarnsArtistFolder(3), "artist folder starts at 3");
            Assert.True(!RulesEngine.IsCompilation(2) && RulesEngine.IsCompilation(3), "compilation starts at 3 artists");
            Assert.True(!RulesEngine.EarnsPeopleFolder(2) && RulesEngine.EarnsPeopleFolder(3), "People folder starts at 3");
        }

        public static void RulesEngine_IsDeluxeVersionOf_KeepsCurrentSemantics()
        {
            Assert.True(RulesEngine.IsDeluxeVersionOf("BULLY - DELUXE", "BULLY"), "deluxe of base");
            Assert.True(!RulesEngine.IsDeluxeVersionOf("BULLY", "BULLY - DELUXE"), "base is not deluxe of deluxe");
            Assert.True(!RulesEngine.IsDeluxeVersionOf("A (Deluxe)", "A (Remastered)"), "two deluxe editions: no hierarchy");
            Assert.True(!RulesEngine.IsDeluxeVersionOf("Other (Deluxe)", "BULLY"), "different prefix");
            Assert.True(!RulesEngine.IsDeluxeVersionOf(null, "BULLY") && !RulesEngine.IsDeluxeVersionOf("BULLY (Deluxe)", ""), "null or empty");
        }
    }
}
