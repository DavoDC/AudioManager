namespace AudioManager
{
    /// <summary>
    /// Tests for Reflector.SanitiseFilename - a pure static function called by TagFixer,
    /// MusicIntegrator, and LibChecker for all filename and folder-name sanitization.
    /// </summary>
    internal static class ReflectorTests
    {
        // ---- SanitiseFilename ----

        public static void SanitiseFilename_PlainAscii_Unchanged()
        {
            Assert.Equal("Artist Name - Song Title", Reflector.SanitiseFilename("Artist Name - Song Title"),
                "plain ASCII with hyphen and spaces should be unchanged");
        }

        public static void SanitiseFilename_Colon_ReplacedWithUnderscore()
        {
            // Documented in CLAUDE.md and DevContext.md: "Colons in ID3 album tags become underscores"
            Assert.Equal("Album_ Live", Reflector.SanitiseFilename("Album: Live"),
                "colon is an invalid Windows filename char -> replaced with underscore");
        }

        public static void SanitiseFilename_QuestionMark_ReplacedWithUnderscore()
        {
            Assert.Equal("Song_", Reflector.SanitiseFilename("Song?"),
                "question mark is an invalid filename char -> replaced with underscore");
        }

        public static void SanitiseFilename_AsteriskAndPipe_ReplacedWithUnderscore()
        {
            Assert.Equal("Song _ Live_Mix", Reflector.SanitiseFilename("Song * Live|Mix"),
                "asterisk and pipe are invalid filename chars -> replaced with underscores");
        }

        public static void SanitiseFilename_Parentheses_Preserved()
        {
            // Parentheses are legal in Windows filenames and must be preserved intact
            // (important: TagFixer strips parentheticals from TAGS, not from filenames)
            Assert.Equal("Song (feat. Artist)", Reflector.SanitiseFilename("Song (feat. Artist)"),
                "parentheses and periods should be preserved (legal filename chars)");
        }

        public static void SanitiseFilename_EmptyString_ReturnsEmpty()
        {
            Assert.Equal("", Reflector.SanitiseFilename(""),
                "empty input should return empty string");
        }

        public static void SanitiseFilename_PreservesCase()
        {
            // LibChecker.CheckFilenameForStr relies on case being preserved (not lowercased).
            // If this breaks, LibChecker will produce false-positive filename warnings.
            Assert.Equal("Artist Beta", Reflector.SanitiseFilename("Artist Beta"),
                "SanitiseFilename must NOT lowercase: case is used in LibChecker filename checks");
        }

        public static void SanitiseFilename_ForwardSlash_ReplacedWithUnderscore()
        {
            Assert.Equal("Artist_Album", Reflector.SanitiseFilename("Artist/Album"),
                "forward slash is an invalid filename char -> replaced with underscore");
        }
    }
}
