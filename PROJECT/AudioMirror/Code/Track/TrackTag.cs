using System;
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
            // Always initialise relative path
            int relStartPos = mirrorFilePath.LastIndexOf(Program.MirrorFolder);
            RelPath = mirrorFilePath.Remove(0, relStartPos + Program.MirrorFolder.Length);

            // Get file contents of mirror file (should be a file path)
            string[] fileContents = File.ReadAllLines(mirrorFilePath);

            // If mirror file does not contain a single, valid file path
            if (fileContents.Length != 1 || !File.Exists(fileContents[0]))
            {
                // Then the mirror file already has XML content
                // So load metadata from XML instead
                
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
                AlbumCoverCount = xmlFileIn.AlbumCoverCount;

                // Stop
                return;
            }

            // # Otherwise, if mirror file contains a valid path, initialise fields

            // Load metadata from file
            var audioMetadata = TagLib.File.Create(fileContents[0]);

            // Extract info from tag
            Tag tag = audioMetadata.Tag;
            Title = string.IsNullOrEmpty(tag.Title) ? "Missing" : tag.Title;
            Artists = string.IsNullOrEmpty(tag.JoinedPerformers) ? "Missing" : tag.JoinedPerformers;
            Album = string.IsNullOrEmpty(tag.Album) ? "Missing" : tag.Album;
            Year = (tag.Year == 0) ? "Missing" : tag.Year.ToString();
            TrackNumber = tag.Track.ToString();
            Genres = string.IsNullOrEmpty(tag.JoinedGenres) ? "Missing" : tag.JoinedGenres;

            // Extract info from properties
            Length = audioMetadata.Properties.Duration.ToString();
            AlbumCoverCount = (audioMetadata.Tag.Pictures?.Length ?? 0).ToString();

            // Overwrite mirror file contents with metadata
            TrackXML xmlFileOut = new TrackXML(mirrorFilePath, this);
        }
    }
}
