﻿namespace AudioMirror.Code.Modules
{
    internal class Track
    {
        // The track's title
        private string title;
        public string Title
        {
            get => title;
            set => title = value;
        }


        // The track's artists (comma separated)
        private string artists;
        public string Artists 
        { 
            get => artists;
            set => artists = value;
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


        // The track's genre
        private string genre;
        public string Genre 
        { 
            get => genre; set => 
            genre = value; 
        }


        // The track's duration
        private string length;
        public string Length 
        { 
            get => length; 
            set => length = value; 
        }
    }
}
