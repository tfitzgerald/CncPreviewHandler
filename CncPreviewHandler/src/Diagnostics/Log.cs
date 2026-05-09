using System;
using System.IO;
using System.Threading;

namespace CncPreviewHandler.Diagnostics
{
    /// <summary>
    /// Lightweight thread-safe logger. Writes to %APPDATA%\CncPreviewHandler\log.txt.
    /// Rotates the file at 256 KB. Never throws — logging failure must not crash
    /// the preview handler.
    /// </summary>
    internal static class Diag
    {
        private static readonly object Sync = new object();
        private static string _path;
        private const long MaxBytes = 256 * 1024;

        static Log()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "CncPreviewHandler");
                Directory.CreateDirectory(dir);
                _path = Path.Combine(dir, "log.txt");
            }
            catch { _path = null; }
        }

        public static string LogPath => _path;

        public static void Info (string msg)         => Write("INFO ", msg, null);
        public static void Warn (string msg)         => Write("WARN ", msg, null);
        public static void Error(string msg, Exception ex = null) => Write("ERROR", msg, ex);

        private static void Write(string level, string msg, Exception ex)
        {
            if (_path == null) return;
            try
            {
                lock (Sync)
                {
                    Rotate();
                    using (var sw = new StreamWriter(_path, append: true))
                    {
                        sw.WriteLine("{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] [pid {2} tid {3}] {4}",
                            DateTime.Now, level,
                            System.Diagnostics.Process.GetCurrentProcess().Id,
                            Thread.CurrentThread.ManagedThreadId,
                            msg);
                        if (ex != null)
                        {
                            sw.WriteLine("    {0}: {1}", ex.GetType().Name, ex.Message);
                            sw.WriteLine(IndentLines(ex.StackTrace ?? "(no stack)", "    "));
                            var inner = ex.InnerException;
                            int depth = 1;
                            while (inner != null && depth < 5)
                            {
                                sw.WriteLine("    Inner[{0}]: {1}: {2}",
                                    depth, inner.GetType().Name, inner.Message);
                                inner = inner.InnerException;
                                depth++;
                            }
                        }
                    }
                }
            }
            catch { /* never throw from logger */ }
        }

        private static void Rotate()
        {
            try
            {
                var fi = new FileInfo(_path);
                if (!fi.Exists || fi.Length < MaxBytes) return;
                var old = _path + ".old";
                if (File.Exists(old)) File.Delete(old);
                File.Move(_path, old);
            }
            catch { }
        }

        private static string IndentLines(string s, string indent)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return indent + s.Replace("\n", "\n" + indent);
        }
    }
}
