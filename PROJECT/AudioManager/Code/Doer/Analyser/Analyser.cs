using AudioManager.Code.Modules;
using System;
using System.Linq;
using TagList = System.Collections.Generic.List<AudioManager.Code.Modules.TrackTag>;

namespace AudioManager
{
    /// <summary>
    /// Analyses audio track metadata to produce statistics.
    /// </summary>
    internal class Analyser : Doer
    {
        // Variables
        double artistStatsCutoff = 0.6;
        double yearStatsCutoff = 2.0;
        double decadeStatsCutoff = 0.0;

        /// <summary>
        /// Construct an audio tag analyser
        /// </summary>
        /// <param name="audioTags">The list of audio track tags</param>
        public Analyser(TagList audioTags)
        {
            // Notify
            Console.WriteLine("\nAnalysing tags...");

            // General statistics
            Console.WriteLine($"\n# General Statistics");
            double totalDuration = audioTags.Sum(tag => TimeSpan.Parse(tag.Length).TotalSeconds);
            double totalHours = Math.Round(totalDuration / 3600.0, 1);
            double days = Math.Round(totalHours / 24.0, 1);
            Console.WriteLine($" - Total playback hours: {totalHours} hours (≈{days} days non-stop)");
            TimeSpan avgLength = TimeSpan.FromSeconds(totalDuration / audioTags.Count);
            Console.WriteLine($" - Average song length: {(int)avgLength.TotalMinutes}m{avgLength.Seconds:D2}s");

            // ### CALCULATE STATS
            // Calculate basic stats
            StatList artistStats = new StatList("Artists", audioTags, tag => tag.Artists);
            StatList genreStats = new StatList("Genre", audioTags, tag => tag.Genres);
            StatList yearStats = new StatList("Year", audioTags, tag => tag.Year);

            // Calculate artist stats excluding Musivation tracks
            TagList tagsExclMusivation = audioTags.Where(tag => !tag.Genres.Contains("Musivation")).ToList();
            StatList artistStatsExclMusivation = new StatList("Artists", tagsExclMusivation, tag => tag.Artists);

            // Calculate decade stats 
            StatList decadeStats = new StatList("Decade", StatList.GetDecadeFreqDist(yearStats));

            // ### PRINT STATS
            // Print artist stats
            artistStatsExclMusivation.Print(artistStatsCutoff, "(Excluding Musivation)");
            artistStats.Print(artistStatsCutoff, "(All)");

            // Print genre stats
            genreStats.Print();

            // Print year and decade stats
            yearStats.Print(yearStatsCutoff);
            decadeStats.Print(decadeStatsCutoff);

            // Finish and print time taken
            Console.WriteLine("");
            FinishAndPrintTimeTaken();
        }
    }
}
