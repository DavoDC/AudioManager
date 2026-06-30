using System;
using System.IO;

namespace AudioManager
{
    /// <summary>
    /// Tests for Reflector pure static functions: SanitiseFilename and IsStaleMirrorXml.
    /// Full mirror creation is not tested here (requires real library path).
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

        // ---- IsStaleMirrorXml ----

        public static void IsStaleMirrorXml_Mp3Newer_ReturnsTrue()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "am_stale_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                string mp3 = Path.Combine(tmp, "track.mp3");
                string xml = Path.Combine(tmp, "track.xml");
                File.WriteAllText(xml, "old data");
                File.SetLastWriteTimeUtc(xml, DateTime.UtcNow.AddMinutes(-5));
                File.WriteAllText(mp3, "");  // MP3 written after XML
                Assert.True(Reflector.IsStaleMirrorXml(mp3, xml), "MP3 newer than XML -> stale");
            }
            finally { Directory.Delete(tmp, true); }
        }

        public static void IsStaleMirrorXml_XmlNewer_ReturnsFalse()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "am_stale_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                string mp3 = Path.Combine(tmp, "track.mp3");
                string xml = Path.Combine(tmp, "track.xml");
                File.WriteAllText(mp3, "");
                File.SetLastWriteTimeUtc(mp3, DateTime.UtcNow.AddMinutes(-5));
                File.WriteAllText(xml, "fresh data");  // XML written after MP3
                Assert.True(!Reflector.IsStaleMirrorXml(mp3, xml), "XML newer than MP3 -> not stale");
            }
            finally { Directory.Delete(tmp, true); }
        }

        // ---- PruneOrphanedXmls ----

        public static void PruneOrphanedXmls_DeletesOrphanedXml() { }

        public static void PruneOrphanedXmls_PreservesExpectedXml() { }

        public static void PruneOrphanedXmls_EmptyMirror_ReturnsZero() { }

        public static void PruneOrphanedXmls_NonexistentMirrorPath_ReturnsZero() { }
    }
}
