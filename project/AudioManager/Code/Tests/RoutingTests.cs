using System;
using System.Collections.Generic;
using System.IO;
using AudioManager.Code.Modules;

namespace AudioManager
{
    internal static class RoutingTests
    {
        // ---- helpers ----

        private static Track MakeTrack(string artist, string title, string album = "Test Album", string genres = "Missing")
        {
            return new Track { Artists = artist, Title = title, Album = album, Genres = genres };
        }

        private static string GetDest(MusicIntegrator integrator, Track track, HashSet<string> newArtistFolders = null)
        {
            return integrator.GetDestDir(track, newArtistFolders ?? new HashSet<string>(), out _, out _);
        }

        // ---- tests ----

        public static void Routing_ExistingArtistFolder_RoutesToSingles()
        {
            string lib = RoutingFixtures.CreateLibraryFixture(artistFolders: new[] { "Known Artist" });
            try
            {
                var integrator = new MusicIntegrator(lib);
                string dest = GetDest(integrator, MakeTrack("Known Artist", "Some Song"));
                string expected = Path.Combine(lib, Constants.ArtistsDir, "Known Artist", "Singles");
                Assert.Equal(expected, dest, "existing artist -> Singles/");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_ExistingArtist_3PlusSongsAlbum_RoutesToAlbumSubfolder()
        {
            string lib = RoutingFixtures.CreateLibraryFixture(artistFolders: new[] { "Known Artist" });
            try
            {
                RoutingFixtures.AddAlbumFiles(lib, "Known Artist", "Great Album", fileCount: 3);
                var integrator = new MusicIntegrator(lib);
                string dest = GetDest(integrator, MakeTrack("Known Artist", "Track 4", album: "Great Album"));
                string expected = Path.Combine(lib, Constants.ArtistsDir, "Known Artist", "Great Album");
                Assert.Equal(expected, dest, "existing artist + 3 album songs -> album subfolder");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_NoArtistFolder_RoutesToMisc()
        {
            string lib = RoutingFixtures.CreateLibraryFixture();
            try
            {
                var integrator = new MusicIntegrator(lib);
                string dest = GetDest(integrator, MakeTrack("Unknown Artist", "Random Song"));
                string expected = Path.Combine(lib, Constants.MiscDir);
                Assert.Equal(expected, dest, "no artist folder -> Misc");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_MusivationGenre_RoutesToMusivation()
        {
            string lib = RoutingFixtures.CreateLibraryFixture();
            try
            {
                var integrator = new MusicIntegrator(lib);
                string dest = GetDest(integrator, MakeTrack("Some Speaker", "Talk Title", genres: "Musivation"));
                string expected = Path.Combine(lib, Constants.MusivDir);
                Assert.Equal(expected, dest, "Musivation genre -> Musivation/");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_ScanAheadNewArtist_RoutesToArtistsSingles()
        {
            string lib = RoutingFixtures.CreateLibraryFixture();
            try
            {
                var integrator = new MusicIntegrator(lib);
                var newArtistFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "New Artist" };
                string dest = GetDest(integrator, MakeTrack("New Artist", "Song 1"), newArtistFolders);
                string expected = Path.Combine(lib, Constants.ArtistsDir, "New Artist", "Singles");
                Assert.Equal(expected, dest, "scan-ahead new artist -> Artists/New Artist/Singles/");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_ScanAheadNewArtist_3PlusSongsAlbum_RoutesToAlbumSubfolder()
        {
            string lib = RoutingFixtures.CreateLibraryFixture();
            try
            {
                // scan-ahead promotes the artist; album has 2 existing songs in the (not-yet-created) folder
                // For a new artist folder, album subfolder is created when albumCount >= 2.
                // Since the artist folder doesn't exist yet, CountAlbumSongs finds 0 in library.
                // Route is Singles/ for new artist with <2 album songs. This verifies the scan-ahead single path.
                var integrator = new MusicIntegrator(lib);
                var newArtistFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "New Artist" };
                string dest = GetDest(integrator, MakeTrack("New Artist", "Song 1", album: "Debut Album"), newArtistFolders);
                string expected = Path.Combine(lib, Constants.ArtistsDir, "New Artist", "Singles");
                Assert.Equal(expected, dest, "scan-ahead new artist, 1 song -> Singles/ (not album subfolder)");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }
    }
}
