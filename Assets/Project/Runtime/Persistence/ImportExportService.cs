using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Validation;
using Sokoban.Domain.Analysis;
namespace Sokoban.Runtime.Persistence
{
 public sealed class ImportResult
 {
  public bool Committed {get;internal set;}
  public PackData Pack {get;internal set;}
  public string StagingId {get;internal set;}
  public string Error {get;internal set;}
 }
 public sealed class ImportExportService
 {
  public const string Extension=".sokopack.json";
  readonly UserDataPaths paths;readonly TransactionalFileStore store;readonly WitnessRepository witnesses;readonly HashSet<string> reserved;
  public ImportStagingRepository Staging {get;}
  public ImportExportService(UserDataPaths paths,IFileSystem files=null,IAnalysisSolver solver=null,IEnumerable<string> reservedIds=null)
  {this.paths=paths;store=new TransactionalFileStore(paths,files);witnesses=new WitnessRepository(solver);reserved=new HashSet<string>(reservedIds??Array.Empty<string>(),StringComparer.Ordinal);Staging=new ImportStagingRepository(paths,store.Files);}
  public Task<PackData> ReadAsync(string path,CancellationToken token=default)=>Task.Run(()=>{RequireExtension(path);token.ThrowIfCancellationRequested();var p=StrictPackJson.Parse(store.Files.ReadAllBytes(path,store.MaxBytes));token.ThrowIfCancellationRequested();return p;},token);
  public async Task<ImportResult> ImportAsync(PackData input,bool replace=false,bool asDraft=false,CancellationToken token=default,string stagingId=null)
  {
   var result=new ImportResult();
   try
   {
    var safe=StrictPackJson.Parse(StrictPackJson.Serialize(input));
    if(replace&&reserved.Contains(safe.packId))throw new InvalidOperationException("内置关卡身份不能被替换，请创建副本。");
    var pack=replace||stagingId!=null?safe:ContentIdentity.CreateIndependentCopy(safe);if(asDraft)pack.documentKind=DocumentKind.DraftPack;
    token.ThrowIfCancellationRequested();
    if(pack.documentKind==DocumentKind.DraftPack)
    {
     var bytes=StrictPackJson.Serialize(pack);var saved=await store.SaveAsync(StorageArea.Drafts,pack.packId,bytes,b=>{var p=StrictPackJson.Parse(b);if(p.documentKind!=DocumentKind.DraftPack||p.packId!=pack.packId)throw new FormatException("草稿身份不匹配。");},token);
     if(!saved.Committed)throw saved.Error??new IOException("草稿未提交。");result.Committed=true;result.Pack=pack;
    }
    else
    {
     result.StagingId=stagingId??ContentIds.NewId();
     var staged=await Staging.SaveAsync(result.StagingId,pack,token);if(!staged.Committed)throw staged.Error??new IOException("无法暂存导入内容。");
     var playable=await PreparePlayableAsync(pack,token);
     var installed=await new InstalledPackRepository(paths,store.Files).InstallAsync(playable,false,token);
     if(!installed.Committed)throw installed.Transaction.Error??new IOException("正式包未提交。");result.Committed=true;result.Pack=installed.Pack;
    }
    if(stagingId!=null||result.StagingId!=null)
    {try{await Staging.RemoveAsync(result.StagingId??stagingId);result.StagingId=null;}catch(Exception e){result.Error="内容已导入；待验证记录清理失败："+e.Message;}}
   }
   catch(Exception e){result.Error=e is OperationCanceledException?"已取消，原有内容保持不变。":e.Message;}
   return result;
  }
  public async Task<TransactionResult> ExportAsync(PackData input,string path,bool playable,bool overwrite,CancellationToken token=default)
  {
   try
   {
    var snapshot=StrictPackJson.Parse(StrictPackJson.Serialize(input));RequireExtension(path);path=Path.GetFullPath(path);
    if(playable)snapshot=await PreparePlayableAsync(snapshot,token);else snapshot.documentKind=DocumentKind.DraftPack;
    var bytes=StrictPackJson.Serialize(snapshot);
    return await store.Enqueue(path,()=>
    {
     if(!Directory.Exists(Path.GetDirectoryName(path)))return new TransactionResult{Error=new DirectoryNotFoundException("目标目录不存在。")};
     if(store.Files.FileExists(path)&&!overwrite)return new TransactionResult{Error=new IOException("目标文件已存在，请确认覆盖。")};
     return store.CommitPath(path,path+".bak",bytes,b=>{var p=StrictPackJson.Parse(b);if(playable)ContentCatalog.ValidatePlayable(p);},token,true);
    });
   }
   catch(Exception e){return new TransactionResult{Status=e is OperationCanceledException?TransactionStatus.Cancelled:TransactionStatus.Failed,Error=e};}
  }
  async Task<PackData> PreparePlayableAsync(PackData pack,CancellationToken token)
  {
   if(pack.levels.Count==0)throw new InvalidOperationException("关卡集为空，请先制作关卡。");
   var proofs=new List<WitnessData>();
   foreach(var id in pack.levelOrder)
   {
    token.ThrowIfCancellationRequested();var level=pack.levels.Single(l=>l.levelId==id);
    if(!StructureValidator.Validate(level).IsValid)throw new InvalidOperationException(level.name+"：结构不完整，请转为草稿修复。");
    var evidence=await witnesses.ResolveAsync(level,pack.solutionWitnesses,token);
    if(evidence.Outcome!=AnalysisOutcome.Solvable||evidence.Witness==null)throw new InvalidOperationException(level.name+"："+evidence.Explanation+" 可转为草稿继续制作。");
    proofs.Add(evidence.Witness.DeepCopy());
   }
   pack.solutionWitnesses=proofs;pack.documentKind=DocumentKind.PlayablePack;return pack;
  }
  public static void RequireExtension(string path){if(string.IsNullOrWhiteSpace(path)||!path.EndsWith(Extension,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("请选择或输入 .sokopack.json 文件。");}
 }
}
