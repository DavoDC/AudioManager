using System;

namespace AudioManager.Code.Modules
{
    /// <summary>
    /// An audio track's metadata
    /// </summary>
    internal class Track
    {
        // The track's relative path within the music library folder
        private string relPath;
        public string RelPath
        {
            get => relPath;
            set => relPath = value;
        }

        // The track's title
        private string title;
        public string Title
        {
            get => title;
            set => title = value;
        }

        // The track's artists (concatenated)
        private string artists;
        public string Artists
        {
            get => artists;
            set => artists = value;
        }

        // The track's primary artist
        public string PrimaryArtist
        { 
            get => StatList.ProcessProperty(Artists)[0];
        }

        // The track's album
        private string album;
        public string Album
        { 
            get => album;
            set => album = value;
        }

        // The track's year
        private string year;
        public string Year
        { 
            get => year;
            set => year = value;
        }

        // The track's number (disc order)
        private string trackNumber;
        public string TrackNumber
        { 
            get => trackNumber;
            set => trackNumber = value;
        }

        // The track's genres (concatenated)
        private string genres;
        public string Genres
        {
            get => genres;
            set => genres = value;
        }

        // The track's duration
        private string length;
        public string Length
        { 
            get => length;
            set => length = value;
        }

        // The track's album cover count
        private string albumCoverCount;
        public string AlbumCoverCount
        {
            get => albumCoverCount;
            set => albumCoverCount = value;
        }

        // The track's compilation status (i.e. whether its album is a compilation of songs by various artists)
        private string compilation;
        public string Compilation
        {
            get => compilation;
            set => compilation = value;
        }

        /// <returns>A concise string representation of this track</returns>
        public override string ToString()
        {
            return $"{Artists} - {Title}";
        }

        /// <returns>A string representation of all track properties.</returns>
        public string ToAllPropertiesString()
        {
            return $"RelPath: {RelPath ?? "NULL"}\n" +
                   $"Title: {Title ?? "NULL"}\n" +
                   $"Artists: {Artists ?? "NULL"}\n" +
                   $"PrimaryArtist: {PrimaryArtist ?? "NULL"}\n" +
                   $"Album: {Album ?? "NULL"}\n" +
                   $"Year: {Year ?? "NULL"}\n" +
                   $"TrackNumber: {TrackNumber ?? "NULL"}\n" +
                   $"Genres: {Genres ?? "NULL"}\n" +
                   $"Length: {Length ?? "NULL"}\n" +
                   $"AlbumCoverCount: {AlbumCoverCount ?? "NULL"}\n" +
                   $"Compilation: {Compilation ?? "NULL"}";
        }

        /// <summary>
        /// Prints all properties of this track.
        /// </summary>
        public void PrintAllProperties()
        {
            Console.WriteLine(ToAllPropertiesString());
        }
    }
}
