using AudioManager.Code.Modules;

namespace AudioManager
{
    internal static class TrackTests
    {
        // ---- Track.ProcessProperty ----

        public static void ProcessProperty_NoSeparator_ReturnsSingleElement()
        {
            string[] result = Track.ProcessProperty("Single Artist");
            Assert.Equal("1", result.Length.ToString(), "no separator -> 1 element");
            Assert.Equal("Single Artist", result[0], "element is the full string");
        }

        public static void ProcessProperty_SemicolonSeparated_SplitsCorrectly()
        {
            string[] result = Track.ProcessProperty("Artist A;Artist B;Artist C");
            Assert.Equal("3", result.Length.ToString(), "semicolon-separated -> 3 elements");
            Assert.Equal("Artist A", result[0], "first");
            Assert.Equal("Artist B", result[1], "second");
            Assert.Equal("Artist C", result[2], "third");
        }

        public static void ProcessProperty_CommaSeparated_SplitsCorrectly()
        {
            string[] result = Track.ProcessProperty("Artist A, Artist B");
            Assert.Equal("2", result.Length.ToString(), "comma-separated -> 2 elements");
            Assert.Equal("Artist A", result[0], "first");
            Assert.Equal("Artist B", result[1], "second");
        }

        public static void ProcessProperty_TrimsWhitespace()
        {
            string[] result = Track.ProcessProperty("  Artist A  ;  Artist B  ");
            Assert.Equal("Artist A", result[0], "first element trimmed");
            Assert.Equal("Artist B", result[1], "second element trimmed");
        }

        public static void ProcessProperty_SemicolonTakesPriorityOverComma()
        {
            // When both separators present, semicolon wins (it appears first in the separators array)
            string[] result = Track.ProcessProperty("Artist A;Artist B,Artist C");
            Assert.Equal("2", result.Length.ToString(), "semicolon splits first");
            Assert.Equal("Artist A", result[0], "first on semicolon split");
            Assert.Equal("Artist B,Artist C", result[1], "remainder after semicolon split");
        }
    }
}
