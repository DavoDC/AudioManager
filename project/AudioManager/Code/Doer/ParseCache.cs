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
        private static readonly int FieldCount = Extract(null).Length;

        // Returns fields in cache serialization order. Extract(null) returns an empty 12-element
        // array whose length is the authoritative field count - one edit here updates Save + validation.
        internal static string[] Extract(TrackTag t) => t == null
            ? new string[12]
            : new[] { t.RelPath, t.Title, t.Artists, t.Album,
                      t.Year, t.TrackNumber, t.Genres, t.Length,
                      t.AlbumCoverCount, t.Compilation, t.CoverWidth, t.CoverHeight };

        /// <summary>
        /// Try to load the parse cache. Returns false if cache is missing, stale, or corrupt.
        /// Any filesystem error during the mtime check degrades to a miss (never crashes).
        /// </summary>
        internal static bool TryLoad(string cachePath, string mirrorPath, out List<TrackTag> tags)
        {
            tags = null;
            if (!File.Exists(cachePath)) return false;
            try
            {
                DateTime cacheTime = new FileInfo(cachePath).LastWriteTime;
                // IsMirrorStale: miss if mirror is empty (Reflector crash mid-regen) or any XML is newer
                if (IsMirrorStale(mirrorPath, cacheTime)) return false;
            }
            catch { return false; } // mirror path error -> degrade to miss
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
                        w.WriteLine(string.Join(Sep.ToString(), Extract(t)));
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
        /// Returns true if the mirror is stale relative to the cache timestamp.
        /// Stale means: (a) no XMLs found - mirror is empty or mid-regen after a crash, or
        /// (b) at least one XML has a LastWriteTime newer than the cache.
        /// Uses DirectoryInfo.GetFiles so mtime comes from the directory enumeration, no extra stats.
        /// Exposed internal for tests.
        /// </summary>
        internal static bool IsMirrorStale(string mirrorPath, DateTime cacheTime)
        {
            var xmlFiles = new DirectoryInfo(mirrorPath)
                .GetFiles("*.xml", SearchOption.AllDirectories);
            // Empty mirror = Reflector crash or missing - treat as stale so Parser re-populates
            if (!xmlFiles.Any()) return true;
            return xmlFiles.Any(f => f.LastWriteTime > cacheTime);
        }

    }
}
