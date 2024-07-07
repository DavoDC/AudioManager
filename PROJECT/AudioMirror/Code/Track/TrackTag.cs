using TagLib;
using File = System.IO.File;

namespace AudioMirror.Code.Modules
{
    /// <summary>
    /// An audio track as an MP3 tag
    /// </summary>
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
                Genres = xmlFileIn.Genres;
                Length = xmlFileIn.Length;

                // Stop
                return;
            }

            // Otherwise, if mirror file only has valid path
            // Load metadata from file
            var audioMetadata = TagLib.File.Create(fileContents[0]);

            // Extract and save length
            Length = audioMetadata.Properties.Duration.ToString();

            // Get tag
            Tag tag = audioMetadata.Tag;

            // Extract info
            Title = tag.Title;
            Artists = tag.JoinedPerformers;
            Album = tag.Album;
            Year = (tag.Year == 0) ? "Missing" : (Year = tag.Year.ToString());
            TrackNumber = tag.Track.ToString();
            Genres = string.IsNullOrEmpty(tag.JoinedGenres) ? "Missing" : tag.JoinedGenres;

            // Overwrite mirror file contents with metadata
            TrackXML xmlFileOut = new TrackXML(mirrorFilePath, this);
        }
    }
}
