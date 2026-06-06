using System;
using System.Linq;

namespace AudioManager.Code.Modules
{
    internal class Track
    {
        public string RelPath { get; set; }
        public string Title { get; set; }
        public string Artists { get; set; }

        public string PrimaryArtist
        {
            get => Track.ProcessProperty(Artists)[0];
        }

        public string Album { get; set; }
        public string Year { get; set; }
        public string TrackNumber { get; set; }
        public string Genres { get; set; }
        public string Length { get; set; }
        public string AlbumCoverCount { get; set; }

        // "0" = no art, "Unknown" = art present but format unrecognised
        public string CoverWidth { get; set; }
        public string CoverHeight { get; set; }
        public string Compilation { get; set; }

        public override string ToString()
        {
            return $"{Artists} - {Title}";
        }

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

        public static string[] ProcessProperty(string full)
        {
            char[] separators = { ';', ',' };
            if (!separators.Any(full.Contains))
            {
                return new[] { full };
            }
            char selectedSeparator = separators.First(s => full.Contains(s));
            return full.Split(selectedSeparator).Select(a => a.Trim()).ToArray();
        }

        public void PrintAllProperties()
        {
            Console.WriteLine(ToAllPropertiesString());
        }
    }
}
