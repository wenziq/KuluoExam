using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Sokoban.Runtime.Persistence
{
    /// <summary>Fixed filenames only. The active log counts toward retainedFiles.</summary>
    public sealed class DiagnosticLog
    {
        private static readonly object WriteGate = new object();
        private readonly string directory;
        private readonly int maxBytes;
        private readonly int retainedFiles;

        public DiagnosticLog(UserDataPaths paths, int maxBytes = 65536, int retainedFiles = 3)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            if (maxBytes < 64) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            if (retainedFiles < 1 || retainedFiles > 16) throw new ArgumentOutOfRangeException(nameof(retainedFiles));
            directory = paths.LogDirectory;
            this.maxBytes = maxBytes;
            this.retainedFiles = retainedFiles;
        }

        public void Write(string message)
        {
            string text = message ?? "";
            if (text.Length > maxBytes) text = text.Substring(0, maxBytes);
            text = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " " + text.Replace('\r', ' ').Replace('\n', ' ');
            byte[] bytes = new byte[maxBytes];
            Encoding.UTF8.GetEncoder().Convert(text.ToCharArray(), 0, text.Length, bytes, 0, maxBytes - 1, true,
                out _, out int count, out _);
            bytes[count++] = (byte)'\n';
            lock (WriteGate)
            {
                Directory.CreateDirectory(directory);
                string active = FileAt(0);
                if (File.Exists(active) && new FileInfo(active).Length + count > maxBytes)
                {
                    File.Delete(FileAt(retainedFiles - 1));
                    for (int index = retainedFiles - 2; index >= 0; index--)
                    {
                        string source = FileAt(index);
                        if (File.Exists(source)) File.Move(source, FileAt(index + 1));
                    }
                }
                using (var stream = new FileStream(active, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(bytes, 0, count);
                    stream.Flush();
                }
            }
        }

        private string FileAt(int index)
        {
            return Path.Combine(directory, index == 0 ? "diagnostic.log" : "diagnostic." + index.ToString(CultureInfo.InvariantCulture) + ".log");
        }
    }
}
