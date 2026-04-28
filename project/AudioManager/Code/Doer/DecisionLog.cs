using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using AudioManager.Code.Modules;

namespace AudioManager
{
    /// <summary>
    /// Logs routing decisions during integration to an XML file for audit and pattern analysis.
    /// Each decision captures: artist, album, track, source, destination, reason, and whether it's a dry-run.
    /// </summary>
    internal class DecisionLog
    {
        private List<XElement> decisions = new List<XElement>();
        private bool dryRun;
        private string sessionDate;

        public DecisionLog(bool dryRun)
        {
            this.dryRun = dryRun;
            this.sessionDate = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
        }

        /// <summary>
        /// Log a routing decision for a single track using Track metadata.
        /// </summary>
        public void LogDecision(Track track, string sourceFile, string destinationPath, string routingReason)
        {
            var trackMetadata = new XElement("trackMetadata",
                new XElement("artist", XmlEncode(track.Artists ?? "")),
                new XElement("primaryArtist", XmlEncode(track.PrimaryArtist ?? "")),
                new XElement("title", XmlEncode(track.Title ?? "")),
                new XElement("album", XmlEncode(track.Album ?? "")),
                new XElement("year", XmlEncode(track.Year ?? "")),
                new XElement("genres", XmlEncode(track.Genres ?? "")),
                new XElement("compilation", XmlEncode(track.Compilation ?? ""))
            );

            var decision = new XElement("decision",
                new XElement("dryRun", dryRun.ToString().ToLower()),
                new XElement("timestamp", DateTime.Now.ToString("O")),
                trackMetadata,
                new XElement("sourceFile", XmlEncode(sourceFile ?? "")),
                new XElement("destinationPath", XmlEncode(destinationPath ?? "")),
                new XElement("routingReason", XmlEncode(routingReason ?? ""))
            );
            decisions.Add(decision);
        }

        /// <summary>
        /// Save all logged decisions to an XML file in logs/decisions-YYYY-MM-DD.xml.
        /// </summary>
        public void Save()
        {
            try
            {
                if (decisions.Count == 0)
                    return;

                // Determine output path: logs/decisions-YYYY-MM-DD.xml
                string repoRoot = FindRepoRoot();
                string logsDir = Path.Combine(repoRoot, "logs");
                Directory.CreateDirectory(logsDir);

                string outputPath = Path.Combine(logsDir, $"decisions-{sessionDate}.xml");

                // Create root element
                var root = new XElement("decisions",
                    new XAttribute("date", sessionDate),
                    new XAttribute("count", decisions.Count),
                    decisions
                );

                // Write to file with nice formatting
                var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
                doc.Save(outputPath, SaveOptions.None);

                Console.WriteLine($"\nDecision log saved: logs\\decisions-{sessionDate}.xml");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [WARN] Could not save decision log: {ex.Message}");
            }
        }

        /// <summary>
        /// XML-encode special characters in text content.
        /// </summary>
        private string XmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            var doc = new XDocument(new XElement("temp", text));
            string encoded = doc.Root.FirstNode.ToString();
            // Strip <temp> and </temp> tags (6 + 7 = 13 chars minimum)
            if (encoded.Length >= 13 && encoded.StartsWith("<temp>") && encoded.EndsWith("</temp>"))
            {
                return encoded.Substring(6, encoded.Length - 13);
            }
            return encoded; // fallback if format is unexpected
        }

        /// <summary>
        /// Find repo root by walking up from executable directory looking for sentinel files (CLAUDE.md, README.md, or .git).
        /// </summary>
        private string FindRepoRoot()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(Path.Combine(current, "CLAUDE.md")) ||
                    File.Exists(Path.Combine(current, "README.md")) ||
                    Directory.Exists(Path.Combine(current, ".git")))
                {
                    return current;
                }
                current = Path.GetDirectoryName(current);
            }
            throw new InvalidOperationException("Could not find repo root. CLAUDE.md, README.md, or .git not found in any parent directory.");
        }
    }
}
