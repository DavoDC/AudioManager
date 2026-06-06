using AudioManager.Code.Modules;

namespace AudioManager
{
    /// <summary>
    /// Tests for ManifestEntry.PrimaryArtist behavior.
    /// </summary>
    internal static class ManifestRunnerTests
    {
        // ---- ManifestEntry.PrimaryArtist ----
        // Different from Track.ProcessProperty: returns only the first artist, not an array.
        // Separator priority: semicolon first, then comma.

        public static void ManifestEntry_PrimaryArtist_SemicolonSeparated_ReturnFirst()
        {
            var entry = new ManifestEntry { Artist = "Artist A;Artist B" };
            Assert.Equal("Artist A", entry.PrimaryArtist, "semicolon-separated: returns first artist");
        }

        public static void ManifestEntry_PrimaryArtist_CommaSeparated_ReturnFirst()
        {
            var entry = new ManifestEntry { Artist = "Artist A, Artist B" };
            Assert.Equal("Artist A", entry.PrimaryArtist, "comma-separated: returns first artist (trimmed)");
        }

        public static void ManifestEntry_PrimaryArtist_SingleArtist_ReturnSelf()
        {
            var entry = new ManifestEntry { Artist = "Single Artist" };
            Assert.Equal("Single Artist", entry.PrimaryArtist, "no separator: returns full string");
        }

        public static void ManifestEntry_PrimaryArtist_SemicolonTakesPriorityOverComma()
        {
            // Semicolon is checked first; comma within the second field is not used as separator
            var entry = new ManifestEntry { Artist = "Artist A;Artist B,Artist C" };
            Assert.Equal("Artist A", entry.PrimaryArtist, "semicolon takes priority: returns first of semicolon split");
        }

    }
}
