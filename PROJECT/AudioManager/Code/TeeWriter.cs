using System;
using System.IO;
using System.Text;

namespace AudioManager
{
    /// <summary>
    /// A TextWriter that writes to two underlying writers simultaneously.
    /// Used to capture console output while still displaying it on screen.
    /// </summary>
    internal class TeeWriter : TextWriter
    {
        private readonly TextWriter consoleWriter;
        private readonly StringWriter captureWriter;

        public TeeWriter(TextWriter consoleWriter, StringWriter captureWriter)
        {
            this.consoleWriter = consoleWriter;
            this.captureWriter = captureWriter;
        }

        public TextWriter ConsoleWriter => consoleWriter;

        public override Encoding Encoding => consoleWriter.Encoding;

        public override void Write(char value)
        {
            consoleWriter.Write(value);
            captureWriter.Write(value);
        }

        public override void WriteLine(string value)
        {
            consoleWriter.WriteLine(value);
            captureWriter.WriteLine(value);
        }

        public override void Flush()
        {
            consoleWriter.Flush();
            captureWriter.Flush();
        }
    }
}
