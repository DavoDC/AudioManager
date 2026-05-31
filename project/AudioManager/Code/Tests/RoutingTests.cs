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

        public static void Routing_MotivationGenre_RoutesToMotivation()
        {
            string lib = RoutingFixtures.CreateLibraryFixture();
            try
            {
                var integrator = new MusicIntegrator(lib);
                string dest = GetDest(integrator, MakeTrack("Some Speaker", "Talk Title", genres: "Motivation"));
                string expected = Path.Combine(lib, Constants.MotivDir);
                Assert.Equal(expected, dest, "Motivation genre -> Motivation/");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_ExistingArtist_TwoAlbumSongs_RoutesToAlbumSubfolder()
        {
            string lib = RoutingFixtures.CreateLibraryFixture(artistFolders: new[] { "Known Artist" });
            try
            {
                // 2 songs is the minimum threshold for album subfolder routing (albumCount >= 2)
                RoutingFixtures.AddAlbumFiles(lib, "Known Artist", "Debut Album", fileCount: 2);
                var integrator = new MusicIntegrator(lib);
                string dest = GetDest(integrator, MakeTrack("Known Artist", "Track 3", album: "Debut Album"));
                string expected = Path.Combine(lib, Constants.ArtistsDir, "Known Artist", "Debut Album");
                Assert.Equal(expected, dest, "existing artist + 2 album songs -> album subfolder (exact threshold)");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_ExistingArtist_OneAlbumSong_RoutesToSingles()
        {
            string lib = RoutingFixtures.CreateLibraryFixture(artistFolders: new[] { "Known Artist" });
            try
            {
                // 1 album song is below the 2-song threshold -> Routes to Singles
                RoutingFixtures.AddAlbumFiles(lib, "Known Artist", "Debut Album", fileCount: 1);
                var integrator = new MusicIntegrator(lib);
                string dest = GetDest(integrator, MakeTrack("Known Artist", "Track 2", album: "Debut Album"));
                string expected = Path.Combine(lib, Constants.ArtistsDir, "Known Artist", "Singles");
                Assert.Equal(expected, dest, "existing artist + 1 album song -> Singles (below threshold)");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_ExistingArtist_MissingAlbum_RoutesToSingles()
        {
            string lib = RoutingFixtures.CreateLibraryFixture(artistFolders: new[] { "Known Artist" });
            try
            {
                var integrator = new MusicIntegrator(lib);
                string dest = GetDest(integrator, MakeTrack("Known Artist", "Some Song", album: "Missing"));
                string expected = Path.Combine(lib, Constants.ArtistsDir, "Known Artist", "Singles");
                Assert.Equal(expected, dest, "existing artist, album=Missing -> Singles (no distinct album)");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_ExistingArtist_AlbumMatchesArtistName_RoutesToSingles()
        {
            string lib = RoutingFixtures.CreateLibraryFixture(artistFolders: new[] { "Known Artist" });
            try
            {
                // When album == primary artist name, GetDestDir treats it as no distinct album -> Singles
                var integrator = new MusicIntegrator(lib);
                string dest = GetDest(integrator, MakeTrack("Known Artist", "Some Song", album: "Known Artist"));
                string expected = Path.Combine(lib, Constants.ArtistsDir, "Known Artist", "Singles");
                Assert.Equal(expected, dest, "existing artist, album == artist name -> Singles (no distinct album)");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        // ---- Akira The Don routing ----

        public static void Routing_AkiraTheDon_NoSampledPerson_RoutesToATDSingles()
        {
            // Single-artist ATD track: no second artist entry means no "sampled person" -> Singles
            // This path avoids CountAkiraTheDonPersonSongs (no real library scan).
            string lib = RoutingFixtures.CreateLibraryFixture();
            try
            {
                var integrator = new MusicIntegrator(lib);
                string dest = GetDest(integrator, MakeTrack("Akira The Don", "Philosophy Talk", genres: "Musivation"));
                string expected = Path.Combine(lib, Constants.MusivDir, "Akira The Don", "Singles");
                Assert.Equal(expected, dest, "ATD with no sampled person -> Musivation/Akira The Don/Singles/");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_AkiraTheDon_SampledPersonBelowThreshold_RoutesToATDSingles()
        {
            // Sampled person with 0 songs in fixture -> below 3-song threshold -> ATD/Singles/
            // CountAkiraTheDonPersonSongs now uses _libraryPath so the fixture is isolated.
            string lib = RoutingFixtures.CreateLibraryFixture();
            try
            {
                var integrator = new MusicIntegrator(lib);
                var track = new AudioManager.Code.Modules.Track { Artists = "Akira The Don;Test Person", Title = "Philosophy", Album = "Missing", Genres = "Musivation" };
                string dest = integrator.GetDestDir(track, new HashSet<string>(), out _, out _);
                string expected = Path.Combine(lib, Constants.MusivDir, "Akira The Don", Constants.SinglesDir);
                Assert.Equal(expected, dest, "ATD sampled person below 3-song threshold -> Akira The Don/Singles/");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_AkiraTheDon_SampledPerson3Plus_RoutesToPeopleSingles()
        {
            // Sampled person with 3+ songs in fixture -> People/{person}/Singles/ (no distinct album)
            string lib = RoutingFixtures.CreateLibraryFixture();
            try
            {
                RoutingFixtures.AddATDPeopleFiles(lib, "Test Person", personFileCount: 3);
                var integrator = new MusicIntegrator(lib);
                var track = new AudioManager.Code.Modules.Track { Artists = "Akira The Don;Test Person", Title = "Philosophy", Album = "Missing", Genres = "Musivation" };
                string dest = integrator.GetDestDir(track, new HashSet<string>(), out _, out _);
                string expected = Path.Combine(lib, Constants.MusivDir, "Akira The Don", "People", "Test Person", Constants.SinglesDir);
                Assert.Equal(expected, dest, "ATD sampled person 3+ songs, no album -> People/Test Person/Singles/");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_AkiraTheDon_SampledPerson2AlbumSongs_RoutesToPeopleAlbumFolder()
        {
            // Sampled person with 3+ songs and 2 from same album -> People/{person}/{album}/
            string lib = RoutingFixtures.CreateLibraryFixture();
            try
            {
                RoutingFixtures.AddATDPeopleFiles(lib, "Test Person", personFileCount: 3, albumName: "Great Album", albumFileCount: 2);
                var integrator = new MusicIntegrator(lib);
                var track = new AudioManager.Code.Modules.Track { Artists = "Akira The Don;Test Person", Title = "Philosophy", Album = "Great Album", Genres = "Musivation" };
                string dest = integrator.GetDestDir(track, new HashSet<string>(), out _, out _);
                string expected = Path.Combine(lib, Constants.MusivDir, "Akira The Don", "People", "Test Person", "Great Album");
                Assert.Equal(expected, dest, "ATD sampled person 3+ songs, 2 from album -> People/Test Person/Great Album/");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }
    }
}
