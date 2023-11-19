using System;
using System.IO;
using System.Xml;

namespace AudioMirror.Code.Modules
{
    internal class TrackXML
    {
        //// Properties
        public string Title { get; }
        public string Artists { get; }
        public string Album { get; }
        public string Year { get; }
        public string Track { get; }
        public string Genre { get; }
        public string Length { get; }

        //// Private variables
        private XmlDocument xmlDoc;
        private XmlElement rootElement;


        /// <summary>
        /// Construct an audio XML file from an EXISTING XML file
        /// </summary>
        /// <param name="mirrorFilePath"></param>
        public TrackXML(string mirrorFilePath)
        {
            try
            {
                // Initialize XML document
                xmlDoc = new XmlDocument();
                xmlDoc.Load(mirrorFilePath);

                // Get the root element
                rootElement = xmlDoc.DocumentElement;

                // Read data from XML and set properties
                Title = GetElementValue("Title");
                Artists = GetElementValue("Artists");
                Album = GetElementValue("Album");
                Year = GetElementValue("Year");
                Track = GetElementValue("Track");
                Genre = GetElementValue("Genre");
                Length = GetElementValue("Length");
            }
            catch (Exception ex)
            {
                HandleError("Error occurred while loading data from XML!", mirrorFilePath, ex);
            }
        }


        /// <summary>
        /// Construct an NEW audio XML file from a track tag
        /// </summary>
        /// <param name="mirrorFilePath">The mirror file path</param>
        /// <param name="tag">The audio metadata</param>
        public TrackXML(string mirrorFilePath, TrackTag tag)
        {
            try
            {
                // Initialize XML document
                xmlDoc = new XmlDocument();

                // Create root element and add
                rootElement = xmlDoc.CreateElement("Track");
                xmlDoc.AppendChild(rootElement);

                // Set XML elements to metadata values
                SetElement("Title", tag.Title);
                SetElement("Artists", tag.Artists);
                SetElement("Album", tag.Album);
                SetElement("Year", tag.Year);
                SetElement("Track", tag.Track);
                SetElement("Genre", tag.Genre);
                SetElement("Length", tag.Length);

                // Save file
                xmlDoc.Save(mirrorFilePath);
            }
            catch (Exception ex)
            {
                HandleError("Error occurred while loading data from XML!", mirrorFilePath, ex);
            }
        }


        /// <summary>
        /// Helper method to get the value of an XML element
        /// </summary>
        /// <param name="elementName"></param>
        /// <returns></returns>
        private string GetElementValue(string elementName)
        {
            var element = rootElement.SelectSingleNode(elementName) as XmlElement;
            return element?.InnerText ?? string.Empty;
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


        /// <summary>
        /// Helper method for error handling
        /// </summary>
        /// <param name="errorMessage"></param>
        /// <param name="filePath"></param>
        /// <param name="ex"></param>
        private void HandleError(string errorMessage, string filePath, Exception ex)
        {
            Console.WriteLine($"\n{errorMessage}");
            Console.WriteLine($"\nMirror File: {filePath}");
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine($"\nStack Trace: \n{ex.StackTrace}");
            Console.WriteLine("\n");
            Environment.Exit(1);
        }
    }
}