using System;

namespace AudioManager
{
    /// <summary>
    /// The routing and library rules that MusicIntegrator (where files go) and LibChecker (whether
    /// they are where they should be) must agree on. Both consume this one definition so the two
    /// cannot drift apart (INT-3). Pure functions and constants only: no I/O, no console output.
    /// Behaviour is pinned by RulesCharacterisationTests and RulesEngineTests.
    /// </summary>
    internal static class RulesEngine
    {
        /// <summary>Songs from one album needed (library plus batch) for an album subfolder instead of Singles/.</summary>
        internal const int AlbumSubfolderMinTracks = 2;

        /// <summary>Songs by one artist needed (batch plus Misc plus Sources) for an Artists/ folder instead of Misc.</summary>
        internal const int ArtistFolderMinTracks = 3;

        /// <summary>Distinct primary artists on one album for it to count as a various-artist compilation.</summary>
        internal const int CompilationMinArtists = 3;

        /// <summary>Songs by a sampled person for their own Akira The Don People/ folder.</summary>
        internal const int PeopleFolderMinTracks = 3;

        /// <summary>
        /// Canonical album name for comparing album tags and album folder names: parentheticals and
        /// edition/version/remaster/year suffixes removed. The one album-version normalizer.
        /// </summary>
        internal static string NormalizeAlbum(string album)
        {
            return TagFixer.StripAlbumSuffixes(TagFixer.RemoveParentheticals(album));
        }

        /// <summary>True when the album has enough songs to live in its own subfolder.</summary>
        internal static bool UsesAlbumSubfolder(int albumSongCount) => albumSongCount >= AlbumSubfolderMinTracks;

        /// <summary>True when an artist has enough songs to earn an Artists/ folder.</summary>
        internal static bool EarnsArtistFolder(int artistSongCount) => artistSongCount >= ArtistFolderMinTracks;

        /// <summary>True when the number of distinct primary artists makes an album a compilation.</summary>
        internal static bool IsCompilation(int distinctPrimaryArtists) => distinctPrimaryArtists >= CompilationMinArtists;

        /// <summary>True when a sampled person has enough songs for their own People/ folder.</summary>
        internal static bool EarnsPeopleFolder(int personSongCount) => personSongCount >= PeopleFolderMinTracks;

        // Keywords that mark an album as a deluxe/expanded/remastered edition.
        private static readonly string[] DeluxeKeywords = new[] {
            "DELUXE", "DELUXE EDITION", "DELUXE VERSION", "EXPANDED", "EXPANDED EDITION",
            "SUPER DELUXE", "REMASTER", "REMASTERED", "ANNIVERSARY EDITION", "SPECIAL EDITION"
        };

        private static bool HasDeluxeKeyword(string album)
        {
            foreach (var kw in DeluxeKeywords)
                if (album.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>
        /// True if <paramref name="candidate"/> is a deluxe/expanded version of <paramref name="baseAlbum"/>:
        /// candidate starts with baseAlbum AND carries a deluxe keyword that baseAlbum does not. Case-insensitive.
        /// </summary>
        internal static bool IsDeluxeVersionOf(string candidate, string baseAlbum)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(baseAlbum)) return false;
            if (!HasDeluxeKeyword(candidate)) return false;
            if (HasDeluxeKeyword(baseAlbum)) return false; // both deluxe: not a clear hierarchy

            string baseTrimmed = baseAlbum.TrimEnd(' ', '-', '(', '[');
            return candidate.StartsWith(baseTrimmed, StringComparison.OrdinalIgnoreCase);
        }
    }
}
