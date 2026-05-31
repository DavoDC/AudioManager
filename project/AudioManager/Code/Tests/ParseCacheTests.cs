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

        public static void IsMirrorStale_EmptyMirror_ReturnsTrueToForceMiss()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"am_mirror_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                // Empty mirror (Reflector crash mid-regen) must force a cache miss
                bool stale = ParseCache.IsMirrorStale(dir, DateTime.Now.AddHours(-1));
                Assert.True(stale, "empty mirror should be stale to prevent false cache hits");
            }
            finally { Directory.Delete(dir, true); }
        }

        public static void IsMirrorStale_XmlOlderThanCache_ReturnsFalse()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"am_mirror_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            string xml = Path.Combine(dir, "test.xml");
            try
            {
                File.WriteAllText(xml, "<x/>");
                File.SetLastWriteTime(xml, DateTime.Now.AddHours(-1));
                // Cache is "now", XML is 1h old - mirror is fresh, cache hit valid
                bool stale = ParseCache.IsMirrorStale(dir, DateTime.Now);
                Assert.True(!stale, "XML older than cache = mirror not stale");
            }
            finally { Directory.Delete(dir, true); }
        }

        public static void IsMirrorStale_XmlNewerThanCache_ReturnsTrue()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"am_mirror_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            string xml = Path.Combine(dir, "test.xml");
            try
            {
                File.WriteAllText(xml, "<x/>");
                // Cache is 1h old, XML was just created - mirror is newer than cache
                bool stale = ParseCache.IsMirrorStale(dir, DateTime.Now.AddHours(-1));
                Assert.True(stale, "XML newer than cache = mirror is stale");
            }
            finally { Directory.Delete(dir, true); }
        }

        public static void TryDeserialize_TooFewFields_ReturnsFalse()
        {
            string path = TempCachePath();
            try
            {
                // Row with 11 fields instead of the required 12 - must be rejected
                File.WriteAllText(path, "PARSE_CACHE_V1\nfield1|field2|field3|field4|field5|field6|field7|field8|field9|field10|field11");
                bool ok = ParseCache.TryDeserialize(path, out var tags);
                Assert.True(!ok, "row with too few fields should return false");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        public static void TryDeserialize_TooManyFields_ReturnsFalse()
        {
            string path = TempCachePath();
            try
            {
                // 13 fields (e.g. a title containing '|') - must be rejected
                File.WriteAllText(path, "PARSE_CACHE_V1\nf1|f2|f3|f4|f5|f6|f7|f8|f9|f10|f11|f12|extra");
                bool ok = ParseCache.TryDeserialize(path, out var tags);
                Assert.True(!ok, "row with too many fields (e.g. pipe in field value) should return false");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
