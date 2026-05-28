using System;
using System.Collections.Generic;
using System.Linq;

namespace AudioManager
{
    internal static class TagFixerTests
    {
        // RemoveParentheticals

        public static void RemoveParentheticals_StripsFeatArtist() { throw new NotImplementedException(); }
        public static void RemoveParentheticals_StripsFtArtist() { throw new NotImplementedException(); }
        public static void RemoveParentheticals_StripsExplicit() { throw new NotImplementedException(); }
        public static void RemoveParentheticals_StripsAlbumVersion() { throw new NotImplementedException(); }
        public static void RemoveParentheticals_LeavesPlainTitle() { throw new NotImplementedException(); }

        // StripAlbumSuffixes

        public static void StripAlbumSuffixes_StripsDeluxeEdition() { throw new NotImplementedException(); }
        public static void StripAlbumSuffixes_StripsRemastered() { throw new NotImplementedException(); }
        public static void StripAlbumSuffixes_StripsYearSuffix() { throw new NotImplementedException(); }
        public static void StripAlbumSuffixes_LeavesPlainAlbum() { throw new NotImplementedException(); }

        // ExtractAndFixArtists

        public static void ExtractAndFixArtists_ExtractsFeaturedArtist() { throw new NotImplementedException(); }
        public static void ExtractAndFixArtists_SplitsAmpersandInFeat() { throw new NotImplementedException(); }
        public static void ExtractAndFixArtists_NoDuplicates() { throw new NotImplementedException(); }
        public static void ExtractAndFixArtists_SkipsOfBandNameClarification() { throw new NotImplementedException(); }

        // ShouldFixGenre

        public static void ShouldFixGenre_MusivationArtistMissingGenre() { throw new NotImplementedException(); }
        public static void ShouldFixGenre_MusivationArtistAlreadyHasGenre() { throw new NotImplementedException(); }
        public static void ShouldFixGenre_NormalArtistNoChange() { throw new NotImplementedException(); }

        // DetermineGenre

        public static void DetermineGenre_MusivationForAkiraTheDon() { throw new NotImplementedException(); }
        public static void DetermineGenre_MusivationForLootBryonSmith() { throw new NotImplementedException(); }
        public static void DetermineGenre_MotivationForOtherArtist() { throw new NotImplementedException(); }
    }
}
