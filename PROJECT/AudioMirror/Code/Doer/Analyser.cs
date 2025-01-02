using AudioMirror.Code.Modules;
using System;
using System.Collections.Generic;

namespace AudioMirror
{
    /// <summary>
    /// Analyses audio track metadata to produce statistics.
    /// </summary>
    internal class Analyser : Doer
    {
        // Variables
        double artistStatsCutoff = 0.5;
        double yearStatsCutoff = 2.0;

        /// <summary>
        /// Construct an audio tag analyser
        /// </summary>
        /// <param name="audioTags"></param>
        public Analyser(List<TrackTag> audioTags)
        {
            // Notify
            Console.WriteLine("\nAnalysing tags...");

            // Calculate stats
            StatList artistStats = new StatList("Artists", audioTags, tag => tag.Artists);
            StatList genreStats = new StatList("Genre", audioTags, tag => tag.Genres);
            StatList yearStats = new StatList("Year", audioTags, tag => tag.Year);

            // Print stats
            artistStats.Print(artistStatsCutoff);
            genreStats.Print();
            yearStats.Print(yearStatsCutoff);

            /// SPECIAL STATS - TODO
            //// Print artist stats excluding Musivation artists
            //PrintArtistStatsExcludingMusivation("Artists (Musivation Filtered Out)", audioTags, artistFreqDist, artistStatsCutoff);

            //// Print decade/time period stats
            //PrintDecadeStats("Decade", yearFreqDist);

            // Print time taken
            Console.WriteLine("");
            PrintTimeTaken();
        }

        ///// <summary>
        ///// Print artists statistics but exclude Musivation tracks
        ///// </summary>
        //private void PrintArtistStatsExcludingMusivation(string statName, List<TrackTag> audioTags, 
        //    StringIntFreqDist artistFreqDist, double artistStatsCutoff)
        //{
        //    // Print heading and columns
        //    PrintHeading(statName);
        //    PrintColumns("%", statName, "Occurrences");

        //    // Filter freq dist down to artists who don't have musivation tracks
        //    var filteredArtistFreqDist = artistFreqDist
        //        .Where(pair => 
        //        !audioTags.Any(tag => tag.Artists.Contains(pair.Key) && tag.Genres.Contains("Musivation")));

        //    // Get total number of items (i.e. sum of occurrences)
        //    int totalItems = filteredArtistFreqDist.Sum(pair => pair.Value);

        //    // For each item
        //    foreach (var item in filteredArtistFreqDist)
        //    {
        //        // Extract info
        //        var itemValue = item.Key.ToString();
        //        var count = item.Value;
        //        var percentage = ((double)count / totalItems) * 100;

        //        // Print statistics line
        //        PrintStatsLine(percentage, itemValue, count, artistStatsCutoff);
        //    }
        //}

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
