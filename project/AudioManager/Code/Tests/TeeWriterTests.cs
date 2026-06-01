using System;
using System.IO;

namespace AudioManager
{
    /// <summary>
    /// Tests for TeeWriter - the dual console+file writer used for all session logging.
    /// Key invariant (DevContext.md): WriteLine(string) with embedded '\n' MUST process
    /// char-by-char via WriteCharToFile so each sub-line gets its own timestamp.
    /// </summary>
    internal static class TeeWriterTests
    {
        private static string TempFile() => Path.GetTempFileName();

        // ---- StringWriter capture (no console, no file) ----

        public static void TeeWriter_WriteChar_GoesToCaptureWriter()
        {
            var capture = new StringWriter();
            var tee = new TeeWriter(TextWriter.Null, capture);
            tee.Write('A');
            Assert.Equal("A", capture.ToString(), "written char should appear in capture writer");
        }

        public static void TeeWriter_WriteLine_GoesToCaptureWriter()
        {
            var capture = new StringWriter();
            var tee = new TeeWriter(TextWriter.Null, capture);
            tee.WriteLine("Hello World");
            Assert.True(capture.ToString().Contains("Hello World"),
                "written line should appear in capture writer");
        }

        // ---- File output ----

        public static void TeeWriter_WriteToFile_FileContainsText()
        {
            string path = TempFile();
            try
            {
                var tee = new TeeWriter(TextWriter.Null, path, addFileTimestamps: false);
                tee.WriteLine("Test Content");
                tee.CloseLogFile();

                string content = File.ReadAllText(path);
                Assert.True(content.Contains("Test Content"),
                    "file should contain the written line");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        public static void TeeWriter_WithTimestamps_FileLineHasTimestampPrefix()
        {
            // Each line written to file should be prefixed with [HH:mm:ss]
            string path = TempFile();
            try
            {
                var tee = new TeeWriter(TextWriter.Null, path, addFileTimestamps: true);
                tee.WriteLine("Timestamped Line");
                tee.CloseLogFile();

                string[] lines = File.ReadAllLines(path);
                Assert.True(lines.Length > 0, "file should have at least one line");
                Assert.True(lines[0].StartsWith("["), "timestamped line should start with [");
                Assert.True(lines[0].Contains("] Timestamped Line"),
                    "line should contain timestamp bracket and then the content");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        public static void TeeWriter_EmbeddedNewline_EachSublineHasTimestamp()
        {
            // DevContext.md invariant: WriteLine(string) with embedded '\n' must write char-by-char
            // so each sub-line gets its own [HH:mm:ss] prefix - not a single timestamp for the block.
            string path = TempFile();
            try
            {
                var tee = new TeeWriter(TextWriter.Null, path, addFileTimestamps: true);
                tee.WriteLine("Line One\nLine Two");
                tee.CloseLogFile();

                string[] lines = File.ReadAllLines(path);
                Assert.True(lines.Length >= 2, "embedded newline should produce at least 2 file lines");
                Assert.True(lines[0].StartsWith("["), "first sub-line should have timestamp prefix");
                Assert.True(lines[1].StartsWith("["), "second sub-line should have timestamp prefix");
                Assert.True(lines[0].Contains("Line One"), "first sub-line should contain first content");
                Assert.True(lines[1].Contains("Line Two"), "second sub-line should contain second content");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
