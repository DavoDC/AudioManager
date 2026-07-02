using AudioManager.Code.Modules;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace AudioManager
{
    /// <summary>
    /// Tests for the analysis --json-output data contract (StatsJson). Assertions are substring-based
    /// (the repo has no JSON parser dependency), which is enough to lock the field names and values
    /// the GUI depends on.
    /// </summary>
    internal static class StatsJsonTests
    {
        // relPath, title, artists, album, year, trackNo, genres, length, coverCount, compilation, coverW, coverH
        private static TrackTag Tag(string artists, string title, string year, string genres,
                                    string length = "00:03:00.0000000", string w = "1000", string h = "1000",
                                    string album = "Album") =>
            new TrackTag($"\\Artists\\{artists}\\Singles\\{artists} - {title}.xml",
                title, artists, album, year, "1", genres, length, "1", "False", w, h);

        private static List<TrackTag> Sample() => new List<TrackTag>
        {
            Tag("Artist A", "Song 1", "2020", "Hip Hop", w: "1200", h: "1200"),
            Tag("Artist A", "Song 2", "2021", "Hip Hop", w: "800",  h: "800"),
            Tag("Artist B", "Song 3", "1995", "Rock",    w: "500",  h: "500"),
        };

        public static void Build_IncludesCoreSummaryCounts()
        {
            string json = StatsJson.Build(Sample(), 30_000_000L);
            Assert.True(json.Contains("\"trackCount\": 3"), "track count = 3");
            Assert.True(json.Contains("\"artistCount\": 2"), "distinct artists = 2");
            Assert.True(json.Contains("\"schemaVersion\": 1"), "schema version present");
        }

        public static void Build_GroupsDecadesFromYears()
        {
            string json = StatsJson.Build(Sample(), 0);
            Assert.True(json.Contains("2020s"), "2020 and 2021 collapse to 2020s");
            Assert.True(json.Contains("1990s"), "1995 lands in 1990s");
        }

        public static void Build_TagCompletenessIsFullWhenAllTagsPresent()
        {
            string json = StatsJson.Build(Sample(), 0);
            Assert.True(json.Contains("\"tagCompleteness\": {"), "tagCompleteness block present");
            Assert.True(json.Contains("\"percent\": 100"), "all sample tags complete -> 100%");
        }

        public static void Build_MissingTagLowersCompleteness()
        {
            var tags = Sample();
            tags.Add(Tag("Artist C", "Song 4", "Missing", "Missing")); // year+genre missing
            string json = StatsJson.Build(tags, 0);
            Assert.True(json.Contains("\"complete\": 3"), "3 of 4 tags complete");
            Assert.True(json.Contains("\"total\": 4"), "4 tags total");
        }

        public static void Build_CoverCoverageCountsOnly800Plus()
        {
            // Two tags >=800 (1200,800), one below (500) -> covered = 2 of 3
            string json = StatsJson.Build(Sample(), 0);
            Assert.True(json.Contains("\"coverCoverage800\": {"), "coverage block present");
            Assert.True(json.Contains("\"covered\": 2"), "two tags at >=800px");
            Assert.True(json.Contains("\"subMin800\": 1"), "one tag below 800px");
        }

        public static void Build_AgeDistributionBucketsExist()
        {
            string json = StatsJson.Build(Sample(), 0);
            Assert.True(json.Contains("\"ageDistribution\": ["), "age distribution present");
            Assert.True(json.Contains("0-2y"), "youngest bucket label present");
            Assert.True(json.Contains("20y+"), "oldest bucket label present");
        }

        public static void Build_UsesInvariantDecimalUnderCommaCulture()
        {
            // Regression guard: a comma-decimal culture (e.g. de-DE) must not corrupt JSON numbers.
            var original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                string json = StatsJson.Build(Sample(), 0);
                // 3x 3-minute tracks = 540s = 0.15h -> must serialize with a '.' not ','
                Assert.True(json.Contains("\"totalPlaybackHours\": 0.2") || json.Contains("\"totalPlaybackHours\": 0.1"),
                    "playback hours uses '.' decimal under de-DE culture");
                Assert.True(!json.Contains("0,2") && !json.Contains("0,1"), "no comma-decimals leaked into JSON");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        public static void Build_ProducesBalancedBraces()
        {
            string json = StatsJson.Build(Sample(), 123);
            int open = 0, close = 0;
            foreach (char c in json) { if (c == '{') open++; if (c == '}') close++; }
            Assert.Equal(open.ToString(), close.ToString(), "every { has a matching }");
            int bo = 0, bc = 0;
            foreach (char c in json) { if (c == '[') bo++; if (c == ']') bc++; }
            Assert.Equal(bo.ToString(), bc.ToString(), "every [ has a matching ]");
        }
    }
}
