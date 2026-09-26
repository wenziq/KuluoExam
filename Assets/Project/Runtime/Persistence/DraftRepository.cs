using System;
using System.IO;
using System.Threading.Tasks;
using Sokoban.Core.Data;
namespace Sokoban.Runtime.Persistence
{
    public sealed class DraftLoadResult
    {
        public PackData Pack { get; internal set; }
        public bool Missing { get; internal set; }
        public string Error { get; internal set; }
        public bool Succeeded => Pack != null && Error == null;
    }
    public sealed class DraftRepository
    {
        private readonly TransactionalFileStore store;
        public DraftRepository(UserDataPaths paths,IFileSystem files=null) { store=new TransactionalFileStore(paths,files); }
        public Task<TransactionResult> SaveAsync(PackData pack)
        {
            try
            {
                var bytes=StrictPackJson.Serialize(pack);string id=pack.packId;
                Validate(bytes,id);
                return store.SaveAsync(StorageArea.Drafts,id,bytes,b=>Validate(b,id));
            }
            catch(Exception ex){return Task.FromResult(new TransactionResult{Status=TransactionStatus.Failed,Error=ex});}
        }
        public Task<TransactionResult> RemoveAsync(string id)
        {
            string primary=store.Paths.Primary(StorageArea.Drafts,id);
            // Always acquire draft then recovery: keep both write queues gated until removal finishes.
            return store.Enqueue(primary,()=>store.Enqueue(store.Paths.Primary(StorageArea.Recovery,id),
                ()=>ArchiveDraft(id)).GetAwaiter().GetResult());
        }
        private TransactionResult ArchiveDraft(string id)
        {
            string archive=Path.Combine(store.Paths.Root,"Removed","Drafts",Guid.NewGuid().ToString("N"));
            var moved=new System.Collections.Generic.List<(string source,string destination)>();
            try
            {
                // Keep the catalog entry until the last move. Archive all copies so recovery cannot revive it.
                string[] sources={store.Paths.Backup(StorageArea.Recovery,id),store.Paths.Primary(StorageArea.Recovery,id),
                    store.Paths.Backup(StorageArea.Drafts,id),store.Paths.Primary(StorageArea.Drafts,id)};
                for(int i=0;i<sources.Length;i++)
                {
                    if(!store.Files.FileExists(sources[i]))continue;
                    store.Files.CreateDirectory(archive);
                    string destination=Path.Combine(archive,i+".removed");
                    store.Files.Move(sources[i],destination);moved.Add((sources[i],destination));
                }
                return new TransactionResult{Status=TransactionStatus.Committed,PreservedOriginalPath=archive};
            }
            catch(Exception ex)
            {
                var errors=new System.Collections.Generic.List<Exception>{ex};
                for(int i=moved.Count-1;i>=0;i--)
                {
                    try{store.Files.Move(moved[i].destination,moved[i].source);}
                    catch(Exception rollback){errors.Add(rollback);}
                }
                return new TransactionResult{Status=TransactionStatus.Failed,
                    Error=errors.Count==1?ex:new AggregateException("删除未完成，部分文件保留在："+archive,errors),
                    PreservedOriginalPath=archive};
            }
        }
        public DraftLoadResult Load(string id)
        {
            try
            {
                byte[] bytes=store.Files.ReadAllBytes(store.Paths.Primary(StorageArea.Drafts,id),store.MaxBytes);
                return new DraftLoadResult{Pack=Validate(bytes,id)};
            }
            catch(FileNotFoundException){return new DraftLoadResult{Missing=true};}
            catch(DirectoryNotFoundException){return new DraftLoadResult{Missing=true};}
            catch(Exception ex){return new DraftLoadResult{Error=ex.Message};}
        }
        public System.Collections.Generic.IReadOnlyList<string> ListDamaged()
        {
            var result=new System.Collections.Generic.List<string>();
            foreach(string file in store.Files.GetFiles(store.Paths.AreaDirectory(StorageArea.Drafts),UserDataPaths.PrimaryPattern))
            {string id=RecoveryService.DecodeFileId(file);if(id!=null&&Load(id).Error!=null)result.Add(id);}
            return result.AsReadOnly();
        }
        public Task<RecoveryReport> InspectAsync(string id)=>new StorageRecovery(store).InspectAsync(StorageArea.Drafts,id,b=>Validate(b,id));
        public Task<TransactionResult> RecoverBackupAsync(string id)=>new StorageRecovery(store).RecoverBackupAsync(StorageArea.Drafts,id,b=>Validate(b,id));
        private static PackData Validate(byte[] bytes,string id)
        {
            var pack=StrictPackJson.Parse(bytes);
            if(pack.documentKind!=DocumentKind.DraftPack||pack.packId!=id)throw new FormatException("草稿类型或身份与保存位置不一致。");
            return pack;
        }
    }
}
