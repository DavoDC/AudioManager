using System.Collections.Generic;
using AudioManager.Code.Modules;

namespace AudioManager
{
    /// <summary>
    /// Coverage for the "Duplicate-resolution UI" JSON/manifest plumbing (docs/Development/IDEAS.md):
    /// the libraryDuplicate block MusicIntegrator.BuildJson emits, and PresentDuplicateAndDecide's
    /// manifest-override path that lets a GUI review-stage decision beat the exe's own recommendation
    /// on a real (noInput) run. Uses the test-only MusicIntegrator(testLibraryPath, ...) constructors -
    /// no real NewMusic/library filesystem access, no console interaction.
    /// </summary>
    internal static class MusicIntegratorDuplicateJsonTests
    {
        private static MusicIntegrator.LogEntry LibraryDupEntry() => new MusicIntegrator.LogEntry
        {
            Filename = "Kendrick Lamar - HUMBLE.mp3",
            Artists = "Kendrick Lamar",
            Title = "HUMBLE.",
            Album = "DAMN.",
            LibraryDuplicate = true,
            DupLibraryPath = "Artists\\Kendrick Lamar\\DAMN.\\Kendrick Lamar - HUMBLE.mp3",
            DupLibraryTrack = "HUMBLE.",
            DupLibraryAlbum = "DAMN.",
            DupNewAlbum = "DAMN. (Deluxe)",
            DupRecommendationKey = "L",
            DupRecommendation = "Delete library copy",
            DupReason = "New file album 'DAMN. (Deluxe)' is a deluxe/expanded version of library album 'DAMN.' - deluxe preferred",
        };

        public static void BuildJson_EmitsLibraryDuplicateBlock_ForADuplicateEntry()
        {
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry> { LibraryDupEntry() });
            Assert.True(json.Contains("\"libraryDuplicate\": true"), "libraryDuplicate flag true");
            Assert.True(json.Contains("\"dupLibraryPath\": \"Artists\\\\Kendrick Lamar\\\\DAMN.\\\\Kendrick Lamar - HUMBLE.mp3\""),
                "dupLibraryPath present");
            Assert.True(json.Contains("\"dupLibraryTrack\": \"HUMBLE.\""), "dupLibraryTrack present");
            Assert.True(json.Contains("\"dupLibraryAlbum\": \"DAMN.\""), "dupLibraryAlbum present");
            Assert.True(json.Contains("\"dupNewAlbum\": \"DAMN. (Deluxe)\""), "dupNewAlbum present");
            Assert.True(json.Contains("\"dupRecommendationKey\": \"L\""), "dupRecommendationKey present");
            Assert.True(json.Contains("\"dupRecommendation\": \"Delete library copy\""), "dupRecommendation present");
            Assert.True(json.Contains("\"dupReason\":"), "dupReason present");
        }

        public static void BuildJson_NonDuplicateEntry_LibraryDuplicateFalseAndFieldsNull()
        {
            var entry = new MusicIntegrator.LogEntry { Filename = "Clean - Song.mp3" };
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry> { entry });
            Assert.True(json.Contains("\"libraryDuplicate\": false"), "libraryDuplicate false for a non-duplicate");
            Assert.True(json.Contains("\"dupLibraryPath\": null"), "dupLibraryPath null when no duplicate");
            Assert.True(json.Contains("\"dupRecommendation\": null"), "dupRecommendation null when no duplicate");
        }

        public static void BuildJson_InBatchDuplicateFieldMeaningIsUnchanged()
        {
            // inBatchDuplicate must keep meaning "same artist+title twice in this batch" -
            // wholly independent of the new libraryDuplicate block.
            var entry = new MusicIntegrator.LogEntry { Filename = "A.mp3", InBatchDuplicate = true, LibraryDuplicate = false };
            string json = MusicIntegrator.BuildJson(new List<MusicIntegrator.LogEntry> { entry });
            Assert.True(json.Contains("\"inBatchDuplicate\": true"), "inBatchDuplicate still serializes independently");
            Assert.True(json.Contains("\"libraryDuplicate\": false"), "libraryDuplicate false while inBatchDuplicate true");
        }

        // ---------------------------------------- PresentDuplicateAndDecide manifest override

        private static MusicIntegrator.ScannedFile MakeDupScannedFile(char recommendedKey)
        {
            var track = new Track { Artists = "Kendrick Lamar", Title = "HUMBLE.", Album = "DAMN. (Deluxe)" };
            var dup = new MusicIntegrator.DupData
            {
                RecommendedKey = recommendedKey,
                OptionsLine = "  [D] ...   [L] ...   [K] ...   [Q] ...",
                RelMirrorPath = "Artists\\Kendrick Lamar\\DAMN.\\Kendrick Lamar - HUMBLE.mp3",
                RelLibraryPath = "Artists\\Kendrick Lamar\\DAMN.\\Kendrick Lamar - HUMBLE.mp3",
                DisplayNewFilename = "Kendrick Lamar - HUMBLE.mp3",
            };
            return new MusicIntegrator.ScannedFile
            {
                SourcePath = @"C:\fake\NewMusic\Kendrick Lamar - HUMBLE.mp3",
                Track = track,
                LogEntry = new MusicIntegrator.LogEntry { Filename = "Kendrick Lamar - HUMBLE.mp3" },
                Duplicate = dup,
            };
        }

        public static void PresentDuplicateAndDecide_NoInput_NoManifestOverride_UsesRecommendation()
        {
            var integrator = new MusicIntegrator("C:\\fake\\lib", noInput: true, manifest: null);
            var sf = MakeDupScannedFile('L');
            integrator.PresentDuplicateAndDecide(sf);
            Assert.Equal("L", sf.Duplicate.Decision.ToString());
        }

        public static void PresentDuplicateAndDecide_NoInput_NoRecommendation_DefaultsToKeepBoth()
        {
            var integrator = new MusicIntegrator("C:\\fake\\lib", noInput: true, manifest: null);
            var sf = MakeDupScannedFile('\0');
            integrator.PresentDuplicateAndDecide(sf);
            Assert.Equal("K", sf.Duplicate.Decision.ToString());
        }

        public static void PresentDuplicateAndDecide_ManifestResolution_OverridesRecommendation()
        {
            var manifest = IntegrationManifest.Parse(
                @"[{""filename"": ""Kendrick Lamar - HUMBLE.mp3"", ""artist"": ""Kendrick Lamar"",
                    ""title"": ""HUMBLE."", ""dupResolution"": ""D""}]", out string error);
            Assert.True(error == null, $"manifest must parse cleanly, got: {error}");

            var integrator = new MusicIntegrator("C:\\fake\\lib", noInput: true, manifest: manifest);
            var sf = MakeDupScannedFile('L'); // recommendation says L, manifest says D
            integrator.PresentDuplicateAndDecide(sf);
            Assert.Equal("D", sf.Duplicate.Decision.ToString());
        }

        public static void PresentDuplicateAndDecide_ManifestResolution_MatchesByArtistTitleAfterRename()
        {
            // The manifest's filename candidate is the pre-rename dry-run name; the ScannedFile's
            // SourcePath here simulates the post-TagFixer-rename real-run name being different -
            // GetDupResolution must still find it via the canonical artist/title fallback.
            var manifest = IntegrationManifest.Parse(
                @"[{""filename"": ""original-dryrun-name.mp3"", ""artist"": ""Kendrick Lamar"",
                    ""title"": ""HUMBLE."", ""dupResolution"": ""K""}]", out _);

            var integrator = new MusicIntegrator("C:\\fake\\lib", noInput: true, manifest: manifest);
            var sf = MakeDupScannedFile('L');
            integrator.PresentDuplicateAndDecide(sf);
            Assert.Equal("K", sf.Duplicate.Decision.ToString());
        }
    }
}
