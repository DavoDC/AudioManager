using AudioManager.Code.Modules;
using System;
using System.IO;

namespace AudioManager
{
    /// <summary>
    /// Integration tests for Parser: read pipeline from XML files to List<TrackTag>.
    /// Uses temp directories to avoid contaminating the real ParseCache at Constants.ParseCachePath.
    /// </summary>
    internal static class ParserTests
    {
        private static string TempDir() =>
            Path.Combine(Path.GetTempPath(), $"am_parser_{Guid.NewGuid():N}");

        private static void WriteXml(string dir, string filename, TrackTag tag)
        {
            TrackXML.Write(Path.Combine(dir, filename), tag);
        }

        private static TrackTag MakeTag(string title, string artists = "Artist A") =>
            new TrackTag($"Artists/X/{title}.xml", title, artists, "Album", "2020", "1",
                         "Hip-Hop", "00:03:00.0000000", "1", "True", "500", "500");

        public static void Parser_ReadsXMLFiles_ReturnsCorrectTags()
        {
            string mirrorDir = TempDir();
            string cacheFile = Path.Combine(Path.GetTempPath(), $"am_cache_{Guid.NewGuid():N}.txt");
            try
            {
                Directory.CreateDirectory(mirrorDir);
                WriteXml(mirrorDir, "track1.xml", MakeTag("Song One"));
                WriteXml(mirrorDir, "track2.xml", MakeTag("Song Two"));

                var parser = new Parser(mirrorDir, cacheFile);

                Assert.Equal("2", parser.audioTags.Count.ToString(), "should parse 2 XML files");
                Assert.True(
                    parser.audioTags.Exists(t => t.Title == "Song One"),
                    "first track title should be parsed");
                Assert.True(
                    parser.audioTags.Exists(t => t.Title == "Song Two"),
                    "second track title should be parsed");
            }
            finally
            {
                if (Directory.Exists(mirrorDir)) Directory.Delete(mirrorDir, recursive: true);
                if (File.Exists(cacheFile)) File.Delete(cacheFile);
            }
        }

        public static void Parser_SkipsReadmeMd_OnlyXMLsInResult()
        {
            string mirrorDir = TempDir();
            string cacheFile = Path.Combine(Path.GetTempPath(), $"am_cache_{Guid.NewGuid():N}.txt");
            try
            {
                Directory.CreateDirectory(mirrorDir);
                WriteXml(mirrorDir, "track1.xml", MakeTag("Song One"));
                File.WriteAllText(Path.Combine(mirrorDir, "README.md"), "# AudioMirror\n");

                var parser = new Parser(mirrorDir, cacheFile);

                Assert.Equal("1", parser.audioTags.Count.ToString(), "README.md should be skipped");
                Assert.True(
                    parser.audioTags.Exists(t => t.Title == "Song One"),
                    "XML track should still be present");
            }
            finally
            {
                if (Directory.Exists(mirrorDir)) Directory.Delete(mirrorDir, recursive: true);
                if (File.Exists(cacheFile)) File.Delete(cacheFile);
            }
        }

        public static void Parser_NonXMLFile_ThrowsArgumentException()
        {
            string mirrorDir = TempDir();
            string cacheFile = Path.Combine(Path.GetTempPath(), $"am_cache_{Guid.NewGuid():N}.txt");
            try
            {
                Directory.CreateDirectory(mirrorDir);
                File.WriteAllText(Path.Combine(mirrorDir, "stray.txt"), "unexpected file");

                bool threw = false;
                try { new Parser(mirrorDir, cacheFile); }
                catch (ArgumentException) { threw = true; }

                Assert.True(threw, "non-XML file in mirror folder should throw ArgumentException");
            }
            finally
            {
                if (Directory.Exists(mirrorDir)) Directory.Delete(mirrorDir, recursive: true);
                if (File.Exists(cacheFile)) File.Delete(cacheFile);
            }
        }

        public static void Parser_CacheHit_ReturnsCachedTags()
        {
            // After first parse, a second Parser with the same paths should hit the cache
            // (no XML newer than cache = IsMirrorStale returns false)
            string mirrorDir = TempDir();
            string cacheFile = Path.Combine(Path.GetTempPath(), $"am_cache_{Guid.NewGuid():N}.txt");
            try
            {
                Directory.CreateDirectory(mirrorDir);
                WriteXml(mirrorDir, "track1.xml", MakeTag("Song One"));

                // First parse writes cache
                var parser1 = new Parser(mirrorDir, cacheFile);
                Assert.Equal("1", parser1.audioTags.Count.ToString(), "first parse should find 1 track");

                // Backdate the XML so cache is newer (simulating "nothing changed")
                File.SetLastWriteTime(Path.Combine(mirrorDir, "track1.xml"), DateTime.Now.AddHours(-1));

                // Second parse with stale XML (older than cache) should hit cache
                var parser2 = new Parser(mirrorDir, cacheFile);
                Assert.Equal("1", parser2.audioTags.Count.ToString(), "cache hit should return same count");
                Assert.True(
                    parser2.audioTags.Exists(t => t.Title == "Song One"),
                    "cache hit should return the same track");
            }
            finally
            {
                if (Directory.Exists(mirrorDir)) Directory.Delete(mirrorDir, recursive: true);
                if (File.Exists(cacheFile)) File.Delete(cacheFile);
            }
        }
    }
}
