using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sokoban.Core.Data;
namespace Sokoban.Runtime.Persistence
{
    public sealed class InstallResult
    {
        public TransactionResult Transaction {get; internal set;}
        public PackData Pack {get; internal set;}
        public bool Committed=>Transaction?.Committed==true;
    }
    public sealed class InstalledPackRepository
    {
        readonly TransactionalFileStore store;
        public InstalledPackRepository(UserDataPaths paths,IFileSystem files=null){store=new TransactionalFileStore(paths,files);}
        public PackData Load(string id,CancellationToken token=default)
        {
            try{return Validate(store.Files.ReadAllBytes(store.Paths.Primary(StorageArea.Installed,id),store.MaxBytes),id,token);}
            catch(FileNotFoundException){return null;}
            catch(DirectoryNotFoundException){return null;}
        }
        public Task<InstallResult> InstallAsync(PackData pack,bool advanceRevision,CancellationToken token=default)
        {
            try
            {
                var snapshot=pack.DeepCopy();string id=snapshot.packId;
                return store.Enqueue(store.Paths.Primary(StorageArea.Installed,id),()=>
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();ContentCatalog.ValidatePlayable(snapshot);
                        var previous=Load(id);
                        if(advanceRevision)snapshot.contentRevision=checked((previous?.contentRevision??0)+1);
                        var bytes=StrictPackJson.Serialize(snapshot);
                        var transaction=store.Commit(StorageArea.Installed,id,bytes,data=>Validate(data,id),token,true);
                        return new InstallResult{Transaction=transaction,Pack=transaction.Committed?snapshot.DeepCopy():null};
                    }
                    catch(Exception error){return Failure(error);}
                });
            }
            catch(Exception error){return Task.FromResult(Failure(error));}
        }
        public Task<TransactionResult> RemoveAsync(string id)
        {
            string primary=store.Paths.Primary(StorageArea.Installed,id);
            // Share the install queue so a concurrent commit cannot interleave with removal.
            return store.Enqueue(primary,()=>
            {
                try
                {
                    if(!store.Files.FileExists(primary))
                        return new TransactionResult{Status=TransactionStatus.Committed};
                    var pack=StrictPackJson.Parse(store.Files.ReadAllBytes(primary,store.MaxBytes));
                    if(pack.packId!=id)throw new FormatException("关卡集身份与文件位置不一致，未删除。");
                    string removed=Path.Combine(store.Paths.Root,"Removed","Installed",Path.GetFileName(primary)+"."+Guid.NewGuid().ToString("N")+".removed");
                    store.Files.CreateDirectory(Path.GetDirectoryName(removed));
                    // One rename removes the visible copy, retaining its bytes separately from the catalog.
                    store.Files.Move(primary,removed);
                    return new TransactionResult{Status=TransactionStatus.Committed,PreservedOriginalPath=removed};
                }
                catch(Exception error){return new TransactionResult{Status=TransactionStatus.Failed,Error=error};}
            });
        }
        static InstallResult Failure(Exception error)=>new InstallResult{Transaction=new TransactionResult{Status=error is OperationCanceledException?TransactionStatus.Cancelled:TransactionStatus.Failed,Error=error}};
        static PackData Validate(byte[] bytes,string id,CancellationToken token=default)
        {
            var pack=StrictPackJson.Parse(bytes);if(pack.packId!=id)throw new FormatException("已安装内容的身份与文件位置不一致。");ContentCatalog.ValidatePlayable(pack,token);return pack;
        }
    }
}
