using System;
using System.IO;
using System.Xml;

namespace AudioMirror.Code.Modules
{
    internal class TrackXML
    {
        //// VARIABLES
        private XmlDocument xmlDoc;
        private XmlElement rootElement;

        /// <summary>
        /// Construct an XML file for an audio track
        /// </summary>
        public TrackXML(string mirrorFilePath, string realFilePath)
        {
            try
            {
                // If no real file path found
                if (realFilePath == null)
                {
                    // Stop, XML file is likely already done
                    return;
                }

                // Initialize XML document
                xmlDoc = new XmlDocument();

                // Create root element and add
                rootElement = xmlDoc.CreateElement("Track");
                xmlDoc.AppendChild(rootElement);

                // Load audio file metadata
                var audioFile = TagLib.File.Create(realFilePath);
                var audioTag = audioFile.Tag;

                // Set XML elements to metadata values
                SetElement("Title", audioTag.Title);
                SetElement("Artists", string.Join(", ", audioTag.Performers));
                SetElement("Album", audioTag.Album);
                SetElement("Year", audioTag.Year.ToString());
                SetElement("Track", audioTag.Track.ToString());
                SetElement("Genre", audioTag.FirstGenre);
                SetElement("Length", audioFile.Properties.Duration.ToString());

                // Save file
                xmlDoc.Save(mirrorFilePath);

                // Dispose audio metadata
                audioFile.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError occurred while parsing!");
                Console.WriteLine($"\nAudio File: {realFilePath}");
                Console.WriteLine($"\nMirror File: {mirrorFilePath}");
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine($"\nStack Trace: \n{ex.StackTrace}");
                Console.WriteLine("\n");
                Environment.Exit(1);
            }
        }


        /// <summary>
        /// Adds or updates an XML element with the specified name and value.
        /// </summary>
        /// <param name="elementName">The name of the XML element.</param>
        /// <param name="elementValue">The value to set for the XML element.</param>
        private void SetElement(string elementName, string elementValue)
        {
            // Try to retrieve element
            var existingElement = rootElement.SelectSingleNode(elementName) as XmlElement;

            // If element exists
            if (existingElement != null)
            {
                // Update existing element and stop
                existingElement.InnerText = elementValue;
                return;
            }

            // Otherwise, create a new XML element with the specified name
            var newElement = xmlDoc.CreateElement(elementName);

            // Set the inner text (value) of the XML element
            newElement.InnerText = elementValue;

            // Append the new XML element to the root element
            rootElement.AppendChild(newElement);
        }
    }
}