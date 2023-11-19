using TagLib;
using File = System.IO.File;

namespace AudioMirror.Code.Modules
{
    internal class TrackTag
    {
        // Properties
        public string Title { get; }
        public string Artists { get; }
        public string Album { get; }
        public string Year { get; }
        public string Track { get; }
        public string Genre { get; }
        public string Length { get; }

        /// <summary>
        /// Represents the MP3 tag of a track
        /// </summary>
        /// <param name="mirrorFilePath">The mirror file path</param>
        public TrackTag(string mirrorFilePath)
        {
            // Get file contents of mirror file
            string[] fileContents = File.ReadAllLines(mirrorFilePath);

            // If mirror file has no valid path, stop
            if (fileContents.Length != 1 || !File.Exists(fileContents[0]))
            {
                return;
            }

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
            Track = tag.Track.ToString();
            Genre = tag.FirstGenre;
        }
    }
}
