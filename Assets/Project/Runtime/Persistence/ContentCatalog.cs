using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
using Sokoban.Core.Validation;
namespace Sokoban.Runtime.Persistence
{
    public enum ContentSource { BuiltIn, Installed, Draft }
    public sealed class CatalogEntry
    {
        private readonly PackData snapshot;
        public CatalogEntry(PackData pack, ContentSource source) { snapshot = pack.DeepCopy(); Source = source; }
        public PackData Pack => snapshot.DeepCopy();
        public string PackId => snapshot.packId;
        public ContentSource Source { get; }
        public bool IsDraft => Source == ContentSource.Draft;
        public string CategoryLabel => Source == ContentSource.BuiltIn ? "内置示例" : IsDraft ? "制作草稿" : "可玩关卡集";
    }
    public sealed class ContentCatalog
    {
        private readonly List<CatalogEntry> entries = new List<CatalogEntry>();
        private readonly List<string> errors = new List<string>();
        public IReadOnlyList<CatalogEntry> Entries => entries.AsReadOnly();
        public IReadOnlyList<string> Errors => errors.AsReadOnly();
        public void Load(string builtInDirectory, UserDataPaths paths, IFileSystem files = null)
        {
            files = files ?? new PhysicalFileSystem();
            entries.Clear();
            errors.Clear();
            Read(builtInDirectory, "*.sokopack.json", ContentSource.BuiltIn);
            Read(paths.AreaDirectory(StorageArea.Installed), UserDataPaths.PrimaryPattern, ContentSource.Installed);
            Read(paths.AreaDirectory(StorageArea.Drafts), UserDataPaths.PrimaryPattern, ContentSource.Draft);
            void Read(string directory, string pattern, ContentSource source)
            {
                string[] names;
                try { names = files.GetFiles(directory, pattern); }
                catch (DirectoryNotFoundException) { return; }
                catch (Exception ex) { errors.Add("无法读取关卡集目录：" + ex.Message); return; }
                var acceptedIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (string file in names.OrderBy(x => x, StringComparer.Ordinal))
                {
                    try
                    {
                        var pack = StrictPackJson.Parse(files.ReadAllBytes(file, 8388608));
                        if (source == ContentSource.Draft)
                        {
                            if (pack.documentKind != DocumentKind.DraftPack) throw new FormatException("草稿目录包含非草稿包。");
                        }
                        else ValidatePlayable(pack);
                        if (!acceptedIds.Add(pack.packId))
                            throw new FormatException("重复的关卡集身份。");
                        entries.Add(new CatalogEntry(pack, source));
                    }
                    catch (Exception ex) { errors.Add(Path.GetFileName(file) + "：" + ex.Message + "（原文件保留）"); }
                }
            }
        }
        public static void ValidatePlayable(PackData pack,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();
            if (pack.documentKind != DocumentKind.PlayablePack || !StructureValidator.Validate(pack).IsValid)
                throw new FormatException("关卡包尚不满足可玩结构。");
            foreach (var level in pack.levels)
            {
                token.ThrowIfCancellationRequested();
                var proof = pack.solutionWitnesses.Find(w => w.levelId == level.levelId && WitnessVerifier.Verify(level, w,cancellationToken:token).IsValid);
                token.ThrowIfCancellationRequested();
                if (proof == null) throw new FormatException("关卡缺少当前可重放的通关证据：" + level.name);
            }
        }
    }
}
