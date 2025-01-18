using System;
using System.Xml;

namespace AudioMirror.Code.Modules
{
    /// <summary>
    /// An audio track's metadata stored as an XML file
    /// </summary>
    internal class TrackXML : Track
    {
        // Private fields
        private XmlDocument xmlDoc;
        private XmlElement rootElement;

        /// <summary>
        /// Initialises a new instance of the <see cref="TrackXML"/> class.
        /// </summary>
        /// <param name="mirrorFilePath">The path to the mirror file.</param>
        /// <param name="tag">The audio metadata. Null if not given</param>
        public TrackXML(string mirrorFilePath, TrackTag tag = null)
        {
            try
            {
                // Initialise XML document
                xmlDoc = new XmlDocument();
                rootElement = xmlDoc.CreateElement("Track");
                xmlDoc.AppendChild(rootElement);

                // If tag provided
                if (tag != null)
                {
                    // CREATE AN XML FILE
                    // Set XML elements to metadata values
                    SetElementValue("Title", tag.Title);
                    SetElementValue("Artists", tag.Artists);
                    SetElementValue("Album", tag.Album);
                    SetElementValue("Year", tag.Year);
                    SetElementValue("TrackNumber", tag.TrackNumber);
                    SetElementValue("Genres", tag.Genres);
                    SetElementValue("Length", tag.Length);
                    SetElementValue("AlbumCoverCount", tag.AlbumCoverCount);
                    SetElementValue("Compilation", tag.Compilation);

                    // Save file
                    xmlDoc.Save(mirrorFilePath);
                }
                else
                {
                    // If no tag, LOAD EXISTING XML FILE
                    xmlDoc.Load(mirrorFilePath);
                    rootElement = xmlDoc.DocumentElement;

                    // Read data from XML and set properties
                    Title = GetElementValue("Title");
                    Artists = GetElementValue("Artists");
                    Album = GetElementValue("Album");
                    Year = GetElementValue("Year");
                    TrackNumber = GetElementValue("TrackNumber");
                    Genres = GetElementValue("Genres");
                    Length = GetElementValue("Length");
                    AlbumCoverCount = GetElementValue("AlbumCoverCount");
                    Compilation = GetElementValue("Compilation");
                }
            }
            catch (Exception)
            {
                // Create error message and throw new exception with it
                string errMsg = "Error occurred while ";
                errMsg += $"{(tag != null ? "creating NEW" : "loading EXISTING")} XML file!";
                errMsg += $"\n'mirrorFilePath' was: {mirrorFilePath}";
                throw new XmlException(errMsg);
            }
        }


        /// <summary>
        /// Sets the value of the specified XML element.
        /// </summary>
        /// <param name="elementName">The name of the XML element.</param>
        /// <param name="elementValue">The value to set for the XML element.</param>
        private void SetElementValue(string elementName, string elementValue)
        {
            var existingElement = GetXmlElement(elementName);

            // If element exists, set its value
            if (existingElement != null)
            {
                existingElement.InnerText = elementValue;
                return;
            }

            // If element does not exist, create a new one
            var newElement = xmlDoc.CreateElement(elementName);
            newElement.InnerText = elementValue;
            rootElement.AppendChild(newElement);
        }


        /// <summary>
        /// Gets the value of the specified XML element.
        /// </summary>
        /// <param name="elementName">The name of the XML element.</param>
        /// <returns>The value of the XML element, or an empty string if the element is not found.</returns>
        private string GetElementValue(string elementName)
        {
            var existingElement = GetXmlElement(elementName);
            return existingElement?.InnerText ?? string.Empty;
        }


        /// <summary>
        /// Gets the specified XML element from the root element.
        /// </summary>
        /// <param name="elementName">The name of the XML element.</param>
        /// <returns>The XML element, or null if not found.</returns>
        private XmlElement GetXmlElement(string elementName)
        {
            return rootElement.SelectSingleNode(elementName) as XmlElement;
        }
    }
}