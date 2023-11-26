using TagLib;
using File = System.IO.File;

namespace AudioMirror.Code.Modules
{
    internal class TrackTag : Track
    {
        /// <summary>
        /// Represents the MP3 tag of a track
        /// </summary>
        /// <param name="mirrorFilePath">The mirror file path</param>
        public TrackTag(string mirrorFilePath)
        {
            // Get file contents of mirror file
            string[] fileContents = File.ReadAllLines(mirrorFilePath);

            // If mirror file has no valid path
            if (fileContents.Length != 1 || !File.Exists(fileContents[0]))
            {
                // Then the mirror file already has XML content
                // Load metadata from XML instead
                
                // Read in XML data
                TrackXML xmlFileIn = new TrackXML(mirrorFilePath);

                // Set tag properties using XML file data
                Title = xmlFileIn.Title;
                Artists = xmlFileIn.Artists;
                Album = xmlFileIn.Album;
                Year = xmlFileIn.Year;
                TrackNumber = xmlFileIn.TrackNumber;
                Genre = xmlFileIn.Genre;
                Length = xmlFileIn.Length;

                // Stop
                return;
            }

            // Otherwise, if mirror file only has valid path
            // Load metadata from file
            var audioMetadata = TagLib.File.Create(fileContents[0]);

            // Extract and save length
            Length = audioMetadata.Properties.Duration.ToString();

            // Extract and save tag info
            Tag tag = audioMetadata.Tag;
            Title = tag.Title;
            Artists = string.Join(", ", tag.Performers);
            Album = tag.Album;
            Year = tag.Year.ToString();
            TrackNumber = tag.Track.ToString();
            Genre = tag.FirstGenre;

            // Overwrite mirror file contents with XML data
            TrackXML xmlFileOut = new TrackXML(mirrorFilePath, this);
        }
    }
}
