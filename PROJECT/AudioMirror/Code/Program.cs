using System;
using System.IO;
using File = System.IO.File;

namespace AudioMirror
{
    internal class Program
    {
        //// CONSTANTS/SETTINGS

        // The path back to the project folder
        static string projectPath = "..\\..\\..\\";

        // The path to the mirror folder relative to program executable
        static string relMirrorPath = projectPath + "..\\AUDIO_MIRROR";

        // The path to the last run info file
        static string lastRunInfoFilePath = projectPath + "LastRunInfo.txt";


        /// <summary>
        /// Main function
        /// </summary>
        /// <param name="args">Arguments given to program</param>
        static void Main(string[] args)
        {
            // Start message
            Console.WriteLine("\n###### Audio Mirror ######");

            // Print date
            DateTime curDate = DateTime.Now;
            string curDateStr = curDate.ToString("yyyy-MM-dd HH:mm:ss");
            Console.WriteLine("\nDateTime.Now: " + curDateStr);

            // Re-create the mirror if it is outdated
            bool recreateMirror = CheckDate(curDate, curDateStr);

            // Set mirror path relative to program executable
            string programDir = AppDomain.CurrentDomain.BaseDirectory;
            string mirrorPath = Path.GetFullPath(Path.Combine(programDir, relMirrorPath));

            // 1) Create mirror of audio folder
            Reflector r = new Reflector(mirrorPath, recreateMirror);

            // 2) Parse metadata into XML files and tag list
            Parser p = new Parser(mirrorPath);

            // 3) Analyse metadata and print statistics
            Analyser a = new Analyser(p.audioTags);

            // Print total time
            TimeSpan totalTime = r.ExecutionTime + p.ExecutionTime + a.ExecutionTime;
            Console.WriteLine("\n\nTotal time taken: " + Doer.ConvertTimeSpanToString(totalTime));

            // Finish message
            Console.WriteLine("\nFinished!\n");
        }


        /// <summary>
        /// Check if the mirror was generated over a week ago. If so, schedule a regeneration.
        /// </summary>
        /// <param name="curDate">The current date object</param>
        /// <param name="curDateStr">The current date as a string</param>
        /// <returns>True if the mirror should be regenerated</returns>
        static bool CheckDate(DateTime curDate, string curDateStr)
        {
            // If the last run info file doesn't exist
            if (!File.Exists(lastRunInfoFilePath))
            {
                // Create with it the current date
                File.WriteAllText(lastRunInfoFilePath, curDateStr);

                // Regenerate the mirror
                return true;
            }

            // Else if the file exists and able to parse date
            DateTime lastRunDate;
            if (DateTime.TryParse(File.ReadAllText(lastRunInfoFilePath), out lastRunDate))
            {
                // If the mirror was created over 7 days ago, regenerate it
                bool regenerate = curDate.Subtract(lastRunDate).Days > 7;

                // If mirror will be regenerated
                if (regenerate)
                {
                    // Notify and update date in file
                    Console.WriteLine("Mirror is outdated, will regenerate!");
                    File.WriteAllText(lastRunInfoFilePath, curDateStr);
                }
                else
                {
                    // Else if not, notify
                    Console.WriteLine("Mirror was created recently, no regeneration needed!");
                }

                return regenerate;
            }
            else
            {
                // Else if cannot parse date
                string parseErr = "\nERROR: Cannot parse date in: " + lastRunInfoFilePath;
                throw new FileLoadException(parseErr);
            }
        }
    }
}