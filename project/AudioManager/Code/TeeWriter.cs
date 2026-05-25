using System;
using System.IO;
using System.Text;

namespace AudioManager
{
    /// <summary>
    /// A TextWriter that writes to multiple underlying writers simultaneously.
    /// Used to capture console output to file while still displaying it on screen.
    /// Supports tee'ing to console + file, or console + StringWriter for in-memory capture.
    /// </summary>
    internal class TeeWriter : TextWriter
    {
        private readonly TextWriter consoleWriter;
        private readonly TextWriter captureWriter;
        private readonly StreamWriter fileWriter;
        private readonly bool addFileTimestamps;
        private bool atLineStart = true;

        /// <summary>Tee to console + StringWriter (in-memory capture).</summary>
        public TeeWriter(TextWriter consoleWriter, StringWriter captureWriter)
        {
            this.consoleWriter = consoleWriter;
            this.captureWriter = captureWriter;
            this.fileWriter = null;
        }

        /// <summary>Tee to console + file + optional StringWriter.
        /// When addFileTimestamps is true, each line written to the file is prefixed with [HH:mm:ss].
        /// Console output is never timestamped.</summary>
        public TeeWriter(TextWriter consoleWriter, string logFilePath, StringWriter captureWriter = null, bool addFileTimestamps = false)
        {
            this.consoleWriter = consoleWriter;
            this.captureWriter = captureWriter;
            this.addFileTimestamps = addFileTimestamps;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));
                this.fileWriter = new StreamWriter(logFilePath, append: false, encoding: Encoding.UTF8) { AutoFlush = true };
            }
            catch
            {
                // If file creation fails, continue without file logging
                this.fileWriter = null;
            }
        }

        public TextWriter ConsoleWriter => consoleWriter;
        public string FilePath { get; set; }

        public override Encoding Encoding => consoleWriter.Encoding;

        public override void Write(char value)
        {
            consoleWriter.Write(value);
            captureWriter?.Write(value);
            if (fileWriter != null)
            {
                if (addFileTimestamps && atLineStart && value != '\r' && value != '\n')
                {
                    fileWriter.Write($"[{DateTime.Now:HH:mm:ss}] ");
                }
                fileWriter.Write(value);
                if (value == '\n') atLineStart = true;
                else if (value != '\r') atLineStart = false;
            }
        }

        public override void WriteLine(string value)
        {
            consoleWriter.WriteLine(value);
            captureWriter?.WriteLine(value);
            if (fileWriter != null)
            {
                string fileValue = value ?? "";
                // Only prefix if we're at the start of a line; mid-line WriteLine calls (e.g. Console.Write then Console.WriteLine) don't get a second timestamp
                if (addFileTimestamps && atLineStart)
                    fileWriter.WriteLine($"[{DateTime.Now:HH:mm:ss}] {fileValue}");
                else
                    fileWriter.WriteLine(fileValue);
                atLineStart = true;
            }
        }

        public override void WriteLine()
        {
            consoleWriter.WriteLine();
            captureWriter?.WriteLine();
            fileWriter?.WriteLine();
            atLineStart = true;
        }

        public override void Flush()
        {
            consoleWriter.Flush();
            captureWriter?.Flush();
            fileWriter?.Flush();
        }

        public void CloseLogFile()
        {
            Flush();
            fileWriter?.Dispose();
        }
    }
}
