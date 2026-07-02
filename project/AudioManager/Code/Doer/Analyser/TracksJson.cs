using AudioManager.Code.Modules;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using TagList = System.Collections.Generic.List<AudioManager.Code.Modules.TrackTag>;

namespace AudioManager
{
    /// <summary>
    /// Emits the full per-track array as JSON for the GUI's Library Browser.
    ///
    /// Companion to StatsJson. Together they let the Python GUI stay 100% decoupled from the raw
    /// AudioMirror XML: StatsJson gives aggregate statistics, TracksJson gives the per-track rows.
    /// The GUI reads these two files and NEVER parses XML itself - so there is exactly one XML parser
    /// (the C# Parser) and zero risk of a second, drifting implementation in Python.
    ///
    /// Data contract: see docs/References/AnalysisJson-Format.md (Tracks section). Bump SchemaVersion
    /// on any breaking change.
    /// </summary>
    internal static class TracksJson
    {
        /// <summary>Contract version. Increment when a field is removed or its meaning changes.</summary>
        public const int SchemaVersion = 1;

        /// <summary>Fixed output filename (latest run wins) so the GUI reads a deterministic path.</summary>
        public const string OutputFileName = "tracks.json";

        /// <summary>
        /// Production entry point: builds the per-track JSON and writes it to logs/tracks.json.
        /// Returns the path written.
        /// </summary>
        public static string Write(TagList audioTags)
        {
            string json = Build(audioTags);
            if (!Directory.Exists(Constants.LogsPath))
                Directory.CreateDirectory(Constants.LogsPath);
            string outPath = Path.Combine(Constants.LogsPath, OutputFileName);
            File.WriteAllText(outPath, json, Encoding.UTF8);
            return outPath;
        }

        /// <summary>
        /// Builds the per-track JSON document. Pure apart from best-effort file-mtime lookups for
        /// "addedDate" (guarded - null when the mirror file isn't on disk, e.g. in unit tests).
        /// </summary>
        public static string Build(TagList audioTags)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"schemaVersion\": {SchemaVersion},");
            sb.AppendLine($"  \"generatedAt\": {JStr(DateTime.Now.ToString("s", CultureInfo.InvariantCulture))},");
            sb.AppendLine($"  \"trackCount\": {audioTags.Count},");
            sb.AppendLine("  \"tracks\": [");

            for (int i = 0; i < audioTags.Count; i++)
            {
                var t = audioTags[i];
                string comma = i < audioTags.Count - 1 ? "," : "";

                double? seconds = TryParseSeconds(t.Length);
                bool hasArt = KnownDim(t, out int w, out int h);
                bool hiRes = hasArt && Math.Min(w, h) >= 800;
                string mp3Path = ReconstructMp3Path(t.RelPath);
                string addedDate = TryGetAddedDate(t.RelPath);
                string decade = Decade(t.Year);

                sb.AppendLine("    {");
                sb.AppendLine($"      \"id\": {JStr(t.RelPath)},");
                sb.AppendLine($"      \"title\": {JStr(t.Title)},");
                sb.AppendLine($"      \"artists\": {JStr(t.Artists)},");
                sb.AppendLine($"      \"primaryArtist\": {JStr(t.PrimaryArtist)},");
                sb.AppendLine($"      \"album\": {JStr(t.Album)},");
                sb.AppendLine($"      \"year\": {JStr(t.Year)},");
                sb.AppendLine($"      \"decade\": {(decade == null ? "null" : JStr(decade))},");
                sb.AppendLine($"      \"genres\": {JStr(t.Genres)},");
                sb.AppendLine($"      \"primaryGenre\": {JStr(PrimaryGenre(t.Genres))},");
                sb.AppendLine($"      \"trackNumber\": {JStr(t.TrackNumber)},");
                sb.AppendLine($"      \"lengthSeconds\": {(seconds.HasValue ? Num(seconds.Value) : "null")},");
                sb.AppendLine($"      \"length\": {JStr(FormatLength(seconds))},");
                sb.AppendLine($"      \"compilation\": {(IsTrue(t.Compilation) ? "true" : "false")},");
                sb.AppendLine($"      \"coverWidth\": {(hasArt ? w.ToString() : "null")},");
                sb.AppendLine($"      \"coverHeight\": {(hasArt ? h.ToString() : "null")},");
                sb.AppendLine($"      \"hasArt\": {(hasArt ? "true" : "false")},");
                sb.AppendLine($"      \"hiResArt\": {(hiRes ? "true" : "false")},");
                sb.AppendLine($"      \"addedDate\": {(addedDate == null ? "null" : JStr(addedDate))},");
                sb.AppendLine($"      \"filePath\": {JStr(mp3Path)}");
                sb.AppendLine($"    }}{comma}");
            }

            sb.AppendLine("  ]");
            sb.Append("}");
            return sb.ToString();
        }

        // ---- helpers ----

        /// <summary>
        /// Reconstructs the real MP3 path from a mirror-relative RelPath. The AudioMirror mirrors the
        /// Audio library folder 1:1, so the MP3 lives at AudioFolderPath + RelPath with .xml -> .mp3.
        /// (For the rare track whose filename was sanitised into a different XML name, this path won't
        /// resolve on disk; the GUI treats that as "no extractable art" and shows a placeholder.)
        /// </summary>
        private static string ReconstructMp3Path(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return "";
            string trimmed = relPath.TrimStart('\\', '/');
            string full = Path.Combine(Constants.AudioFolderPath, trimmed);
            return Path.ChangeExtension(full, ".mp3");
        }

        /// <summary>Best-effort "date added" = last-write time of the mirror XML file (ISO date), or null.</summary>
        private static string TryGetAddedDate(string relPath)
        {
            try
            {
                if (string.IsNullOrEmpty(relPath)) return null;
                string xmlPath = Path.Combine(Constants.MirrorFolderPath, relPath.TrimStart('\\', '/'));
                if (!File.Exists(xmlPath)) return null;
                return File.GetLastWriteTime(xmlPath).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Decade label from a year string ("2020s"), or null if the year isn't parseable.</summary>
        private static string Decade(string year) =>
            int.TryParse(year, out int y) ? $"{(y / 10) * 10}s" : null;

        /// <summary>First genre of a "; "-separated Genres field.</summary>
        private static string PrimaryGenre(string genres)
        {
            if (string.IsNullOrEmpty(genres)) return genres;
            return Track.ProcessProperty(genres)[0];
        }

        private static bool IsTrue(string s) =>
            !string.IsNullOrEmpty(s) && s.Equals("True", StringComparison.OrdinalIgnoreCase);

        private static bool KnownDim(TrackTag t, out int w, out int h)
        {
            w = 0; h = 0;
            if (!int.TryParse(t.CoverWidth, out w) || !int.TryParse(t.CoverHeight, out h)) return false;
            return w > 0 && h > 0;
        }

        private static double? TryParseSeconds(string length)
        {
            if (TimeSpan.TryParse(length, CultureInfo.InvariantCulture, out TimeSpan ts))
                return ts.TotalSeconds;
            return null;
        }

        /// <summary>Formats a length in seconds as m:ss (empty string if unknown).</summary>
        private static string FormatLength(double? seconds)
        {
            if (!seconds.HasValue) return "";
            var ts = TimeSpan.FromSeconds(seconds.Value);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
        }

        private static string Num(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);

        private static string JStr(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n") + "\"";
        }
    }
}
