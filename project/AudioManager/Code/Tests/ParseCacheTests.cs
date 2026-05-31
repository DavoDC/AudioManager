using AudioManager.Code.Modules;
using System;
using System.Collections.Generic;
using System.IO;

namespace AudioManager
{
    internal static class ParseCacheTests
    {
        private static string TempCachePath() =>
            Path.Combine(Path.GetTempPath(), $"am_cache_{Guid.NewGuid():N}.txt");

        private static TrackTag MakeSample(string suffix) =>
            new TrackTag($"Artists/X/test{suffix}.xml", $"Title {suffix}", $"Artist {suffix}",
                         $"Album {suffix}", "2024", "1", "Pop; Rock", "00:03:30.0000000",
                         "1", "False", "500", "499");

        public static void RoundTrip_SingleTrack_AllFieldsPreserved()
        {
            string path = TempCachePath();
            try
            {
                var original = new List<TrackTag> { MakeSample("A") };
                ParseCache.Save(path, original);
                ParseCache.TryDeserialize(path, out var loaded);

                Assert.Equal("1", loaded.Count.ToString(), "count");
                Assert.Equal("Title A", loaded[0].Title,          "title");
                Assert.Equal("Artist A", loaded[0].Artists,        "artists");
                Assert.Equal("Album A", loaded[0].Album,           "album");
                Assert.Equal("2024", loaded[0].Year,               "year");
                Assert.Equal("1", loaded[0].TrackNumber,            "tracknum");
                Assert.Equal("Pop; Rock", loaded[0].Genres,        "genres");
                Assert.Equal("00:03:30.0000000", loaded[0].Length, "length");
                Assert.Equal("1", loaded[0].AlbumCoverCount,       "covercount");
                Assert.Equal("False", loaded[0].Compilation,       "compilation");
                Assert.Equal("500", loaded[0].CoverWidth,          "coverwidth");
                Assert.Equal("499", loaded[0].CoverHeight,         "coverheight");
                Assert.Equal("Artists/X/testA.xml", loaded[0].RelPath, "relpath");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        public static void RoundTrip_EmptyList_LoadsWithZeroTracks()
        {
            string path = TempCachePath();
            try
            {
                ParseCache.Save(path, new List<TrackTag>());
                bool ok = ParseCache.TryDeserialize(path, out var loaded);
                Assert.True(ok, "empty list should deserialize ok");
                Assert.Equal("0", loaded.Count.ToString(), "count");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        public static void RoundTrip_MultipleTracks_OrderPreserved()
        {
            string path = TempCachePath();
            try
            {
                var tracks = new List<TrackTag> { MakeSample("B"), MakeSample("C"), MakeSample("D") };
                ParseCache.Save(path, tracks);
                ParseCache.TryDeserialize(path, out var loaded);

                Assert.Equal("3", loaded.Count.ToString(), "count");
                Assert.Equal("Title B", loaded[0].Title, "first");
                Assert.Equal("Title C", loaded[1].Title, "second");
                Assert.Equal("Title D", loaded[2].Title, "third");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        public static void TryDeserialize_MissingFile_ReturnsFalse()
        {
            bool ok = ParseCache.TryDeserialize(TempCachePath(), out var tags);
            Assert.True(!ok,    "missing file should return false");
            Assert.True(tags == null, "missing file should yield null tags");
        }

        public static void TryDeserialize_WrongHeader_ReturnsFalse()
        {
            string path = TempCachePath();
            try
            {
                File.WriteAllText(path, "WRONG_HEADER\nsome|data|here");
                bool ok = ParseCache.TryDeserialize(path, out var tags);
                Assert.True(!ok, "wrong header should return false");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        public static void AnyXmlNewerThan_NoXmlFiles_ReturnsFalse()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"am_mirror_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                // No XML files in directory - should return false
                bool result = ParseCache.AnyXmlNewerThan(dir, DateTime.Now.AddHours(-1));
                Assert.True(!result, "no XMLs should return false");
            }
            finally { Directory.Delete(dir, true); }
        }

        public static void AnyXmlNewerThan_XmlOlderThanThreshold_ReturnsFalse()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"am_mirror_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            string xml = Path.Combine(dir, "test.xml");
            try
            {
                File.WriteAllText(xml, "<x/>");
                // Set mtime to 1 hour ago
                File.SetLastWriteTime(xml, DateTime.Now.AddHours(-1));
                // Threshold is "now" - XML is older than threshold
                bool result = ParseCache.AnyXmlNewerThan(dir, DateTime.Now);
                Assert.True(!result, "old XML should return false");
            }
            finally { Directory.Delete(dir, true); }
        }

        public static void AnyXmlNewerThan_XmlNewerThanThreshold_ReturnsTrue()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"am_mirror_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            string xml = Path.Combine(dir, "test.xml");
            try
            {
                File.WriteAllText(xml, "<x/>");
                // Threshold is 1 hour ago - XML (just created) is newer
                bool result = ParseCache.AnyXmlNewerThan(dir, DateTime.Now.AddHours(-1));
                Assert.True(result, "new XML should return true");
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
