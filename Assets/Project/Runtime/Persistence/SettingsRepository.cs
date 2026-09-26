using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Sokoban.Runtime.Persistence
{
 public sealed class GameSettings
 {
  public int version=1;public float volume=.35f;public bool sound=true,fullscreen;
  public GameSettings Copy()=>(GameSettings)MemberwiseClone();
 }
 public sealed class SettingsLoadResult{public GameSettings Settings {get;internal set;}public string Error {get;internal set;}}
 public sealed class SettingsRepository
 {
  readonly TransactionalFileStore store;static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
  public SettingsRepository(UserDataPaths paths,IFileSystem files=null){store=new TransactionalFileStore(paths,files,4096);}
  public SettingsLoadResult Load()
  {
   try{return new SettingsLoadResult{Settings=Parse(store.Files.ReadAllBytes(store.Paths.Primary(StorageArea.Settings,"preferences"),4096))};}
   catch(FileNotFoundException){return new SettingsLoadResult{Settings=new GameSettings()};}
   catch(DirectoryNotFoundException){return new SettingsLoadResult{Settings=new GameSettings()};}
   catch(Exception e){return new SettingsLoadResult{Settings=new GameSettings(),Error="设置读取失败，已使用默认值。原文件保留。"+e.Message};}
  }
  public Task<TransactionResult> SaveAsync(GameSettings settings)
  {
   try{Validate(settings);var bytes=Utf8.GetBytes(JsonConvert.SerializeObject(settings.Copy()));return store.SaveAsync(StorageArea.Settings,"preferences",bytes,b=>Parse(b));}
   catch(Exception e){return Task.FromResult(new TransactionResult{Error=e});}
  }
  static GameSettings Parse(byte[] bytes)
  {
   using(var reader=new JsonTextReader(new StringReader(Utf8.GetString(bytes))){MaxDepth=4,DateParseHandling=DateParseHandling.None})
   {
    var value=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});if(reader.Read())throw new FormatException("设置包含额外内容。");
    if(value.Properties().Count()!=4||value["version"]?.Type!=JTokenType.Integer||value["sound"]?.Type!=JTokenType.Boolean||value["fullscreen"]?.Type!=JTokenType.Boolean||(value["volume"]?.Type!=JTokenType.Float&&value["volume"]?.Type!=JTokenType.Integer))throw new FormatException("设置字段无效。");
    var settings=new GameSettings{version=(int)value["version"],volume=(float)value["volume"],sound=(bool)value["sound"],fullscreen=(bool)value["fullscreen"]};Validate(settings);return settings;
   }
  }
  static void Validate(GameSettings s){if(s==null||s.version!=1||float.IsNaN(s.volume)||float.IsInfinity(s.volume)||s.volume<0||s.volume>1)throw new FormatException("设置版本或音量范围无效。");}
 }
}
