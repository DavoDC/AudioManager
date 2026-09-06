using System.Collections.Generic;
using System.Linq;

namespace AudioManager
{
    /// <summary>
    /// Coverage for the execution-record contract (docs/References/Execution-Record-Contract-
    /// Design.md): BuildJson's new schemaVersion/recordType/dryRun/generatedAt/confidence keys,
    /// the per-file "detail" field, and the JStr control-character escaping fix that ships in the
    /// same pass (section 6 of the design doc). Pure string assertions against BuildJson/JStr - no
    /// filesystem, no console interaction.
    /// </summary>
    internal static class MusicIntegratorExecutionRecordTests
    {
        private static MusicIntegrator.LogEntry Entry(string filename, string status = "moved") =>
            new MusicIntegrator.LogEntry { Filename = filename, Status = status };

        private static MusicIntegrator.ConfidenceRecord PopulatedConfidence()
        {
            var c = new MusicIntegrator.ConfidenceRecord
            {
                TotalFiles = 15,
                Moved = 12,
                Skipped = 3,
                ExpectedMoved = 12,
                CountOk = true,
                SanityRan = true,
                SanityChecked = 12,
            };
            c.SanityFailures.Add(("Artists\\Dave\\Singles\\Dave - Titanium.mp3", "missing"));
            c.NewFolders.Add("Artists\\Dave\\Singles");
            c.Errors.Add(("Foo - Bar.mp3", "Access to the path is denied."));
            return c;
        }

        // ---------------------------------------------------------------- 1. dry run shape

        public static void BuildJson_DryRun_EmitsRoutingRecordTypeAndNullConfidence()
        {
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry> { Entry("A.mp3") },
                dryRun: true, confidence: null);
            Assert.True(json.Contains("\"schemaVersion\": 1"), "schemaVersion 1");
            Assert.True(json.Contains("\"recordType\": \"routing\""), "recordType routing on a dry run");
            Assert.True(json.Contains("\"dryRun\": true"), "dryRun true");
            Assert.True(json.Contains("\"confidence\": null"), "confidence null on a dry run");
        }

        // ---------------------------------------------------------------- 2. real run shape

        public static void BuildJson_RealRun_EmitsExecutionRecordTypeAndFullConfidenceBlock()
        {
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry> { Entry("A.mp3") },
                dryRun: false, confidence: PopulatedConfidence());
            Assert.True(json.Contains("\"recordType\": \"execution\""), "recordType execution on a real run");
            Assert.True(json.Contains("\"dryRun\": false"), "dryRun false");
            Assert.True(json.Contains("\"totalFiles\": 15"), "countCheck.totalFiles");
            Assert.True(json.Contains("\"moved\": 12"), "countCheck.moved");
            Assert.True(json.Contains("\"skipped\": 3"), "countCheck.skipped");
            Assert.True(json.Contains("\"expectedMoved\": 12"), "countCheck.expectedMoved");
            Assert.True(json.Contains("\"ok\": true"), "countCheck.ok");
            Assert.True(json.Contains("\"ran\": true"), "sanityCheck.ran");
            Assert.True(json.Contains("\"checked\": 12"), "sanityCheck.checked");
            Assert.True(json.Contains("\"destination\": \"Artists\\\\Dave\\\\Singles\\\\Dave - Titanium.mp3\""),
                "sanityCheck.failures[].destination");
            Assert.True(json.Contains("\"reason\": \"missing\""), "sanityCheck.failures[].reason");
            Assert.True(json.Contains("\"newFolders\": [\"Artists\\\\Dave\\\\Singles\"]"), "confidence.newFolders");
            Assert.True(json.Contains("\"errorCount\": 1"), "confidence.errorCount");
            Assert.True(json.Contains("\"filename\": \"Foo - Bar.mp3\""), "confidence.errors[].filename");
            Assert.True(json.Contains("\"detail\": \"Access to the path is denied.\""), "confidence.errors[].detail");
        }

        public static void BuildJson_RealRun_SanityOkFalseWhenFailuresPresent()
        {
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry>(),
                dryRun: false, confidence: PopulatedConfidence());
            Assert.True(json.Contains("\"sanityCheck\": {"), "sanityCheck block present");
            // sanityCheck.ok must be false: PopulatedConfidence() has one failure.
            int sanityStart = json.IndexOf("\"sanityCheck\": {");
            int sanityEnd = json.IndexOf("},", sanityStart);
            string sanityBlock = json.Substring(sanityStart, sanityEnd - sanityStart);
            Assert.True(sanityBlock.Contains("\"ok\": false"), "sanityCheck.ok false when failures[] is non-empty");
        }

        public static void BuildJson_RealRun_CleanConfidence_EmptyArraysNotNull()
        {
            var clean = new MusicIntegrator.ConfidenceRecord
            {
                TotalFiles = 3, Moved = 3, Skipped = 0, ExpectedMoved = 3,
                CountOk = true, SanityRan = true, SanityChecked = 3,
            };
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry>(), dryRun: false, confidence: clean);
            Assert.True(json.Contains("\"failures\": []"), "empty failures array, never omitted or null");
            Assert.True(json.Contains("\"newFolders\": []"), "empty newFolders array");
            Assert.True(json.Contains("\"errorCount\": 0"), "errorCount zero");
            Assert.True(json.Contains("\"errors\": []"), "empty errors array");
        }

        // ---------------------------------------------------------------- 3. detail field

        public static void BuildJson_DetailEmittedForErrorEntry_EmptyStringWhenNull()
        {
            var withDetail = new MusicIntegrator.LogEntry { Filename = "bad.mp3", Status = "error", Detail = "routing failed: disk full" };
            var noDetail = new MusicIntegrator.LogEntry { Filename = "ok.mp3", Status = "moved", Detail = null };
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry> { withDetail, noDetail });
            Assert.True(json.Contains("\"detail\": \"routing failed: disk full\""), "detail text present for an error entry");
            Assert.True(json.Contains("\"detail\": \"\""), "null Detail serializes as empty string, never null");
        }

        // ---------------------------------------------------------------- 4. round-trip validity

        public static void BuildJson_PopulatedOutput_IsBalancedAndControlCharacterFree()
        {
            var entry = new MusicIntegrator.LogEntry
            {
                Filename = "A.mp3", Artists = "A", Title = "T", Album = "Al",
                Destination = "Artists\\A\\A.mp3", Reason = "r", Status = "moved", Detail = "d\ttab",
            };
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry> { entry },
                dryRun: false, confidence: PopulatedConfidence());

            int braceBalance = json.Count(c => c == '{') - json.Count(c => c == '}');
            int bracketBalance = json.Count(c => c == '[') - json.Count(c => c == ']');
            Assert.Equal("0", braceBalance.ToString());
            Assert.Equal("0", bracketBalance.ToString());

            // The tab embedded in "d\ttab" must have been escaped to the two characters \ and t
            // inside the detail field's own value, not left as a literal 0x09. This is checked on
            // the "detail" line specifically rather than the whole document: BuildJson pretty-
            // prints with AppendLine, so the document as a whole legitimately contains \r\n line
            // separators, which are themselves control characters below U+0020.
            Assert.True(json.Contains("\"detail\": \"d\\ttab\""), "embedded tab escaped inside the detail value, not left raw");
        }

        // ---------------------------------------------------------------- 5. JStr escaping

        public static void JStr_EscapesTabBackspaceFormfeedAndCarriageReturn()
        {
            var entry = new MusicIntegrator.LogEntry { Filename = "x.mp3", Status = "moved", Detail = "a\tb\bc\fd\re" };
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry> { entry });
            Assert.True(json.Contains("\"detail\": \"a\\tb\\bc\\fd\\re\""),
                "tab/backspace/formfeed/carriage-return all escaped, \\r not stripped");
            Assert.True(!json.Contains("\"detail\": \"a\tb"), "no raw control characters leaked into the output");
        }

        public static void JStr_EscapesBareControlCharacterAsUnicodeEscape()
        {
            // U+0001 is not one of the named short escapes, so it must fall back to the
            // six-character \u escape form: backslash, u, 0, 0, 0, 1.
            var entry = new MusicIntegrator.LogEntry { Filename = "y.mp3", Status = "moved", Detail = "a" + '\u0001' + "b" };
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry> { entry });
            string expectedEscape = "\\u0001";
            Assert.True(json.Contains("\"detail\": \"a" + expectedEscape + "b\""),
                "bare U+0001 control character escaped as \\u0001, not emitted raw");
        }
    }
}
