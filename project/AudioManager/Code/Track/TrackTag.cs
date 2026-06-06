using AudioManager;
using TagLib;
using File = System.IO.File;
using ID3Tag = TagLib.Id3v2.Tag;

namespace AudioManager.Code.Modules
{
    /// <summary>
    /// An audio track as an MP3 tag
    /// </summary>
    internal class TrackTag : Track
    {
        /// <summary>
        /// Construct a TrackTag from pre-parsed cache field values (no file I/O).
        /// </summary>
        internal TrackTag(string relPath, string title, string artists, string album,
                          string year, string trackNumber, string genres, string length,
                          string albumCoverCount, string compilation, string coverWidth, string coverHeight)
        {
            RelPath        = relPath;
            Title          = title;
            Artists        = artists;
            Album          = album;
            Year           = year;
            TrackNumber    = trackNumber;
            Genres         = genres;
            Length         = length;
            AlbumCoverCount = albumCoverCount;
            Compilation    = compilation;
            CoverWidth     = coverWidth;
            CoverHeight    = coverHeight;
        }

        /// <summary>
        /// Construct a track tag
        /// </summary>
        /// <param name="mirrorFilePath">The mirror file path</param>
        public TrackTag(string mirrorFilePath)
        {
            // Always initialise relative path
            int relStartPos = mirrorFilePath.LastIndexOf(Constants.MirrorFolderName);
            RelPath = mirrorFilePath.Remove(0, relStartPos + Constants.MirrorFolderName.Length);

            // Get file contents of mirror file (should be a file path)
            string[] fileContents = File.ReadAllLines(mirrorFilePath);

            // If mirror file does not contain a single, valid file path
            if (fileContents.Length != 1 || !File.Exists(fileContents[0]))
            {
                // Then the mirror file already has XML content
                // So load metadata from XML instead
                
                TrackXML.Read(mirrorFilePath, this);
                return;
            }

            // # Otherwise, if mirror file contains a valid path, initialise fields

            // Load metadata object from file
            TagLib.File tagFile = TagLib.File.Create(fileContents[0]);

            // Extract duration info from properties
            Length = tagFile.Properties.Duration.ToString();

            // Extract info from generic tag
            Tag tag = tagFile.Tag;
            Title = string.IsNullOrEmpty(tag.Title) ? "Missing" : tag.Title;
            Artists = string.IsNullOrEmpty(tag.JoinedPerformers) ? "Missing" : tag.JoinedPerformers;
            Album = string.IsNullOrEmpty(tag.Album) ? "Missing" : tag.Album;
            Year = (tag.Year == 0) ? "Missing" : tag.Year.ToString();
            TrackNumber = tag.Track.ToString();
            Genres = string.IsNullOrEmpty(tag.JoinedGenres) ? "Missing" : tag.JoinedGenres;
            AlbumCoverCount = (tag.Pictures?.Length ?? 0).ToString();

            // Extract compilation info from ID3 tag (see https://id3.org/iTunes%20Compilation%20Flag)
            ID3Tag id3tag = (ID3Tag) tagFile.GetTag(TagLib.TagTypes.Id3v2, true);
            Compilation = id3tag.IsCompilation.ToString();

            // Extract album art dimensions (pure byte parsing - no System.Drawing dependency)
            var pics = tag.Pictures;
            if (pics != null && pics.Length > 0)
            {
                var (w, h) = GetCoverDimensions(pics[0].Data.Data);
                CoverWidth  = w > 0 ? w.ToString() : "Unknown";
                CoverHeight = h > 0 ? h.ToString() : "Unknown";
            }
            else
            {
                CoverWidth = "0";
                CoverHeight = "0";
            }

            TrackXML.Write(mirrorFilePath, this);
        }

        /// <summary>
        /// Reads pixel dimensions from raw JPEG or PNG album art bytes.
        /// Returns (0, 0) for unsupported/unreadable formats.
        /// </summary>
        private static (int width, int height) GetCoverDimensions(byte[] data)
        {
            if (data == null || data.Length < 8) return (0, 0);

            // PNG: magic 89 50 4E 47 - IHDR chunk has dimensions at bytes 16-23
            if (data.Length >= 24 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            {
                int w = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
                int h = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
                return (w, h);
            }

            // JPEG: FF D8 - scan for SOF (Start Of Frame) marker
            if (data[0] == 0xFF && data[1] == 0xD8)
            {
                int i = 2;
                while (i + 4 < data.Length)
                {
                    if (data[i] != 0xFF) break;
                    byte marker = data[i + 1];
                    if (marker == 0xFF) { i++; continue; }
                    int segLen = (data[i + 2] << 8) | data[i + 3];
                    bool isSof = (marker >= 0xC0 && marker <= 0xC3) || (marker >= 0xC5 && marker <= 0xC7) ||
                                 (marker >= 0xC9 && marker <= 0xCB) || (marker >= 0xCD && marker <= 0xCF);
                    if (isSof && i + 8 < data.Length)
                        return ((data[i + 7] << 8) | data[i + 8], (data[i + 5] << 8) | data[i + 6]);
                    i += 2 + segLen;
                }
            }

            return (0, 0);
        }
    }
}
