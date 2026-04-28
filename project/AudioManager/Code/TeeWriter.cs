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

        /// <summary>Tee to console + StringWriter (in-memory capture).</summary>
        public TeeWriter(TextWriter consoleWriter, StringWriter captureWriter)
        {
            this.consoleWriter = consoleWriter;
            this.captureWriter = captureWriter;
            this.fileWriter = null;
        }

        /// <summary>Tee to console + file + optional StringWriter.</summary>
        public TeeWriter(TextWriter consoleWriter, string logFilePath, StringWriter captureWriter = null)
        {
            this.consoleWriter = consoleWriter;
            this.captureWriter = captureWriter;

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
            fileWriter?.Write(value);
        }

        public override void WriteLine(string value)
        {
            consoleWriter.WriteLine(value);
            captureWriter?.WriteLine(value);
            fileWriter?.WriteLine(value);
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
