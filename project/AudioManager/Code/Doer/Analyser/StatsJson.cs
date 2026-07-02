using AudioManager.Code.Modules;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TagList = System.Collections.Generic.List<AudioManager.Code.Modules.TrackTag>;

namespace AudioManager
{
    /// <summary>
    /// Emits the full library statistics as a single structured JSON document for the GUI.
    ///
    /// Why this exists: the GUI's Statistics/Library tabs need every number the text
    /// AudioReport.md contains, but parsing markdown is brittle. Rather than reimplement
    /// all the distribution/age/cover logic in Python (which would silently drift from the
    /// C# Analyser over time), this exporter reuses the SAME StatList primitives the text
    /// report uses, so the JSON and the report can never disagree.
    ///
    /// Data contract: see docs/References/AnalysisJson-Format.md and the "analysis --json-output"
    /// section of docs/Development/fable-gui/fable-brief.md. Bump SchemaVersion on any breaking change.
    /// </summary>
    internal static class StatsJson
    {
        /// <summary>Contract version. Increment when a field is removed or its meaning changes.</summary>
        public const int SchemaVersion = 1;

        /// <summary>Fixed output filename (latest run wins) so the GUI reads a deterministic path.</summary>
        public const string OutputFileName = "analysis-stats.json";

        /// <summary>
        /// Production entry point: walks the audio folder for the real on-disk size, builds the
        /// JSON, and writes it to logs/analysis-stats.json. Returns the path written.
        /// </summary>
        public static string Write(TagList audioTags)
        {
            long libraryBytes = TryGetLibraryBytes();
            string json = Build(audioTags, libraryBytes);

            if (!Directory.Exists(Constants.LogsPath))
                Directory.CreateDirectory(Constants.LogsPath);
            string outPath = Path.Combine(Constants.LogsPath, OutputFileName);
            File.WriteAllText(outPath, json, Encoding.UTF8);
            return outPath;
        }

        /// <summary>
        /// Pure, unit-testable builder: produces the JSON document from the tag list plus a
        /// pre-computed library byte count (0 if unknown). No file or disk I/O here.
        /// </summary>
        public static string Build(TagList audioTags, long totalLibraryBytes)
        {
            int trackCount = audioTags.Count;

            // Reuse the exact primitives the text report uses - guarantees JSON == report.
            var genreStats = new StatList("Genre", audioTags, t => t.Genres);
            var yearStats = new StatList("Year", audioTags, t => t.Year);
            var decadeDist = StatList.GetDecadeFreqDist(yearStats);
            var artistStatsAll = new StatList("Artists", audioTags, t => t.Artists);
            var tagsExclMusivation = audioTags.Where(t => !t.Genres.Contains("Musivation")).ToList();
            var artistStatsExcl = new StatList("Artists", tagsExclMusivation, t => t.Artists);

            int artistCount = artistStatsAll.SortedFreqDist.Count();
            int genreCount = genreStats.SortedFreqDist.Count();

            // ---- Playback / length ----
            var durations = audioTags
                .Select(t => TryParseSeconds(t.Length))
                .Where(s => s.HasValue)
                .Select(s => s.Value)
                .OrderBy(s => s)
                .ToArray();
            double totalSeconds = durations.Sum();
            double totalHours = Math.Round(totalSeconds / 3600.0, 1);
            int avgSongSeconds = durations.Length > 0 ? (int)Math.Round(totalSeconds / durations.Length) : 0;
            int medianSongSeconds = durations.Length > 0 ? (int)Math.Round(Median(durations)) : 0;
            long avgFileBytes = trackCount > 0 ? totalLibraryBytes / trackCount : 0;

            // ---- Age ----
            int currentYear = DateTime.Now.Year;
            int[] ages = audioTags
                .Where(t => int.TryParse(t.Year, out _))
                .Select(t => currentYear - int.Parse(t.Year))
                .OrderBy(a => a)
                .ToArray();
            bool hasAges = ages.Length > 0;

            // ---- Cover art (mirrors Analyser.PrintCoverArtStatistics) ----
            int missingData = audioTags.Count(t => string.IsNullOrEmpty(t.CoverWidth));
            int noCover = audioTags.Count(t => t.CoverWidth == "0");
            int unknownFormat = audioTags.Count(t => t.CoverWidth == "Unknown");
            int hasKnownDim = trackCount - missingData - noCover - unknownFormat;
            int subMin800 = audioTags.Count(t => KnownDim(t, out int w, out int h) && Math.Min(w, h) < 800);
            int nonSquare = audioTags.Count(t => KnownDim(t, out int w, out int h) && w != h);
            int covered800 = audioTags.Count(t => KnownDim(t, out int w, out int h) && Math.Min(w, h) >= 800);
            var coverHistogram = audioTags
                .Where(t => KnownDim(t, out _, out _))
                .GroupBy(t => $"{t.CoverWidth}x{t.CoverHeight}")
                .OrderByDescending(g => g.Count())
                .Take(15)
                .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
                .ToList();

            // ---- Tag completeness (all five core tags present) ----
            int complete = audioTags.Count(t =>
                IsPresent(t.Title) && IsPresent(t.Artists) && IsPresent(t.Album) &&
                IsPresent(t.Genres) && IsPresent(t.Year));

            // ---- Serialize ----
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"schemaVersion\": {SchemaVersion},");
            sb.AppendLine($"  \"generatedAt\": {JStr(DateTime.Now.ToString("s", CultureInfo.InvariantCulture))},");

            sb.AppendLine("  \"summary\": {");
            sb.AppendLine($"    \"trackCount\": {trackCount},");
            sb.AppendLine($"    \"artistCount\": {artistCount},");
            sb.AppendLine($"    \"genreCount\": {genreCount},");
            sb.AppendLine($"    \"totalLibraryBytes\": {totalLibraryBytes},");
            sb.AppendLine($"    \"avgFileBytes\": {avgFileBytes},");
            sb.AppendLine($"    \"totalPlaybackSeconds\": {Num(totalSeconds)},");
            sb.AppendLine($"    \"totalPlaybackHours\": {Num(totalHours)},");
            sb.AppendLine($"    \"avgSongLengthSeconds\": {avgSongSeconds},");
            sb.AppendLine($"    \"medianSongLengthSeconds\": {medianSongSeconds}");
            sb.AppendLine("  },");

            AppendLabelCountArray(sb, "genreDistribution", genreStats.SortedFreqDist, allEntries: true);
            AppendLabelCountArray(sb, "decadeDistribution", decadeDist, allEntries: true);
            AppendLabelCountArray(sb, "yearDistribution", yearStats.SortedFreqDist, allEntries: true);

            sb.AppendLine("  \"topArtists\": {");
            AppendLabelCountArray(sb, "exclMusivation", artistStatsExcl.SortedFreqDist, allEntries: false, indent: 4, trailingComma: true);
            AppendLabelCountArray(sb, "all", artistStatsAll.SortedFreqDist, allEntries: false, indent: 4, trailingComma: false);
            sb.AppendLine("  },");

            sb.AppendLine("  \"ageStats\": {");
            sb.AppendLine($"    \"averageYears\": {(hasAges ? Num(Math.Round(ages.Average(), 1)) : "null")},");
            sb.AppendLine($"    \"medianYears\": {(hasAges ? Num(Math.Round(Median(ages.Select(a => (double)a).ToArray()), 1)) : "null")},");
            sb.AppendLine($"    \"newestYears\": {(hasAges ? ages.First().ToString() : "null")},");
            sb.AppendLine($"    \"oldestYears\": {(hasAges ? ages.Last().ToString() : "null")}");
            sb.AppendLine("  },");

            // Age distribution buckets (aligns with the mockup's Track Age Distribution chart).
            var ageBuckets = new[]
            {
                new KeyValuePair<string, int>("0-2y",   ages.Count(a => a <= 2)),
                new KeyValuePair<string, int>("2-5y",   ages.Count(a => a > 2 && a <= 5)),
                new KeyValuePair<string, int>("5-10y",  ages.Count(a => a > 5 && a <= 10)),
                new KeyValuePair<string, int>("10-20y", ages.Count(a => a > 10 && a <= 20)),
                new KeyValuePair<string, int>("20y+",   ages.Count(a => a > 20)),
            };
            AppendLabelCountArray(sb, "ageDistribution", ageBuckets, allEntries: true);

            sb.AppendLine("  \"coverArt\": {");
            sb.AppendLine($"    \"total\": {trackCount},");
            sb.AppendLine($"    \"hasKnownDims\": {hasKnownDim},");
            sb.AppendLine($"    \"noCover\": {noCover},");
            sb.AppendLine($"    \"missingData\": {missingData},");
            sb.AppendLine($"    \"unknownFormat\": {unknownFormat},");
            sb.AppendLine($"    \"subMin800\": {subMin800},");
            sb.AppendLine($"    \"nonSquare\": {nonSquare},");
            AppendLabelCountArray(sb, "dimensionHistogram", coverHistogram, allEntries: true, indent: 4, trailingComma: false);
            sb.AppendLine("  },");

            sb.AppendLine("  \"tagCompleteness\": {");
            sb.AppendLine($"    \"complete\": {complete},");
            sb.AppendLine($"    \"total\": {trackCount},");
            sb.AppendLine($"    \"percent\": {Percent(complete, trackCount)}");
            sb.AppendLine("  },");

            sb.AppendLine("  \"coverCoverage800\": {");
            sb.AppendLine($"    \"covered\": {covered800},");
            sb.AppendLine($"    \"total\": {trackCount},");
            sb.AppendLine($"    \"percent\": {Percent(covered800, trackCount)}");
            sb.AppendLine("  }");

            sb.Append("}");
            return sb.ToString();
        }

        // ---- helpers ----

        /// <summary>Appends a "name": [ {"label":..,"count":..}, .. ] block. </summary>
        private static void AppendLabelCountArray(
            StringBuilder sb, string name, IEnumerable<KeyValuePair<string, int>> data,
            bool allEntries, int indent = 2, bool trailingComma = true)
        {
            var items = allEntries ? data.ToList() : data.Take(50).ToList();
            string pad = new string(' ', indent);
            string ipad = new string(' ', indent + 2);
            sb.AppendLine($"{pad}\"{name}\": [");
            for (int i = 0; i < items.Count; i++)
            {
                string comma = i < items.Count - 1 ? "," : "";
                sb.AppendLine($"{ipad}{{\"label\": {JStr(items[i].Key)}, \"count\": {items[i].Value}}}{comma}");
            }
            sb.AppendLine($"{pad}]{(trailingComma ? "," : "")}");
        }

        /// <summary>True if the cover width/height parse to real positive pixel dimensions.</summary>
        private static bool KnownDim(TrackTag t, out int w, out int h)
        {
            w = 0; h = 0;
            if (!int.TryParse(t.CoverWidth, out w) || !int.TryParse(t.CoverHeight, out h)) return false;
            return w > 0 && h > 0;
        }

        /// <summary>A tag value counts as present if it is non-blank and not the "Missing" sentinel.</summary>
        private static bool IsPresent(string s) =>
            !string.IsNullOrWhiteSpace(s) && !s.Equals("Missing", StringComparison.OrdinalIgnoreCase);

        /// <summary>Parses a .NET TimeSpan length string to seconds, or null if unparseable.</summary>
        private static double? TryParseSeconds(string length)
        {
            if (TimeSpan.TryParse(length, CultureInfo.InvariantCulture, out TimeSpan ts))
                return ts.TotalSeconds;
            return null;
        }

        /// <summary>Median of a pre-sorted array.</summary>
        private static double Median(double[] sorted)
        {
            int n = sorted.Length;
            if (n == 0) return 0;
            return n % 2 == 1
                ? sorted[n / 2]
                : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
        }

        private static string Percent(int part, int total) =>
            total > 0 ? Num(Math.Round(part * 100.0 / total, 1)) : "0";

        /// <summary>Walks the audio library once for a real byte total; 0 if it can't be read.</summary>
        private static long TryGetLibraryBytes()
        {
            try
            {
                return new DirectoryInfo(Constants.AudioFolderPath)
                    .GetFiles("*.mp3", SearchOption.AllDirectories)
                    .Sum(f => f.Length);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>Formats a double as a culture-invariant JSON number (always '.' decimal).</summary>
        private static string Num(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>JSON-escapes a string value into a quoted literal (or null).</summary>
        private static string JStr(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n") + "\"";
        }
    }
}
