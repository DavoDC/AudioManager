using AudioMirror.Code.Modules;
using System;
using System.Linq;
using TagList = System.Collections.Generic.List<AudioMirror.Code.Modules.TrackTag>;

namespace AudioMirror
{
    /// <summary>
    /// Analyses audio track metadata to produce statistics.
    /// </summary>
    internal class Analyser : Doer
    {
        // Variables
        double artistStatsCutoff = 0.6;
        double yearStatsCutoff = 2.0;

        /// <summary>
        /// Construct an audio tag analyser
        /// </summary>
        /// <param name="audioTags">The list of audio track tags</param>
        public Analyser(TagList audioTags)
        {
            // Notify
            Console.WriteLine("\nAnalysing tags...");

            // ### CALCULATE STATS
            // Calculate basic stats
            StatList artistStats = new StatList("Artists", audioTags, tag => tag.Artists);
            StatList genreStats = new StatList("Genre", audioTags, tag => tag.Genres);
            StatList yearStats = new StatList("Year", audioTags, tag => tag.Year);

            // Calculate artist stats excluding Musivation tracks
            TagList tagsExclMusivation = audioTags.Where(tag => !tag.Genres.Contains("Musivation")).ToList();
            StatList artistStatsExclMusivation = new StatList("Artists", tagsExclMusivation, tag => tag.Artists);

            // ### PRINT STATS
            // Print artist stats
            artistStatsExclMusivation.Print(artistStatsCutoff, "(Excluding Musivation)");
            artistStats.Print(artistStatsCutoff, "(All)");

            // Print genre stats
            genreStats.Print();

            // Print year and decade stats
            yearStats.Print(yearStatsCutoff);
            //PrintDecadeStats("Decade", yearFreqDist);

            // Print time taken
            Console.WriteLine("");
            PrintTimeTaken();
        }

        ///// <summary>
        ///// Print statistics on how many tracks are in each time/decade period
        ///// </summary>
        //private void PrintDecadeStats(string statName, StringIntFreqDist yearFreqDist)
        //{
        //    // Print heading and columns
        //    PrintHeading(statName);
        //    PrintColumns("%", statName, "Occurrences");

        //    // Group counts by decade
        //    var decadeDict = yearFreqDist
        //        .GroupBy(yearPair => GetDecade(yearPair.Key.ToString()))
        //        .ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value));

        //    // Sort decades and calculate total occurrences
        //    var sortedDecades = decadeDict.OrderByDescending(pair => pair.Value);
        //    int totalItems = sortedDecades.Sum(pair => pair.Value);

        //    // Print stats for each decade
        //    foreach (var decadePair in sortedDecades)
        //    {
        //        var decade = decadePair.Key;
        //        var count = decadePair.Value;
        //        double percentage = (double)count / totalItems * 100;
        //        PrintStatsLine(percentage, $"{decade}s", count, 0);
        //    }
        //}

        ///// <summary>
        ///// Calculates the starting year of the decade for a given year.
        ///// </summary>
        ///// <param name="year">The year as a string, or "Missing" if the track didn't have it.</param>
        ///// <returns>The starting year of the decade (e.g., 1990 for 1995).</returns>
        //private int GetDecade(string year)
        //{
        //    int yearNum = 0;
        //    if (int.TryParse(year, out yearNum))
        //    {
        //        return (yearNum / 10) * 10;
        //    }
        //    else
        //    {
        //        string errMsg = $"######### ERROR: Cannot parse year string: '{year}'";
        //        Console.WriteLine(errMsg);
        //        return 0;
        //    }
        //}
    }
}
