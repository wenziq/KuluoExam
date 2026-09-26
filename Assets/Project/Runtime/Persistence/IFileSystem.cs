using System;
using System.IO;

namespace Sokoban.Runtime.Persistence
{
    public interface IFileSystem
    {
        void CreateDirectory(string path);
        bool FileExists(string path);
        Stream CreateNew(string path);
        void Flush(Stream stream);
        byte[] ReadAllBytes(string path, int maxBytes);
        string[] GetFiles(string directory, string pattern);
        void CopyNew(string source, string destination);
        void Move(string source, string destination);
        void Replace(string source, string destination);
        void Delete(string path);
    }
}
