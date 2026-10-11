namespace AudioManager
{
    /// <summary>
    /// Pins the exact console line formats the Python GUI parses (gui/tabs/integration.py
    /// _update_exec_status, gui/routing.py execution_record_path). If one of these fails, the
    /// GUI parsers and gui/tests/test_integration.py must change in the same commit.
    /// </summary>
    internal static class MusicIntegratorConsoleFormatTests
    {
        public static void AutoLine_IsArtistsDashTitleThenAlbumSuffix()
        {
            Assert.Equal("[AUTO] A; B - Song [Album]", MusicIntegrator.FormatAutoLine("A; B", "Song", " [Album]"), "moved line format");
            Assert.Equal("[AUTO] A - Song", MusicIntegrator.FormatAutoLine("A", "Song", ""), "no album suffix");
        }

        public static void SkipLine_IsArtistsDashTitleThenReason()
        {
            Assert.Equal("[SKIP] A - Song: already exists at destination", MusicIntegrator.FormatSkipLine("A", "Song"), "skip line format");
        }

        public static void ExecutionJsonLine_CarriesMarkerAndPath()
        {
            Assert.Equal("\n  EXECUTION JSON: C:/logs/execution-20260101-000000.json",
                MusicIntegrator.FormatExecutionJsonLine("C:/logs/execution-20260101-000000.json"), "marker line format");
        }
    }
}
