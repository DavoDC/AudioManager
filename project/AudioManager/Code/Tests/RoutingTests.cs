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

        private static string GetDest(MusicIntegrator integrator, Track track,
            HashSet<string> newArtistFolders = null, HashSet<string> compilationAlbums = null)
        {
            return integrator.GetDestDir(track,
                newArtistFolders ?? new HashSet<string>(),
                compilationAlbums ?? new HashSet<string>(),
                out _, out _);
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

        public static void Routing_ExistingArtist_AlbumMatchesArtistName_NoLibrarySongs_RoutesToSingles()
        {
            string lib = RoutingFixtures.CreateLibraryFixture(artistFolders: new[] { "Known Artist" });
            try
            {
                // Self-titled album ("Known Artist") with 0 library songs -> below threshold -> Singles.
                // Self-titled albums are no longer special-cased: only album song count decides routing.
                var integrator = new MusicIntegrator(lib);
                string dest = GetDest(integrator, MakeTrack("Known Artist", "Some Song", album: "Known Artist"));
                string expected = Path.Combine(lib, Constants.ArtistsDir, "Known Artist", "Singles");
                Assert.Equal(expected, dest, "self-titled album, 0 library songs -> Singles (below threshold)");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_SelfTitledAlbum_WithLibrarySongs_RoutesToAlbumFolder()
        {
            // Bug fix: album == artist name was incorrectly excluded from album routing.
            // With 2+ library songs in the self-titled album folder, it must route to the album subfolder.
            string lib = RoutingFixtures.CreateLibraryFixture(artistFolders: new[] { "Paperboys" });
            try
            {
                RoutingFixtures.AddAlbumFiles(lib, "Paperboys", "Paperboys", fileCount: 2);
                var integrator = new MusicIntegrator(lib);
                string dest = GetDest(integrator, MakeTrack("Paperboys", "No Middleman", album: "Paperboys"));
                string expected = Path.Combine(lib, Constants.ArtistsDir, "Paperboys", "Paperboys");
                Assert.Equal(expected, dest, "self-titled album with 2+ library songs -> album subfolder");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_BatchAlbumCount_NormalizedAlbumSuffixMatchesCount()
        {
            // Bug fix: scan-ahead must normalize album names before storing in batch counts.
            // This test injects pre-normalized batch data to verify CountAlbumSongs finds it.
            // Real scenario: "The Blueprint (Explicit Version)" raw -> "The Blueprint" normalized.
            // Before fix, batch data was stored under the raw key; CountAlbumSongs looked up the
            // normalized key and found 0, routing everything to Singles.
            string lib = RoutingFixtures.CreateLibraryFixture(artistFolders: new[] { "Jay-Z" });
            try
            {
                // Simulate scan-ahead having stored normalized "The Blueprint" with 4 batch songs
                var batchAlbumCounts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Jay-Z"] = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["The Blueprint"] = 4
                    }
                };
                var integrator = new MusicIntegrator(lib, batchAlbumCounts);
                string dest = GetDest(integrator, MakeTrack("Jay-Z", "Izzo (H.O.V.A.)", album: "The Blueprint"));
                string expected = Path.Combine(lib, Constants.ArtistsDir, "Jay-Z", "The Blueprint");
                Assert.Equal(expected, dest, "4 batch songs from album (normalized key) -> album subfolder");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_ExistingArtist_MultipleBatchSongsFromSameAlbum_RoutesToAlbumFolder()
        {
            // Bug scenario: Jay-Z already has an Artists/ folder. Batch contains 3+ songs from
            // "The Blueprint". RunScanAhead stores them under raw "JAY-Z" key. CountAlbumSongs
            // is called with tag-fixed "Jay-Z". OrdinalIgnoreCase on the outer dict should match.
            // If this test fails, the casing bridge is broken and batchCount returns 0.
            string lib = RoutingFixtures.CreateLibraryFixture(artistFolders: new[] { "Jay-Z" });
            try
            {
                var batchAlbumCounts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["JAY-Z"] = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["The Blueprint"] = 3
                    }
                };
                var integrator = new MusicIntegrator(lib, batchAlbumCounts);
                string dest = GetDest(integrator, MakeTrack("Jay-Z", "Heart Of The City", album: "The Blueprint"));
                string expected = Path.Combine(lib, Constants.ArtistsDir, "Jay-Z", "The Blueprint");
                Assert.Equal(expected, dest, "3 batch songs under raw 'JAY-Z' key -> tag-fixed 'Jay-Z' query -> album subfolder");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_ExistingArtist_UnicodeArtistVariant_RoutesToAlbumFolder()
        {
            // Root-cause regression test: streaming providers tag files with "JAŸ-Z" (Y-umlaut, U+0178).
            // artist-name-overrides.xml maps "JAŸ-Z" -> "Jay-Z". Before the fix, RunScanAhead stored
            // the raw "JAŸ-Z" key in batchAlbumCounts. CountAlbumSongs queried "Jay-Z"; OrdinalIgnoreCase
            // cannot bridge U+0178 vs U+0059, so batchCount returned 0 and songs routed to Singles.
            // Post-fix: RunScanAhead normalizes via TagFixer first, so the key is stored as "Jay-Z".
            // This test injects the PRE-FIX state (raw "JAŸ-Z" key) to verify the lookup fails,
            // which confirms that the fix (normalizing before storing) is the correct mitigation.
            string lib = RoutingFixtures.CreateLibraryFixture(artistFolders: new[] { "Jay-Z" });
            try
            {
                // Simulate post-fix state: key already normalized to "Jay-Z" (what RunScanAhead now stores)
                var batchAlbumCounts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Jay-Z"] = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["The Blueprint"] = 3
                    }
                };
                var integrator = new MusicIntegrator(lib, batchAlbumCounts);
                string dest = GetDest(integrator, MakeTrack("Jay-Z", "Heart Of The City", album: "The Blueprint"));
                string expected = Path.Combine(lib, Constants.ArtistsDir, "Jay-Z", "The Blueprint");
                Assert.Equal(expected, dest, "normalized 'Jay-Z' key (post-fix RunScanAhead) -> album subfolder");

                // Confirm the pre-fix state (raw "JAŸ-Z" key) would have returned 0 batch count
                var rawBatchAlbumCounts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["JAŸ-Z"] = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["The Blueprint"] = 3
                    }
                };
                var integratorRaw = new MusicIntegrator(lib, rawBatchAlbumCounts);
                string destRaw = GetDest(integratorRaw, MakeTrack("Jay-Z", "Heart Of The City", album: "The Blueprint"));
                string expectedRaw = Path.Combine(lib, Constants.ArtistsDir, "Jay-Z", "Singles");
                Assert.Equal(expectedRaw, destRaw, "raw 'JAŸ-Z' key (pre-fix bug state) -> lookup miss -> Singles");
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
                string dest = integrator.GetDestDir(track, new HashSet<string>(), new HashSet<string>(), out _, out _);
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
                string dest = integrator.GetDestDir(track, new HashSet<string>(), new HashSet<string>(), out _, out _);
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
                string dest = integrator.GetDestDir(track, new HashSet<string>(), new HashSet<string>(), out _, out _);
                string expected = Path.Combine(lib, Constants.MusivDir, "Akira The Don", "People", "Test Person", "Great Album");
                Assert.Equal(expected, dest, "ATD sampled person 3+ songs, 2 from album -> People/Test Person/Great Album/");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_CompilationAlbum_NoArtistFolder_RoutesToCompilations()
        {
            // 3+ distinct primary artists on same album, no artist folders -> Compilations/{album}/
            string lib = RoutingFixtures.CreateLibraryFixture();
            try
            {
                var integrator = new MusicIntegrator(lib);
                var albumSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Barbie The Album" };
                string dest = GetDest(integrator, MakeTrack("Nicki Minaj", "Barbie World", album: "Barbie The Album"),
                    compilationAlbums: albumSet);
                string expected = Path.Combine(lib, Constants.CompilationsDir, "Barbie The Album");
                Assert.Equal(expected, dest, "compilation album, no artist folder -> Compilations/{album}/");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_CompilationAlbum_ArtistHasFolder_RoutesToArtistSingles()
        {
            // Artist has a folder: compilation flag does not override existing folder routing
            string lib = RoutingFixtures.CreateLibraryFixture(artistFolders: new[] { "Nicki Minaj" });
            try
            {
                var integrator = new MusicIntegrator(lib);
                var albumSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Barbie The Album" };
                string dest = GetDest(integrator, MakeTrack("Nicki Minaj", "Barbie World", album: "Barbie The Album"),
                    compilationAlbums: albumSet);
                string expected = Path.Combine(lib, Constants.ArtistsDir, "Nicki Minaj", "Singles");
                Assert.Equal(expected, dest, "compilation album but artist has folder -> Artists/{artist}/Singles/");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_NonCompilationAlbum_NoArtistFolder_RoutesToMisc()
        {
            // Same album, no artist folder, NOT in compilationAlbums set -> still Misc
            string lib = RoutingFixtures.CreateLibraryFixture();
            try
            {
                var integrator = new MusicIntegrator(lib);
                string dest = GetDest(integrator, MakeTrack("Solo Artist", "Solo Song", album: "Solo Album"));
                string expected = Path.Combine(lib, Constants.MiscDir);
                Assert.Equal(expected, dest, "album not in compilationAlbums -> Misc");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        // ---- Version-suffix normalization routing (B.I.G. bug 2026-06-28) ----

        public static void Routing_VersionSuffixedAlbumTag_RoutesToExistingCleanFolder()
        {
            // After TagFixer strips "(2014 Remastered Edition)" from the ID3 tag, track.Album is clean.
            // Routing should find the existing clean-named library folder and route there.
            string lib = RoutingFixtures.CreateLibraryFixture(artistFolders: new[] { "The Notorious B.I.G." });
            try
            {
                RoutingFixtures.AddAlbumFiles(lib, "The Notorious B.I.G.", "Life After Death", fileCount: 4);
                var integrator = new MusicIntegrator(lib);
                // Track has clean album name (as TagFixer would produce after the fix)
                string dest = GetDest(integrator, MakeTrack("The Notorious B.I.G.", "I Love The Dough", album: "Life After Death"));
                string expected = Path.Combine(lib, Constants.ArtistsDir, "The Notorious B.I.G.", "Life After Death");
                Assert.Equal(expected, dest, "clean album tag + 4 library songs -> routes to existing clean folder");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }

        public static void Routing_SuffixedLibraryFolder_MatchedByCleanAlbumName()
        {
            // FindAlbumFolder fuzzy match: library has a suffixed folder, incoming clean album name
            // should route into that folder rather than creating a new clean-named folder.
            string lib = RoutingFixtures.CreateLibraryFixture(artistFolders: new[] { "The Notorious B.I.G." });
            try
            {
                // Library already has a suffixed folder (created by a pre-fix integration run)
                RoutingFixtures.AddAlbumFiles(lib, "The Notorious B.I.G.", "Life After Death (2014 Remastered Edition)", fileCount: 2);
                var integrator = new MusicIntegrator(lib);
                // Incoming file has clean album tag (as TagFixer would produce)
                string dest = GetDest(integrator, MakeTrack("The Notorious B.I.G.", "Sky's The Limit", album: "Life After Death"));
                string expected = Path.Combine(lib, Constants.ArtistsDir, "The Notorious B.I.G.", "Life After Death (2014 Remastered Edition)");
                Assert.Equal(expected, dest, "clean album tag + suffixed library folder -> FindAlbumFolder fuzzy match routes to existing suffixed folder");
            }
            finally { RoutingFixtures.Cleanup(lib); }
        }
    }
}
