using System;
using System.IO;

namespace AudioManager
{
    /// <summary>
    /// Checks how long ago the mirror was generated.
    /// </summary>
    internal class AgeChecker : Doer
    {
        /// <summary>
        /// True if the mirror should be regenerated
        /// </summary>
        public static bool RegenMirror { get; set; }

        // The path to the last run info file
        private static readonly string lastRunInfoFilePath = Program.MirrorRepoPath + "LastRunInfo.txt";

        // If the mirror is older than this amount of time, it is considered outdated
        private static readonly TimeSpan ageThreshold = TimeSpan.FromDays(7);

        /// <summary>
        /// Create an age checker
        /// </summary>
        /// <param name="forceMirrorRegen">Whether the mirror should be regenerated regardless of age</param>
        /// <exception cref="FileLoadException">If parsing date in last run file fails</exception>
        public AgeChecker(bool forceMirrorRegen)
        {
            // Notify
            Console.WriteLine($"\nChecking age of mirror...");

            // Get current date
            DateTime curDate = DateTime.Now;
            PrintDate("Now", curDate);

            // If the last run info file doesn't exist
            if (!File.Exists(lastRunInfoFilePath))
            {
                // Create with it the current date
                File.WriteAllText(lastRunInfoFilePath, GetStrFromDate(curDate));

                // Notify and regenerate
                Console.WriteLine(" - Mirror age is unknown, will regenerate!");
                RegenMirror = true;
                return;
            }

            //// Else if the last run file exists:
            // Try to parse date
            DateTime mirrorCreationDate;
            if (!DateTime.TryParse(File.ReadAllText(lastRunInfoFilePath), out mirrorCreationDate))
            {
                // If date parsing fails, notify and throw error
                string parseErr = "\nERROR: Cannot parse date in: " + lastRunInfoFilePath;
                throw new FileLoadException(parseErr);
            }

            // Print mirror creation date
            PrintDate("MirrorCreation", mirrorCreationDate);

            // Determine age of mirror and print
            TimeSpan mirrorAge = curDate.Subtract(mirrorCreationDate);
            PrintDate("MirrorAge", mirrorAge);

            // If mirror is outdated (i.e. mirror's age exceeds the threshold)
            if (mirrorAge > ageThreshold)
            {
                // Always regenerate
                Console.WriteLine(" - Mirror is outdated, will regenerate!");
                RegenMirror = true;
            }
            else
            {
                // Else if mirror not outdated
                string mirrMsg = " - Mirror was generated recently, ";

                // If force regen is on
                if (forceMirrorRegen)
                {
                    // Regenerate anyway
                    mirrMsg += "but 'force regeneration' is active! Will regenerate!";
                    RegenMirror = true;
                }
                else
                {
                    // Else do not regen
                    mirrMsg += "no regeneration needed!";
                    RegenMirror = false;
                }

                // Notify 
                Console.WriteLine(mirrMsg);
            }

            // If regeneration will occur, update last run info
            if (RegenMirror)
            {
                File.WriteAllText(lastRunInfoFilePath, GetStrFromDate(curDate));
            }

            // Finish and print time taken
            FinishAndPrintTimeTaken();
        }

        /// <param name="desc">A description of the date/time</param>
        /// <param name="date">The date/time object</param>
        private void PrintDate(string desc, object timeVal)
        {
            string paddedDesc = (" - DateTime." + desc).PadRight(27);
            Console.WriteLine($"{paddedDesc}   {GetStrFromDate(timeVal)}");
        }

        /// <summary>
        /// Format a DateTime or TimeSpan as a string.
        /// </summary>
        /// <param name="timeVal">The DateTime or TimeSpan object</param>
        /// <returns>The formatted string</returns>
        private string GetStrFromDate(object timeVal)
        {
            if (timeVal is DateTime)
            {
                DateTime dateTime = (DateTime)timeVal;
                return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else if (timeVal is TimeSpan)
            {
                TimeSpan ts = (TimeSpan)timeVal;
                return $"{ts.Days}d, {ts.Hours}h, {ts.Minutes}m, {ts.Seconds}s";
            }
            else
            {
                throw new ArgumentException("Unsupported type given to function");
            }
        }
    }
}
