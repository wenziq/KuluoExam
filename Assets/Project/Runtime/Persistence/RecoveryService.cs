using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sokoban.Core.Data;
using Sokoban.Core.Validation;
using Sokoban.Domain.Workshop;
namespace Sokoban.Runtime.Persistence
{
    public sealed class RecoverySnapshot
    {
        public PackData Pack { get; internal set; }
        public string BaseSavedHash { get; internal set; }
        public DateTime SavedUtc { get; internal set; }
        public WorkshopDocument Restore()
        {
            var document=new WorkshopDocument(Pack);
            if(BaseSavedHash==null)document.MarkUnsaved();else document.MarkSaved(BaseSavedHash);
            return document;
        }
    }
    public sealed class RecoveryItem
    {
        public string Id { get; internal set; }
        public RecoverySnapshot Snapshot { get; internal set; }
        public string Error { get; internal set; }
    }
    public sealed class RecoveryService
    {
        private readonly TransactionalFileStore store;
        private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        public RecoveryService(UserDataPaths paths,IFileSystem files=null) { store=new TransactionalFileStore(paths,files); }
        public Task<TransactionResult> SaveAsync(WorkshopDocument document)
        {
            if(document.HasActiveStroke)return Task.FromResult(new TransactionResult{Status=TransactionStatus.Cancelled});
            try
            {
                var snapshot=document.Snapshot();string id=snapshot.packId;
                var root=new JObject{["version"]=1,["baseSavedHash"]=document.SavedHash==null?JValue.CreateNull():new JValue(document.SavedHash),["savedUtcTicks"]=DateTime.UtcNow.Ticks,["packJson"]=StrictPackJson.SerializeToString(snapshot)};
                byte[] bytes=Utf8.GetBytes(root.ToString(Formatting.None));
                return store.SaveAsync(StorageArea.Recovery,id,bytes,b=>Decode(b,id));
            }
            catch(Exception ex){return Task.FromResult(new TransactionResult{Status=TransactionStatus.Failed,Error=ex});}
        }
        public RecoverySnapshot Load(string id)
        {
            try{return Decode(store.Files.ReadAllBytes(store.Paths.Primary(StorageArea.Recovery,id),store.MaxBytes),id);}
            catch(FileNotFoundException){return null;}
            catch(DirectoryNotFoundException){return null;}
        }
        public Task DeleteAsync(string id)
        {
            string primary=store.Paths.Primary(StorageArea.Recovery,id);
            return store.Enqueue(primary,()=>
            {
                // Delete backup first: a failed second delete still leaves a discoverable current recovery.
                DeleteIfPresent(store.Paths.Backup(StorageArea.Recovery,id));DeleteIfPresent(primary);return true;
            });
        }
        private void DeleteIfPresent(string path)
        { try{store.Files.Delete(path);}catch(FileNotFoundException){}catch(DirectoryNotFoundException){} }
        public Task<RecoveryReport> InspectAsync(string id)=>new StorageRecovery(store).InspectAsync(StorageArea.Recovery,id,b=>Decode(b,id));
        public Task<TransactionResult> RecoverBackupAsync(string id)=>new StorageRecovery(store).RecoverBackupAsync(StorageArea.Recovery,id,b=>Decode(b,id));
        public IReadOnlyList<RecoveryItem> Enumerate()
        {
            string[] files;
            try{files=store.Files.GetFiles(store.Paths.AreaDirectory(StorageArea.Recovery),UserDataPaths.PrimaryPattern);}
            catch(DirectoryNotFoundException){return Array.Empty<RecoveryItem>();}
            var items=new List<RecoveryItem>();
            foreach(string file in files.OrderBy(f=>f,StringComparer.Ordinal))
            {
                string id=DecodeFileId(file);if(id==null)continue;
                try{var snapshot=Load(id);if(snapshot!=null)items.Add(new RecoveryItem{Id=id,Snapshot=snapshot});}
                catch(Exception ex){items.Add(new RecoveryItem{Id=id,Error=ex.Message});}
            }
            return items.AsReadOnly();
        }
        internal static string DecodeFileId(string file)
        {
            string name=Path.GetFileName(file);if(!name.StartsWith("doc-",StringComparison.Ordinal)||!name.EndsWith(".json",StringComparison.Ordinal))return null;
            string hex=name.Substring(4,name.Length-9);if(hex.Length%2!=0||hex.Length>128)return null;
            var id=new StringBuilder();for(int i=0;i<hex.Length;i+=2){if(!byte.TryParse(hex.Substring(i,2),System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out var value))return null;id.Append((char)value);}
            return PackValidator.IsId(id.ToString())?id.ToString():null;
        }
        private static RecoverySnapshot Decode(byte[] bytes,string id)
        {
            if(bytes==null||bytes.Length>8388608)throw new FormatException("恢复文件过大。");
            string text=Utf8.GetString(bytes);JsonSyntaxGuard.Validate(text);
            JObject root;
            using(var reader=new JsonTextReader(new StringReader(text)){MaxDepth=8,DateParseHandling=DateParseHandling.None})
            {
                root=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
                if(reader.Read())throw new FormatException("恢复文件含尾随数据。");
            }
            var expected=new HashSet<string>{"version","baseSavedHash","savedUtcTicks","packJson"};
            foreach(var property in root.Properties())if(!expected.Remove(property.Name))throw new FormatException("未知恢复字段。");
            if(expected.Count!=0||root["version"].Type!=JTokenType.Integer||root["version"].Value<long>()!=1)throw new FormatException("恢复版本或字段无效。");
            if(root["savedUtcTicks"].Type!=JTokenType.Integer||root["packJson"].Type!=JTokenType.String)throw new FormatException("恢复时间或内容格式无效。");
            var baseline=root["baseSavedHash"];if(baseline.Type!=JTokenType.Null&&(baseline.Type!=JTokenType.String||!PackValidator.IsFingerprint(baseline.Value<string>())))throw new FormatException("恢复保存点无效。");
            var pack=StrictPackJson.Parse(root["packJson"].Value<string>());
            if(pack.documentKind!=DocumentKind.DraftPack||pack.packId!=id)throw new FormatException("恢复内容身份或类型不匹配。");
            return new RecoverySnapshot{Pack=pack,BaseSavedHash=baseline.Type==JTokenType.Null?null:baseline.Value<string>(),SavedUtc=new DateTime(root["savedUtcTicks"].Value<long>(),DateTimeKind.Utc)};
        }
    }
}
