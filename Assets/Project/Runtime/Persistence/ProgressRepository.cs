using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sokoban.Domain.Gameplay;
namespace Sokoban.Runtime.Persistence
{
    public sealed class ProgressLoadResult
    {
        public ProgressSnapshot Snapshot { get; internal set; }
        public string Error { get; internal set; }
        public bool Succeeded => Error == null;
    }
    public sealed class ProgressRepository
    {
        private const string DocumentId = "player-progress-v1";
        private readonly TransactionalFileStore store;
        private readonly UserDataPaths paths;
        private readonly IFileSystem files;
        public ProgressRepository(UserDataPaths paths, IFileSystem files = null)
        {
            this.paths = paths;
            this.files = files ?? new PhysicalFileSystem();
            store = new TransactionalFileStore(paths, this.files);
        }
        public ProgressLoadResult Load()
        {
            try
            {
                string path = paths.Primary(StorageArea.Progress, DocumentId);
                return new ProgressLoadResult { Snapshot = files.FileExists(path) ? Decode(files.ReadAllBytes(path, 8388608)) : new ProgressSnapshot() };
            }
            catch (Exception ex)
            {
                return new ProgressLoadResult { Error = "无法读取正式进度。原文件已保留：" + ex.Message };
            }
        }
        public Task<TransactionResult> SaveAsync(ProgressSnapshot snapshot, CancellationToken token = default)
        {
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(JsonConvert.SerializeObject(snapshot));
            return store.SaveAsync(StorageArea.Progress, DocumentId, bytes, b => Decode(b), token);
        }
        private static ProgressSnapshot Decode(byte[] bytes)
        {
            if (bytes.Length > 8388608) throw new FormatException("进度文件过大。");
            using (var reader = new JsonTextReader(new StringReader(new UTF8Encoding(false, true).GetString(bytes))))
            {
                reader.MaxDepth = 12;
                reader.DateParseHandling = DateParseHandling.None;
                var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new FormatException("进度含尾随数据。");
                Fields(root, "version", "recentPackId", "records");
                if (Integer(root, "version") != 1) throw new FormatException("未知进度版本。");
                var snapshot = new ProgressSnapshot { recentPackId = Text(root, "recentPackId", 64) };
                if (!(root["records"] is JArray records) || records.Count > 3000) throw new FormatException("进度记录数量无效。");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var token in records)
                {
                    if (!(token is JObject row)) throw new FormatException("进度记录格式无效。");
                    Fields(row, "packId", "levelId", "fingerprint", "path", "moves", "pushes");
                    var record = new CompletionRecord
                    {
                        packId = Text(row, "packId", 64), levelId = Text(row, "levelId", 64),
                        fingerprint = Text(row, "fingerprint", 64), path = Text(row, "path", 100000),
                        moves = Integer(row, "moves"), pushes = Integer(row, "pushes")
                    };
                    if (record.packId.Length == 0 || record.levelId.Length == 0 || record.fingerprint.Length != 64 || record.moves != record.path.Length || record.pushes < 0 || record.pushes > record.moves)
                        throw new FormatException("进度身份或计数无效。");
                    foreach (char step in record.path) if (step != 'U' && step != 'D' && step != 'L' && step != 'R') throw new FormatException("进度路线无效。");
                    if (!seen.Add(record.packId + "\n" + record.levelId + "\n" + record.fingerprint)) throw new FormatException("重复进度记录。");
                    snapshot.records.Add(record);
                }
                return snapshot;
            }
        }
        private static void Fields(JObject row, params string[] names)
        {
            var expected = new HashSet<string>(names, StringComparer.Ordinal);
            foreach (var property in row.Properties()) if (!expected.Remove(property.Name)) throw new FormatException("未知进度字段。");
            if (expected.Count != 0) throw new FormatException("缺失进度字段。");
        }
        private static string Text(JObject row, string name, int maximum)
        {
            var token = row[name];
            if (token?.Type != JTokenType.String || token.Value<string>().Length > maximum) throw new FormatException("进度文本无效。");
            return token.Value<string>();
        }
        private static int Integer(JObject row, string name)
        {
            if (row[name]?.Type != JTokenType.Integer) throw new FormatException("进度整数无效。");
            long value = row[name].Value<long>();
            if (value < 0 || value > 100000) throw new FormatException("进度整数越界。");
            return (int)value;
        }
    }
}
