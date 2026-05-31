using AudioManager.Code.Modules;
using System;
using System.IO;

namespace AudioManager
{
    /// <summary>
    /// Tests for the TrackXML round-trip: write-then-read must be lossless.
    /// This verifies the AudioMirror XML format contract between Reflector (writer) and Parser/TrackTag (reader).
    /// </summary>
    internal static class TrackXMLTests
    {
        private static string TempXmlPath() =>
            Path.Combine(Path.GetTempPath(), $"am_xml_{Guid.NewGuid():N}.xml");

        private static TrackTag SampleTag() =>
            new TrackTag("Artists/X/test.xml", "Song Title", "Artist Name",
                         "Album Name", "2020", "3", "Hip-Hop", "00:03:30.0000000",
                         "1", "True", "500", "499");

        public static void TrackXML_RoundTrip_AllFieldsPreserved()
        {
            string path = TempXmlPath();
            try
            {
                var tag = SampleTag();
                new TrackXML(path, tag);            // write
                var loaded = new TrackXML(path);    // read back

                Assert.Equal("Song Title",         loaded.Title,           "Title");
                Assert.Equal("Artist Name",        loaded.Artists,         "Artists");
                Assert.Equal("Album Name",         loaded.Album,           "Album");
                Assert.Equal("2020",               loaded.Year,            "Year");
                Assert.Equal("3",                  loaded.TrackNumber,     "TrackNumber");
                Assert.Equal("Hip-Hop",            loaded.Genres,          "Genres");
                Assert.Equal("00:03:30.0000000",   loaded.Length,          "Length");
                Assert.Equal("1",                  loaded.AlbumCoverCount, "AlbumCoverCount");
                Assert.Equal("True",               loaded.Compilation,     "Compilation");
                Assert.Equal("500",                loaded.CoverWidth,      "CoverWidth");
                Assert.Equal("499",                loaded.CoverHeight,     "CoverHeight");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        public static void TrackXML_RoundTrip_SpecialCharsInFields_Preserved()
        {
            // XmlDocument encodes &, <, > on write and decodes on read - must be lossless
            string path = TempXmlPath();
            try
            {
                var tag = new TrackTag("Artists/X/test.xml", "Track & Roll",
                    "Artist <Name>", "Album \"Quoted\"", "2020", "1", "Pop",
                    "00:03:00.0000000", "1", "True", "500", "500");
                new TrackXML(path, tag);
                var loaded = new TrackXML(path);

                Assert.Equal("Track & Roll",       loaded.Title,   "ampersand in title preserved");
                Assert.Equal("Artist <Name>",      loaded.Artists, "angle bracket in artists preserved");
                Assert.Equal("Album \"Quoted\"",   loaded.Album,   "quotes in album preserved");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        public static void TrackXML_ReadMissingElement_ReturnsEmpty()
        {
            // When an XML element is absent (e.g. old mirror files pre-CoverWidth), read returns ""
            string path = TempXmlPath();
            try
            {
                File.WriteAllText(path, "<Track><Title>Song A</Title><Artists>Artist A</Artists></Track>");
                var loaded = new TrackXML(path);
                Assert.Equal("Song A",  loaded.Title,       "Title present in XML");
                Assert.Equal("Artist A", loaded.Artists,    "Artists present in XML");
                Assert.Equal("",        loaded.CoverWidth,  "missing CoverWidth returns empty");
                Assert.Equal("",        loaded.CoverHeight, "missing CoverHeight returns empty");
                Assert.Equal("",        loaded.Album,       "missing Album returns empty");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
