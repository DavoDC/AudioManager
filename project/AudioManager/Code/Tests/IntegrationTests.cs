using AudioManager.Code.Modules;
using System;
using System.Collections.Generic;
using System.IO;

namespace AudioManager
{
    /// <summary>
    /// End-to-end integration tests spanning multiple modules.
    /// These tests verify module compatibility that unit tests can't catch
    /// (e.g. TrackXML field format matches what Parser feeds to LibChecker).
    /// </summary>
    internal static class IntegrationTests
    {
        /// <summary>
        /// Full read pipeline: TrackXML writes XML → Parser reads it → LibChecker validates.
        /// Key requirement: temp dir must contain "AUDIO_MIRROR" so TrackTag computes the
        /// correct leading-backslash RelPath (\Artists\Artist\Singles\file.xml) that
        /// LibChecker's path-index logic requires.
        /// </summary>
        public static void Pipeline_WriteXML_ParseIt_ValidateWithLibChecker_IsClean()
        {
            string tempBase = Path.Combine(Path.GetTempPath(), $"am_pipe_{Guid.NewGuid():N}");
            string mirrorDir = Path.Combine(tempBase, Constants.MirrorFolderName);
            string singlesDir = Path.Combine(mirrorDir, Constants.ArtistsDir, "Artist A", Constants.SinglesDir);
            string xmlPath = Path.Combine(singlesDir, "Artist A - Song A.xml");
            string cacheFile = Path.Combine(Path.GetTempPath(), $"am_pipe_cache_{Guid.NewGuid():N}.txt");

            try
            {
                Directory.CreateDirectory(singlesDir);

                // Step 1: Write XML via TrackXML (using pre-parsed constructor to avoid filesystem reads)
                var tag = new TrackTag(xmlPath, "Song A", "Artist A", "Test Album",
                    "2020", "1", "Hip-Hop", "00:03:00.0000000", "1", "True", "500", "500");
                TrackXML.Write(xmlPath, tag);

                // Step 2: Parse via Parser (reads AUDIO_MIRROR dir, saves to temp cache)
                var parser = new Parser(mirrorDir, cacheFile);

                Assert.Equal("1", parser.audioTags.Count.ToString(),
                    "pipeline: parser should find 1 XML track");
                Assert.Equal("Song A", parser.audioTags[0].Title,
                    "pipeline: title should survive TrackXML write + Parser read");
                Assert.Equal("Artist A", parser.audioTags[0].Artists,
                    "pipeline: artists should survive the round-trip");

                // Step 3: Validate with LibChecker
                // RelPath computed from mirrorFilePath by TrackTag: \Artists\Artist A\Singles\Artist A - Song A.xml
                // This gives LibChecker the correct path structure for all its rules.
                var checker = new LibChecker(parser.audioTags);
                Assert.True(checker.IsClean,
                    "pipeline: TrackXML-written tags parsed by Parser should pass LibChecker validation");
            }
            finally
            {
                if (Directory.Exists(tempBase)) Directory.Delete(tempBase, recursive: true);
                if (File.Exists(cacheFile)) File.Delete(cacheFile);
            }
        }
    }
}
