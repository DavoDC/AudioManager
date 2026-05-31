using AudioManager.Code.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AudioManager
{
    /// <summary>
    /// Flat-file cache for parsed TrackTag data. Eliminates XML re-reads when the mirror is unchanged.
    /// Cache validity: compare cache file LastWriteTime against newest XML mtime in the mirror.
    /// If no XML is newer than the cache, the cached data is current and can be used directly.
    /// </summary>
    internal static class ParseCache
    {
        private const string Header = "PARSE_CACHE_V1";
        private const char Sep = '|';
        private const int FieldCount = 12;

        /// <summary>
        /// Try to load the parse cache. Returns false if cache is missing, stale, or corrupt.
        /// </summary>
        internal static bool TryLoad(string cachePath, string mirrorPath, out List<TrackTag> tags)
        {
            tags = null;
            if (!File.Exists(cachePath)) return false;
            DateTime cacheTime = new FileInfo(cachePath).LastWriteTime;
            if (AnyXmlNewerThan(mirrorPath, cacheTime)) return false;
            return TryDeserialize(cachePath, out tags);
        }

        /// <summary>
        /// Save the tag list to the cache file. Silently no-ops on I/O failure.
        /// </summary>
        internal static void Save(string cachePath, List<TrackTag> tags)
        {
            try
            {
                string dir = Path.GetDirectoryName(cachePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                using (var w = new StreamWriter(cachePath, append: false))
                {
                    w.WriteLine(Header);
                    foreach (var t in tags)
                    {
                        w.WriteLine(string.Join(Sep.ToString(),
                            t.RelPath, t.Title, t.Artists, t.Album,
                            t.Year, t.TrackNumber, t.Genres, t.Length,
                            t.AlbumCoverCount, t.Compilation, t.CoverWidth, t.CoverHeight));
                    }
                }
            }
            catch { /* cache write failures must not break the pipeline */ }
        }

        /// <summary>
        /// Deserialize a cache file without checking mtimes. Used by tests and TryLoad.
        /// Returns false if the file is missing, has the wrong header, or has malformed rows.
        /// </summary>
        internal static bool TryDeserialize(string cachePath, out List<TrackTag> tags)
        {
            tags = null;
            if (!File.Exists(cachePath)) return false;
            try
            {
                string[] lines = File.ReadAllLines(cachePath);
                if (lines.Length == 0 || lines[0] != Header) return false;

                var result = new List<TrackTag>(lines.Length - 1);
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrEmpty(lines[i])) continue;
                    string[] parts = lines[i].Split(Sep);
                    if (parts.Length != FieldCount) return false;
                    result.Add(new TrackTag(parts[0], parts[1], parts[2], parts[3],
                                            parts[4], parts[5], parts[6], parts[7],
                                            parts[8], parts[9], parts[10], parts[11]));
                }
                tags = result;
                return true;
            }
            catch
            {
                tags = null;
                return false;
            }
        }

        /// <summary>
        /// Returns true if any .xml file under mirrorPath has a LastWriteTime newer than threshold.
        /// Uses DirectoryInfo.GetFiles so mtime comes from the directory enumeration with no extra stats.
        /// </summary>
        internal static bool AnyXmlNewerThan(string mirrorPath, DateTime threshold)
        {
            return new DirectoryInfo(mirrorPath)
                .GetFiles("*.xml", SearchOption.AllDirectories)
                .Any(f => f.LastWriteTime > threshold);
        }
    }
}
