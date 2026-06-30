using System.Diagnostics;
using System.IO;

namespace AudioManager
{
    /// <summary>
    /// Tests for AudioMirrorCommitter.GetSkipReason (pure gate logic) and RunGit (subprocess).
    /// RunGit tests use a temp git repo to exercise the actual subprocess code path and verify
    /// stdout/stderr are both captured without deadlock.
    /// </summary>
    internal static class AudioMirrorCommitterTests
    {
        private static string CreateTempGitRepo()
        {
            string dir = Path.Combine(Path.GetTempPath(), "am_gittest_" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            // Bootstrap without RunGit so tests don't depend on the method under test
            RunProcess("git", "init", dir);
            RunProcess("git", "config user.email test@test.com", dir);
            RunProcess("git", "config user.name Test", dir);
            return dir;
        }

        private static void RunProcess(string exe, string args, string workingDir)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using (var p = Process.Start(psi))
            {
                p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                p.WaitForExit();
            }
        }

        public static void GetSkipReason_IncrementalTrigger_ReturnsIncremental()
        {
            // Incremental analysis may have stale XMLs - must skip regardless of clean state
            string reason = AudioMirrorCommitter.GetSkipReason(libCheckerClean: true, trigger: CommitTrigger.AnalysisIncremental);
            Assert.Equal("incremental", reason, "incremental trigger should always return skip reason");
        }

        public static void GetSkipReason_IncrementalDirty_ReturnsIncremental()
        {
            // Incremental + dirty -> incremental check fires first
            string reason = AudioMirrorCommitter.GetSkipReason(libCheckerClean: false, trigger: CommitTrigger.AnalysisIncremental);
            Assert.Equal("incremental", reason, "incremental fires before dirty check");
        }

        public static void GetSkipReason_DirtyForceRegen_ReturnsDirty()
        {
            // Force regen is reliable, but dirty library must prevent commit
            string reason = AudioMirrorCommitter.GetSkipReason(libCheckerClean: false, trigger: CommitTrigger.AnalysisForceRegen);
            Assert.Equal("dirty", reason, "dirty LibChecker should block commit on force regen");
        }

        public static void GetSkipReason_DirtyIntegration_ReturnsDirty()
        {
            // Integration is reliable, but dirty library must prevent commit
            string reason = AudioMirrorCommitter.GetSkipReason(libCheckerClean: false, trigger: CommitTrigger.Integration);
            Assert.Equal("dirty", reason, "dirty LibChecker should block commit on integration");
        }

        public static void GetSkipReason_CleanForceRegen_ReturnsNull()
        {
            // Clean + force regen: no gate applies -> should proceed to git
            string reason = AudioMirrorCommitter.GetSkipReason(libCheckerClean: true, trigger: CommitTrigger.AnalysisForceRegen);
            Assert.True(reason == null, "clean force regen has no skip reason (would proceed to git)");
        }

        public static void GetSkipReason_CleanIntegration_ReturnsNull()
        {
            // Clean + integration: no gate applies -> should proceed to git
            string reason = AudioMirrorCommitter.GetSkipReason(libCheckerClean: true, trigger: CommitTrigger.Integration);
            Assert.True(reason == null, "clean integration has no skip reason (would proceed to git)");
        }

        // RunGit subprocess tests - verify stdout and stderr are both captured without deadlock

        public static void RunGit_CapturesStdoutFromStatusCommand()
        {
            string dir = CreateTempGitRepo();
            try
            {
                string result = AudioMirrorCommitter.RunGit(dir, "status --porcelain");
                // Fresh repo with no changes - result is empty string (clean) or ?? entries
                Assert.True(result != null, "RunGit should return a non-null string");
            }
            finally { try { Directory.Delete(dir, recursive: true); } catch { } }
        }

        public static void RunGit_CapturesStderrFromFailedCommand()
        {
            // rev-parse on a nonexistent ref exits non-zero and writes to stderr.
            // With the sequential ReadToEnd bug, large stderr output causes a deadlock.
            // This test verifies stderr is captured (not swallowed or hung) for failed commands.
            string dir = CreateTempGitRepo();
            try
            {
                string result = AudioMirrorCommitter.RunGit(dir, "rev-parse --verify nonexistent-ref-abc123");
                Assert.True(result != null && result.Length > 0, "stderr from failed git command should be returned, not swallowed");
            }
            finally { try { Directory.Delete(dir, recursive: true); } catch { } }
        }

        public static void RunGit_InvalidWorkingDirectory_ReturnsErrorMessage()
        {
            string result = AudioMirrorCommitter.RunGit(@"C:\nonexistent\path\abc123", "status");
            Assert.True(result != null && result.StartsWith("error:"), "invalid working dir should return error: message");
        }
    }
}
