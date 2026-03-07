using System;
using System.IO;

namespace AudioManager
{
    internal class ReportWriter : Doer
    {
        public ReportWriter(string reportContent)
        {
            DateTime now = DateTime.Now;
            string year = now.Year.ToString();
            string yearFolder = Path.Combine(Constants.ReportsPath, year);
            string filename = now.ToString("yyyy-MM-dd") + " - AudioReport.txt";
            string fullPath = Path.Combine(yearFolder, filename);

            Directory.CreateDirectory(yearFolder);
            File.WriteAllText(fullPath, reportContent);

            Console.WriteLine($" - Report saved: REPORTS\\{year}\\{filename}");

            FinishAndPrintTimeTaken();
        }
    }
}
