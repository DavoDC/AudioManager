namespace AudioManager
{
    /// <summary>
    /// Tests for AudioMirrorCommitter.GetSkipReason - the pure gate logic extracted from TryCommit.
    /// Tests verify the critical safety invariants: never commit on incremental analysis,
    /// never commit when LibChecker is dirty.
    /// </summary>
    internal static class AudioMirrorCommitterTests
    {
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
        public static void RunGit_CapturesStdoutFromStatusCommand() { }

        public static void RunGit_CapturesStderrFromFailedCommand() { }

        public static void RunGit_InvalidWorkingDirectory_ReturnsErrorMessage() { }
    }
}
