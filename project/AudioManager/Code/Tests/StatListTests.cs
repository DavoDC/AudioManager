using AudioManager.Code.Modules;
using System.Collections.Generic;
using System.Linq;

namespace AudioManager
{
    internal static class StatListTests
    {
        private static TrackTag SimpleTag(string artists, string title = "Song", string year = "2020") =>
            new TrackTag($"\\Artists\\{artists}\\Singles\\{artists} - {title}.xml",
                title, artists, "Test Album", year, "1", "Hip-Hop",
                "00:03:00.0000000", "1", "True", "500", "500");

        // ---- GetSortedFreqDist ----

        public static void GetSortedFreqDist_EmptyList_ReturnsEmpty()
        {
            var result = StatList.GetSortedFreqDist(new List<TrackTag>(), t => t.Artists).ToList();
            Assert.Equal("0", result.Count.ToString(), "empty input -> empty result");
        }

        public static void GetSortedFreqDist_SingleTag_CountsOne()
        {
            var tags = new List<TrackTag> { SimpleTag("Artist A") };
            var result = StatList.GetSortedFreqDist(tags, t => t.Artists).ToList();
            Assert.Equal("1", result.Count.ToString(), "one tag -> one entry");
            Assert.Equal("Artist A", result[0].Key, "entry key is the artist");
            Assert.Equal("1", result[0].Value.ToString(), "entry count is 1");
        }

        public static void GetSortedFreqDist_MultipleTagsSameValue_SumsCount()
        {
            var tags = new List<TrackTag>
            {
                SimpleTag("Artist A", "Song 1"),
                SimpleTag("Artist A", "Song 2"),
                SimpleTag("Artist A", "Song 3"),
            };
            var result = StatList.GetSortedFreqDist(tags, t => t.Artists).ToList();
            Assert.Equal("1", result.Count.ToString(), "3 tags with same artist -> 1 distinct entry");
            Assert.Equal("3", result[0].Value.ToString(), "count should be 3");
        }

        public static void GetSortedFreqDist_SortedDescendingByCount()
        {
            // Two tags by Artist A, one by Artist B -> Artist A should appear first
            var tags = new List<TrackTag>
            {
                SimpleTag("Artist A", "Song 1"),
                SimpleTag("Artist A", "Song 2"),
                SimpleTag("Artist B", "Song 1"),
            };
            var result = StatList.GetSortedFreqDist(tags, t => t.Artists).ToList();
            Assert.Equal("2", result.Count.ToString(), "two distinct artists");
            Assert.Equal("Artist A", result[0].Key, "most frequent artist should be first");
            Assert.Equal("2", result[0].Value.ToString(), "Artist A appears twice");
            Assert.Equal("Artist B", result[1].Key, "less frequent artist second");
            Assert.Equal("1", result[1].Value.ToString(), "Artist B appears once");
        }

        public static void GetSortedFreqDist_MultipleArtistsPerTag_CountsEachSeparately()
        {
            // Tag with semicolon-separated artists: each artist counted individually
            var tags = new List<TrackTag>
            {
                SimpleTag("Artist A;Artist B"),
            };
            var result = StatList.GetSortedFreqDist(tags, t => t.Artists).ToList();
            Assert.True(
                result.Any(kv => kv.Key == "Artist A" && kv.Value == 1),
                "Artist A from semicolon-separated field should be counted once");
            Assert.True(
                result.Any(kv => kv.Key == "Artist B" && kv.Value == 1),
                "Artist B from semicolon-separated field should be counted once");
        }

        // ---- GetDecadeFreqDist ----

        public static void GetDecadeFreqDist_NormalYears_GroupsCorrectly()
        {
            // Two 1990s tracks, one 2000s track
            var tags = new List<TrackTag>
            {
                SimpleTag("Artist A", "Song 1", year: "1995"),
                SimpleTag("Artist A", "Song 2", year: "1997"),
                SimpleTag("Artist A", "Song 3", year: "2003"),
            };
            var yearStats = new StatList("Year", tags, t => t.Year);
            var decades = StatList.GetDecadeFreqDist(yearStats).ToList();

            var nineties = decades.FirstOrDefault(d => d.Key == "1990s");
            var twoThousands = decades.FirstOrDefault(d => d.Key == "2000s");

            Assert.Equal("2", nineties.Value.ToString(), "1995 and 1997 should both land in 1990s");
            Assert.Equal("1", twoThousands.Value.ToString(), "2003 should land in 2000s");
        }
    }
}
