using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sokoban.Core.Data;
namespace Sokoban.Runtime.Persistence
{
 public sealed class ImportStagingRepository
 {
  readonly TransactionalFileStore store;
  public ImportStagingRepository(UserDataPaths paths,IFileSystem files=null){store=new TransactionalFileStore(paths,files);}
  public Task<TransactionResult> SaveAsync(string id,PackData pack,CancellationToken token=default)=>store.SaveAsync(StorageArea.Staging,id,StrictPackJson.Serialize(pack),b=>StrictPackJson.Parse(b),token);
  public PackData Load(string id)=>StrictPackJson.Parse(store.Files.ReadAllBytes(store.Paths.Primary(StorageArea.Staging,id),store.MaxBytes));
  public IReadOnlyList<string> List(){var ids=new List<string>();foreach(var path in store.Files.GetFiles(store.Paths.AreaDirectory(StorageArea.Staging),UserDataPaths.PrimaryPattern)){var id=RecoveryService.DecodeFileId(path);if(id!=null)ids.Add(id);}return ids.AsReadOnly();}
  public Task RemoveAsync(string id)=>store.Enqueue(store.Paths.Primary(StorageArea.Staging,id),()=>{store.Files.Delete(store.Paths.Primary(StorageArea.Staging,id));store.Files.Delete(store.Paths.Backup(StorageArea.Staging,id));return true;});
 }
}
