using System;
using System.IO;

namespace ASCOM.PegasusFlatPanel
{
    /// <summary>
    /// Minimal trace log written next to the other ASCOM logs:
    /// Documents\ASCOM\Logs yyyy-MM-dd\ASCOM.PegasusFlatPanel.HHmmss.txt
    /// </summary>
    internal sealed class Log : IDisposable
    {
        private readonly object _lock = new object();
        private StreamWriter _writer;
        public bool Enabled { get; set; }

        public Log(bool enabled) { Enabled = enabled; }

        public void Write(string message)
        {
            if (!Enabled) return;
            lock (_lock)
            {
                try
                {
                    if (_writer == null)
                    {
                        string dir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "ASCOM", "Logs " + DateTime.Now.ToString("yyyy-MM-dd"));
                        Directory.CreateDirectory(dir);
                        string file = Path.Combine(dir, $"ASCOM.PegasusFlatPanel.{DateTime.Now:HHmmss.fff}.txt");
                        _writer = new StreamWriter(file, true) { AutoFlush = true };
                    }
                    _writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {message}");
                }
                catch { /* logging must never break the driver */ }
            }
        }

        public void Dispose()
        {
            lock (_lock) { _writer?.Dispose(); _writer = null; }
        }
    }
}
