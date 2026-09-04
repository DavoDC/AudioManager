using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AudioManager
{
    /// <summary>
    /// Manifest for selective integration (integrate --manifest &lt;accepted.json&gt;).
    /// The GUI's review queue writes a JSON array of accepted tracks:
    ///   [ {"filename": "...", "artist": "...", "title": "..."}, ... ]
    /// and the integrator moves ONLY matching NewMusic files; everything else is
    /// left untouched in NewMusic (no moves, no deletes).
    ///
    /// Matching is by filename OR by the canonical TagFixer rename
    /// "{SanitiseFilename(artist)} - {SanitiseFilename(title)}.mp3". The second
    /// candidate is what makes manifests survive TagFixer: dry-run JSON records
    /// the pre-rename on-disk name, but a real run renames files before the
    /// integrator lists them - the canonical name matches either way.
    /// </summary>
    internal class IntegrationManifest
    {
        internal class Entry
        {
            public string Filename;
            public string Artist;
            public string Title;
            // GUI review-stage duplicate resolution ("D"/"L"/"K"), if the user changed it away
            // from the exe's own recommendation. Null/absent means "unresolved" - the exe falls
            // back to its recommendation exactly as it always has. See "Duplicate-resolution UI"
            // in docs/Development/IDEAS.md.
            public string DupResolution;
        }

        internal List<Entry> Entries = new List<Entry>();

        private HashSet<string> candidateNames;
        private readonly Dictionary<string, Entry> candidateToEntry =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<Entry> matchedEntries = new HashSet<Entry>();

        /// <summary>
        /// Loads and validates a manifest file. Returns null (with error set) on
        /// unreadable/invalid/empty input - callers must treat that as a hard
        /// failure, never as "integrate everything".
        /// </summary>
        internal static IntegrationManifest Load(string path, out string error)
        {
            error = null;
            string text;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                error = $"could not read manifest '{path}': {ex.Message}";
                return null;
            }

            var manifest = Parse(text, out error);
            if (manifest == null) return null;
            if (manifest.Entries.Count == 0)
            {
                error = "manifest contains no entries - refusing to integrate nothing";
                return null;
            }
            return manifest;
        }

        /// <summary>Parses the JSON text (array of flat string-valued objects).</summary>
        internal static IntegrationManifest Parse(string text, out string error)
        {
            error = null;
            var manifest = new IntegrationManifest();
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "manifest is empty";
                return null;
            }

            int i = 0;
            SkipWhitespace(text, ref i);
            if (i >= text.Length || text[i] != '[')
            {
                error = "manifest must be a JSON array";
                return null;
            }
            i++; // past [

            while (true)
            {
                SkipWhitespace(text, ref i);
                if (i >= text.Length) { error = "unterminated manifest array"; return null; }
                if (text[i] == ']') break;
                if (text[i] == ',') { i++; continue; }
                if (text[i] != '{') { error = $"unexpected character '{text[i]}' at position {i}"; return null; }

                var fields = ParseFlatObject(text, ref i, out error);
                if (fields == null) return null;

                var entry = new Entry();
                fields.TryGetValue("filename", out entry.Filename);
                fields.TryGetValue("artist", out entry.Artist);
                fields.TryGetValue("title", out entry.Title);
                fields.TryGetValue("dupResolution", out entry.DupResolution);
                if (string.IsNullOrWhiteSpace(entry.Filename) &&
                    (string.IsNullOrWhiteSpace(entry.Artist) || string.IsNullOrWhiteSpace(entry.Title)))
                {
                    error = "manifest entry needs a filename, or an artist and title";
                    return null;
                }
                manifest.Entries.Add(entry);
            }

            manifest.BuildCandidates();
            return manifest;
        }

        /// <summary>True if a NewMusic filename is accepted by this manifest.</summary>
        internal bool Matches(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return false;
            if (!candidateNames.Contains(filename)) return false;
            if (candidateToEntry.TryGetValue(filename, out var entry))
                matchedEntries.Add(entry);
            return true;
        }

        /// <summary>Entries that never matched any scanned file (stale scan warning).</summary>
        internal List<Entry> UnmatchedEntries() =>
            Entries.Where(e => !matchedEntries.Contains(e)).ToList();

        /// <summary>
        /// Looks up a GUI-resolved duplicate decision ('D'/'L'/'K') for a scanned file, tried by
        /// exact filename first (same candidate set Matches() uses), then falling back to the
        /// canonical TagFixer-rename form built from artist+title (so it survives a real-run
        /// rename same as filename matching already does). Returns '\0' when unresolved or when
        /// the value isn't one of D/L/K - callers must treat that as "no override", never as an
        /// implicit decision.
        /// </summary>
        internal char GetDupResolution(string filename, string artist, string title)
        {
            Entry entry = null;
            if (!string.IsNullOrEmpty(filename))
                candidateToEntry.TryGetValue(filename, out entry);

            if (entry == null && !string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(title))
            {
                string sanArtist = Reflector.SanitiseFilename(artist);
                string sanTitle = Reflector.SanitiseFilename(title);
                if (!string.IsNullOrEmpty(sanArtist) && !string.IsNullOrEmpty(sanTitle))
                    candidateToEntry.TryGetValue($"{sanArtist} - {sanTitle}.mp3", out entry);
            }

            if (entry == null || string.IsNullOrEmpty(entry.DupResolution)) return '\0';
            char c = char.ToUpperInvariant(entry.DupResolution[0]);
            return (c == 'D' || c == 'L' || c == 'K') ? c : '\0';
        }

        private void BuildCandidates()
        {
            candidateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in Entries)
            {
                if (!string.IsNullOrWhiteSpace(e.Filename))
                {
                    candidateNames.Add(e.Filename);
                    if (!candidateToEntry.ContainsKey(e.Filename))
                        candidateToEntry[e.Filename] = e;
                }
                if (!string.IsNullOrWhiteSpace(e.Artist) && !string.IsNullOrWhiteSpace(e.Title))
                {
                    // Same canonical name TagFixer renames to (see TagFixer rename block)
                    string sanArtist = Reflector.SanitiseFilename(e.Artist);
                    string sanTitle = Reflector.SanitiseFilename(e.Title);
                    if (!string.IsNullOrEmpty(sanArtist) && !string.IsNullOrEmpty(sanTitle))
                    {
                        string canonical = $"{sanArtist} - {sanTitle}.mp3";
                        candidateNames.Add(canonical);
                        if (!candidateToEntry.ContainsKey(canonical))
                            candidateToEntry[canonical] = e;
                    }
                }
            }
        }

        // ------------------------------------------------ minimal JSON pieces

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        /// <summary>Parses {"key": "value", ...} (string values only) starting at '{'.</summary>
        private static Dictionary<string, string> ParseFlatObject(string s, ref int i, out string error)
        {
            error = null;
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            i++; // past {
            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length) { error = "unterminated manifest object"; return null; }
                if (s[i] == '}') { i++; return result; }
                if (s[i] == ',') { i++; continue; }
                if (s[i] != '"') { error = $"expected key string at position {i}"; return null; }

                string key = ParseString(s, ref i, out error);
                if (key == null) return null;
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') { error = $"expected ':' at position {i}"; return null; }
                i++;
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == '"')
                {
                    string value = ParseString(s, ref i, out error);
                    if (value == null) return null;
                    result[key] = value;
                }
                else
                {
                    // tolerate non-string values (null/true/numbers) by skipping the token
                    while (i < s.Length && s[i] != ',' && s[i] != '}') i++;
                }
            }
        }

        /// <summary>Parses a JSON string starting at '"', handling standard escapes.</summary>
        private static string ParseString(string s, ref int i, out string error)
        {
            error = null;
            var sb = new StringBuilder();
            i++; // past opening quote
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '"') { i++; return sb.ToString(); }
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[i + 1];
                    switch (n)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (i + 5 < s.Length &&
                                int.TryParse(s.Substring(i + 2, 4),
                                    System.Globalization.NumberStyles.HexNumber, null, out int code))
                            {
                                sb.Append((char)code);
                                i += 4;
                            }
                            break;
                        default: sb.Append(n); break;
                    }
                    i += 2;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            error = "unterminated string in manifest";
            return null;
        }
    }
}
