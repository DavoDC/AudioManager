using System.Linq;

namespace AudioManager
{
    /// <summary>
    /// Tests for the selective-integration manifest (integrate --manifest).
    /// Locks the parse contract the GUI writes and the two-way filename matching
    /// (raw dry-run name OR canonical TagFixer rename) that makes manifests
    /// survive TagFixer renames between the dry run and the real run.
    /// </summary>
    internal static class IntegrationManifestTests
    {
        private const string SampleJson = @"[
          {""filename"": ""21 Savage - see the real (Explicit).mp3"", ""artist"": ""21 Savage"", ""title"": ""see the real""},
          {""filename"": ""Aurora Bytes - Nightfall.mp3"", ""artist"": ""Aurora Bytes"", ""title"": ""Nightfall""}
        ]";

        public static void Parse_ReadsAllEntries()
        {
            var m = IntegrationManifest.Parse(SampleJson, out string error);
            Assert.True(error == null, $"no parse error, got: {error}");
            Assert.True(m.Entries.Count == 2, "two entries parsed");
            Assert.True(m.Entries[0].Artist == "21 Savage", "artist field mapped");
            Assert.True(m.Entries[1].Title == "Nightfall", "title field mapped");
        }

        public static void Matches_ByExactFilename()
        {
            var m = IntegrationManifest.Parse(SampleJson, out _);
            Assert.True(m.Matches("21 Savage - see the real (Explicit).mp3"), "raw dry-run filename matches");
            Assert.True(!m.Matches("Some Other - Song.mp3"), "unlisted file does not match");
        }

        public static void Matches_ByCanonicalRenameAfterTagFixer()
        {
            // Real run: TagFixer renamed the file to "{artist} - {title}.mp3" before
            // the integrator listed NewMusic - the manifest must still match it.
            var m = IntegrationManifest.Parse(SampleJson, out _);
            Assert.True(m.Matches("21 Savage - see the real.mp3"), "canonical post-rename name matches");
        }

        public static void Matches_IsCaseInsensitive()
        {
            var m = IntegrationManifest.Parse(SampleJson, out _);
            Assert.True(m.Matches("AURORA BYTES - NIGHTFALL.MP3"), "matching ignores case");
        }

        public static void Parse_HandlesEscapedQuotesAndUnicode()
        {
            var m = IntegrationManifest.Parse(
                @"[{""filename"": ""AC\/DC - T.N.T..mp3"", ""artist"": ""AC\/DC"", ""title"": ""Say \""Hi\"" & Go""}]",
                out string error);
            Assert.True(error == null, $"no parse error, got: {error}");
            Assert.True(m.Entries[0].Title == "Say \"Hi\" & Go", "escapes decoded");
            Assert.True(m.Matches("AC/DC - T.N.T..mp3"), "escaped filename matches");
        }

        public static void Parse_RejectsGarbageAndEmpty()
        {
            Assert.True(IntegrationManifest.Parse("not json", out string e1) == null && e1 != null,
                "garbage rejected with error");
            Assert.True(IntegrationManifest.Parse("", out string e2) == null && e2 != null,
                "empty text rejected with error");
            Assert.True(IntegrationManifest.Parse("[]", out _)?.Entries.Count == 0,
                "empty array parses to zero entries (Load() then refuses it)");
        }

        public static void Parse_RejectsEntryWithNoUsableKeys()
        {
            var m = IntegrationManifest.Parse(@"[{""artist"": ""Solo Artist""}]", out string error);
            Assert.True(m == null && error != null, "entry without filename or artist+title rejected");
        }

        public static void UnmatchedEntries_ReportsStaleScanEntries()
        {
            var m = IntegrationManifest.Parse(SampleJson, out _);
            m.Matches("Aurora Bytes - Nightfall.mp3");
            var unmatched = m.UnmatchedEntries();
            Assert.True(unmatched.Count == 1, "one entry never matched");
            Assert.True(unmatched.Single().Artist == "21 Savage", "the unmatched entry is the 21 Savage one");
        }

        public static void Parse_ToleratesNonStringValues()
        {
            var m = IntegrationManifest.Parse(
                @"[{""filename"": ""A - B.mp3"", ""accepted"": true, ""order"": 3}]", out string error);
            Assert.True(error == null && m.Entries.Count == 1, "non-string values skipped without error");
        }

        // ---------------------------------------------- dupResolution (GUI review-stage override)

        private const string DupJson = @"[
          {""filename"": ""Kendrick Lamar - HUMBLE.mp3"", ""artist"": ""Kendrick Lamar"", ""title"": ""HUMBLE."",
           ""dupResolution"": ""L""}
        ]";

        public static void Parse_ReadsDupResolutionField()
        {
            var m = IntegrationManifest.Parse(DupJson, out string error);
            Assert.True(error == null, $"no parse error, got: {error}");
            Assert.Equal("L", m.Entries[0].DupResolution);
        }

        public static void GetDupResolution_MatchesByExactFilename()
        {
            var m = IntegrationManifest.Parse(DupJson, out _);
            char r = m.GetDupResolution("Kendrick Lamar - HUMBLE.mp3", "Kendrick Lamar", "HUMBLE.");
            Assert.Equal("L", r.ToString());
        }

        public static void GetDupResolution_FallsBackToCanonicalArtistTitleAfterRename()
        {
            // Real run: TagFixer already renamed the file before the integrator listed NewMusic,
            // so the on-disk filename no longer matches the dry-run scan's filename - same
            // survival requirement as Matches_ByCanonicalRenameAfterTagFixer above.
            var m = IntegrationManifest.Parse(DupJson, out _);
            char r = m.GetDupResolution("some-other-on-disk-name.mp3", "Kendrick Lamar", "HUMBLE.");
            Assert.Equal("L", r.ToString());
        }

        public static void GetDupResolution_ReturnsNulCharWhenUnresolved()
        {
            var m = IntegrationManifest.Parse(SampleJson, out _); // entries with no dupResolution field
            char r = m.GetDupResolution("21 Savage - see the real (Explicit).mp3", "21 Savage", "see the real");
            Assert.Equal("\0", r.ToString());
        }

        public static void GetDupResolution_IgnoresGarbageValue()
        {
            var m = IntegrationManifest.Parse(
                @"[{""filename"": ""A - B.mp3"", ""artist"": ""A"", ""title"": ""B"", ""dupResolution"": ""Z""}]",
                out _);
            Assert.Equal("\0", m.GetDupResolution("A - B.mp3", "A", "B").ToString());
        }
    }
}
