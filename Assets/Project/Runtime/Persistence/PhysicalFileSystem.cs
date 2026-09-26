using System;
using System.IO;

namespace Sokoban.Runtime.Persistence
{
    /// <summary>Uses same-directory atomic rename operations; never deletes a destination to replace it.</summary>
    public sealed class PhysicalFileSystem : IFileSystem
    {
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public bool FileExists(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.Directory) == 0;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }

        public Stream CreateNew(string path)
        {
            return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }

        public void Flush(Stream stream)
        {
            if (stream is FileStream file) file.Flush(true);
            else stream.Flush();
        }

        public byte[] ReadAllBytes(string path, int maxBytes)
        {
            if (maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > maxBytes) throw new InvalidDataException("文件超过读取字节上限。");
                using (var output = new MemoryStream((int)Math.Min(stream.Length, 81920)))
                {
                    var buffer = new byte[Math.Min(maxBytes, 81920)];
                    int count;
                    while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        if (output.Length + count > maxBytes) throw new InvalidDataException("文件超过读取字节上限。");
                        output.Write(buffer, 0, count);
                    }
                    return output.ToArray();
                }
            }
        }

        public string[] GetFiles(string directory, string pattern)
        {
            try { return Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly); }
            catch (DirectoryNotFoundException) { return Array.Empty<string>(); }
        }

        public void CopyNew(string source, string destination)
        {
            // Streaming preservation deliberately permits an oversized corrupt original.
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output, 81920);
                output.Flush(true);
            }
        }

        public void Move(string source, string destination)
        {
            File.Move(source, destination);
        }

        public void Replace(string source, string destination)
        {
            File.Replace(source, destination, null);
        }

        public void Delete(string path)
        {
            File.Delete(path);
        }
    }
}
