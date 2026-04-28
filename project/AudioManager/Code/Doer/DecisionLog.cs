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
            this.sessionDate = DateTime.Now.ToString("yyyy-MM-dd");
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
        /// Save all logged decisions to an XML file in docs/Historical/WorkflowExecution-YYYY-MM-DD/.
        /// </summary>
        public void Save()
        {
            try
            {
                if (decisions.Count == 0)
                    return;

                // Determine output path: docs/Historical/WorkflowExecution-YYYY-MM-DD/decisions.xml
                string repoRoot = FindRepoRoot();
                string workflowDir = Path.Combine(repoRoot, "docs", "Historical", $"WorkflowExecution-{sessionDate}");
                Directory.CreateDirectory(workflowDir);

                string outputPath = Path.Combine(workflowDir, "decisions.xml");

                // Create root element
                var root = new XElement("decisions",
                    new XAttribute("date", sessionDate),
                    new XAttribute("count", decisions.Count),
                    decisions
                );

                // Write to file with nice formatting
                var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
                doc.Save(outputPath, SaveOptions.None);

                Console.WriteLine($"\nDecision log saved: docs\\Historical\\WorkflowExecution-{sessionDate}\\decisions.xml");
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
            return doc.Root.FirstNode.ToString().Substring(5); // Strip <temp> tags
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
