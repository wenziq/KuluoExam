using System;
using System.IO;
using System.Text;
using Sokoban.Core.Validation;

namespace Sokoban.Runtime.Persistence
{
    public enum StorageArea { Drafts, Recovery, Installed, Staging, Analysis, Progress, Settings }

    public sealed class UserDataPaths
    {
        public string Root { get; }
        public const string PrimaryPattern = "doc-*.json";
        public string LogDirectory => Path.Combine(Root, "Logs");

        public UserDataPaths(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("数据根目录缺失。", nameof(root));
            Root = Path.GetFullPath(root);
        }

        public string AreaDirectory(StorageArea area)
        {
            return Path.Combine(Root, AreaName(area));
        }

        public string Primary(StorageArea area, string id)
        {
            return Path.Combine(AreaDirectory(area), FileName(id));
        }

        public string Backup(StorageArea area, string id)
        {
            return Path.Combine(Root, "Backups", AreaName(area), FileName(id));
        }

        internal string Temporary(string destination)
        {
            return destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        }

        internal string PreservedOriginal(StorageArea area, string id)
        {
            return Backup(area, id) + "." + Guid.NewGuid().ToString("N") + ".corrupt";
        }

        private static string AreaName(StorageArea area)
        {
            if (!Enum.IsDefined(typeof(StorageArea), area)) throw new ArgumentOutOfRangeException(nameof(area));
            return area.ToString();
        }

        private static string FileName(string id)
        {
            if (!PackValidator.IsId(id)) throw new ArgumentException("内部 ID 格式无效。", nameof(id));
            // Hex preserves case-sensitive protocol identities on case-insensitive filesystems.
            // The fixed prefix also avoids Windows reserved device names such as CON.
            var encoded = new StringBuilder("doc-");
            foreach (char value in id) encoded.Append(((int)value).ToString("x2"));
            return encoded.Append(".json").ToString();
        }
    }
}
