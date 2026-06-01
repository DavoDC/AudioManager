using System;
using System.IO;

namespace AudioManager
{
    /// <summary>
    /// Tests for AgeChecker - all 5 decision branches.
    /// Uses a temp lastRunInfo file via the injected path to avoid touching Constants.LastRunInfoFilePath.
    /// Note: AgeChecker.RegenMirror is static; tests are ordered so each sets it explicitly.
    /// </summary>
    internal static class AgeCheckerTests
    {
        private static string TempInfoFile() =>
            Path.Combine(Path.GetTempPath(), $"am_agecheck_{Guid.NewGuid():N}.txt");

        public static void AgeChecker_MissingFile_SetsRegenTrue()
        {
            // No last-run file -> age unknown -> must regenerate
            string path = TempInfoFile();
            try
            {
                new AgeChecker(forceMirrorRegen: false, lastRunInfoPath: path);
                Assert.True(AgeChecker.RegenMirror, "missing last-run file should set RegenMirror=true");
                Assert.True(File.Exists(path), "AgeChecker should create the last-run file when missing");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        public static void AgeChecker_StaleFile_SetsRegenTrue()
        {
            // Date in file is older than AgeThreshold (7 days) -> regen required
            string path = TempInfoFile();
            try
            {
                // Write a date 10 days in the past
                File.WriteAllText(path, DateTime.Now.AddDays(-10).ToString("yyyy-MM-dd HH:mm:ss"));
                new AgeChecker(forceMirrorRegen: false, lastRunInfoPath: path);
                Assert.True(AgeChecker.RegenMirror, "mirror older than threshold should set RegenMirror=true");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        public static void AgeChecker_FreshFile_ForceRegen_SetsRegenTrue()
        {
            // Date is recent but force regen is requested -> must regenerate
            string path = TempInfoFile();
            try
            {
                File.WriteAllText(path, DateTime.Now.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss"));
                new AgeChecker(forceMirrorRegen: true, lastRunInfoPath: path);
                Assert.True(AgeChecker.RegenMirror, "force regen on fresh mirror should set RegenMirror=true");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        public static void AgeChecker_MalformedDate_ThrowsFileLoadException()
        {
            // File contains non-parseable content -> should throw FileLoadException
            string path = TempInfoFile();
            try
            {
                File.WriteAllText(path, "not-a-date");
                bool threw = false;
                try { new AgeChecker(forceMirrorRegen: false, lastRunInfoPath: path); }
                catch (System.IO.FileLoadException) { threw = true; }
                Assert.True(threw, "malformed date in last-run file should throw FileLoadException");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        public static void AgeChecker_FreshFile_NoForce_SetsRegenFalse()
        {
            // Date is recent, no force -> no regen needed (placed last: explicitly sets false)
            string path = TempInfoFile();
            try
            {
                File.WriteAllText(path, DateTime.Now.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss"));
                new AgeChecker(forceMirrorRegen: false, lastRunInfoPath: path);
                Assert.True(!AgeChecker.RegenMirror, "fresh mirror with no force should set RegenMirror=false");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
