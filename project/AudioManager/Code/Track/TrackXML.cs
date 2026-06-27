using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace AudioManager.Code.Modules
{
    internal static class TrackXML
    {
        internal static void Read(string path, TrackTag t)
        {
            try
            {
                var root = XDocument.Load(path).Root;
                var cover        = root.Element("AlbumCover");
                t.Title          = root.Element("Title").Value;
                t.Artists        = root.Element("Artists").Value;
                t.Album          = root.Element("Album").Value;
                t.Year           = root.Element("Year").Value;
                t.TrackNumber    = root.Element("TrackNumber").Value;
                t.Genres         = root.Element("Genres").Value;
                t.Length         = root.Element("Length").Value;
                t.AlbumCoverCount = cover.Element("Count").Value;
                t.CoverWidth     = cover.Element("Width").Value;
                t.CoverHeight    = cover.Element("Height").Value;
                t.Compilation    = root.Element("Compilation").Value;
            }
            catch (Exception ex)
            {
                throw new XmlException($"Error reading XML: {path}", ex);
            }
        }

        internal static void Write(string path, TrackTag t)
        {
            string tmpPath = path + ".tmp";
            try
            {
                var doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    new XElement("Track",
                        new XElement("Title",       t.Title),
                        new XElement("Artists",     t.Artists),
                        new XElement("Album",       t.Album),
                        new XElement("Year",        t.Year),
                        new XElement("TrackNumber", t.TrackNumber),
                        new XElement("Genres",      t.Genres),
                        new XElement("Length",      t.Length),
                        new XElement("AlbumCover",
                            new XElement("Count",  t.AlbumCoverCount),
                            new XElement("Width",  t.CoverWidth),
                            new XElement("Height", t.CoverHeight)),
                        new XElement("Compilation", t.Compilation)));

                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    NewLineChars = "\n",
                    NewLineHandling = NewLineHandling.Replace,
                    Encoding = new System.Text.UTF8Encoding(true)
                };
                using (var writer = XmlWriter.Create(tmpPath, settings))
                    doc.Save(writer);

                // Atomic promotion: replace existing file or move into place for first write
                if (File.Exists(path))
                    File.Replace(tmpPath, path, null);
                else
                    File.Move(tmpPath, path);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
                throw new XmlException($"Error writing XML: {path}", ex);
            }
        }
    }
}
