using AudioManager.Code.Modules;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;

namespace AudioManager
{
    /// <summary>
    /// Tests for the per-track data contract (TracksJson) the GUI's Library Browser consumes.
    /// Substring/regex-based (the repo has no JSON parser dependency).
    /// </summary>
    internal static class TracksJsonTests
    {
        private static TrackTag Tag(string artists, string title, string year, string genres,
                                    string length = "00:03:00.0000000", string w = "1000", string h = "1000",
                                    string album = "Album", string comp = "False") =>
            new TrackTag($"\\Artists\\{artists}\\Singles\\{artists} - {title}.xml",
                title, artists, album, year, "1", genres, length, "1", comp, w, h);

        private static List<TrackTag> Sample() => new List<TrackTag>
        {
            Tag("21 Savage", "see the real", "2024", "Rap; Hip Hop", w: "1200", h: "1200"),
            Tag("Metallica", "Master of Puppets", "1986", "Metal", w: "500", h: "500", comp: "True"),
        };

        public static void Build_EmitsOneEntryPerTrack()
        {
            string json = TracksJson.Build(Sample());
            Assert.True(json.Contains("\"trackCount\": 2"), "top-level trackCount = 2");
            int titles = Regex.Matches(json, "\"title\":").Count;
            Assert.Equal("2", titles.ToString(), "one title field per track");
        }

        public static void Build_DerivesDecadeAndPrimaryGenre()
        {
            string json = TracksJson.Build(Sample());
            Assert.True(json.Contains("\"decade\": \"2020s\""), "2024 -> 2020s");
            Assert.True(json.Contains("\"decade\": \"1980s\""), "1986 -> 1980s");
            Assert.True(json.Contains("\"primaryGenre\": \"Rap\""), "first of 'Rap; Hip Hop' is Rap");
        }

        public static void Build_MissingYear_DecadeNull()
        {
            var tags = new List<TrackTag> { Tag("Unknown", "Song", "Missing", "Rock") };
            string json = TracksJson.Build(tags);
            Assert.True(json.Contains("\"decade\": null"), "unparseable year -> null decade");
        }

        public static void Build_ReconstructsMp3FilePath()
        {
            string json = TracksJson.Build(Sample());
            // The id field is the .xml RelPath (expected); filePath must be the reconstructed .mp3.
            foreach (Match m in Regex.Matches(json, "\"filePath\": \"([^\"]*)\""))
            {
                string path = m.Groups[1].Value;
                Assert.True(path.Contains("Audio"), "filePath rooted under the Audio library");
                Assert.True(path.EndsWith(".mp3"), "filePath points at an .mp3, not the .xml");
            }
        }

        public static void Build_AlbumArtStatusFlags()
        {
            string json = TracksJson.Build(Sample());
            // 1200px track is hi-res; 500px track is not.
            Assert.True(json.Contains("\"hiResArt\": true"), "1200px track flagged hi-res");
            Assert.True(json.Contains("\"hiResArt\": false"), "500px track flagged not hi-res");
            Assert.True(json.Contains("\"hasArt\": true"), "both tracks have art");
        }

        public static void Build_CompilationFlagIsBoolean()
        {
            string json = TracksJson.Build(Sample());
            Assert.True(json.Contains("\"compilation\": true"), "Metallica tag marked compilation");
            Assert.True(json.Contains("\"compilation\": false"), "21 Savage tag not a compilation");
        }

        public static void Build_UsesInvariantDecimalUnderCommaCulture()
        {
            var original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                // 3m30.5s length -> 210.5 lengthSeconds must serialize with '.' not ','
                var tags = new List<TrackTag> { Tag("A", "B", "2020", "Rock", length: "00:03:30.5000000") };
                string json = TracksJson.Build(tags);
                Assert.True(json.Contains("\"lengthSeconds\": 210.5"), "lengthSeconds uses '.' decimal under de-DE");
                Assert.True(!json.Contains("210,5"), "no comma-decimal leaked");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        public static void Build_ProducesBalancedBraces()
        {
            string json = TracksJson.Build(Sample());
            int open = 0, close = 0;
            foreach (char c in json) { if (c == '{') open++; if (c == '}') close++; }
            Assert.Equal(open.ToString(), close.ToString(), "every { has a matching }");
        }
    }
}
