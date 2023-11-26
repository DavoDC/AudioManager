using System;
using System.Xml;

namespace AudioMirror.Code.Modules
{
    /// <summary>
    /// Represents a track stored using XML
    /// </summary>
    internal class TrackXML : Track
    {
        // Private fields
        private XmlDocument xmlDoc;
        private XmlElement rootElement;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrackXML"/> class.
        /// </summary>
        /// <param name="mirrorFilePath">The path to the mirror file.</param>
        /// <param name="tag">The audio metadata. Null if not given</param>
        public TrackXML(string mirrorFilePath, TrackTag tag = null)
        {
            try
            {
                // Initialize XML document
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
                    SetElementValue("Genre", tag.Genre);
                    SetElementValue("Length", tag.Length);

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
                    Genre = GetElementValue("Genre");
                    Length = GetElementValue("Length");
                }
            }
            catch (Exception ex)
            {
                // Create error message
                string errMsg = "Error occurred while ";
                errMsg += (tag != null) ? "creating NEW" : "loading EXISTING";
                errMsg += " XML file!";

                // Print info
                Console.WriteLine($"\n{errMsg}");
                Console.WriteLine($"\nMirror File: {mirrorFilePath}");
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine($"\nStack Trace: \n{ex.StackTrace}");
                Console.WriteLine("\n");
                Environment.Exit(1);
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