using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sokoban.Runtime.Persistence;
namespace Sokoban.Runtime.Platform.FileDialogs
{
 public sealed class DirectoryListing
 {
  public string Directory;public readonly List<string> Directories=new List<string>(),Files=new List<string>();public bool Truncated;public string Error;
 }
 public sealed class DirectoryListingService
 {
  public Task<DirectoryListing> ListAsync(string path,CancellationToken token=default)=>Task.Run(()=>
  {
   var result=new DirectoryListing{Directory=path};var watch=Stopwatch.StartNew();int visited=0;
   try
   {
    foreach(var entry in Directory.EnumerateFileSystemEntries(path))
    {
     token.ThrowIfCancellationRequested();
     if(++visited>2000||result.Directories.Count+result.Files.Count>=200||watch.ElapsedMilliseconds>500){result.Truncated=true;break;}
     if(Path.GetFileName(entry).StartsWith(".",StringComparison.Ordinal))continue;
     var attr=File.GetAttributes(entry);
     if((attr&FileAttributes.Directory)!=0)result.Directories.Add(entry);
     else if(entry.EndsWith(ImportExportService.Extension,StringComparison.OrdinalIgnoreCase))result.Files.Add(entry);
    }
    result.Directories.Sort(StringComparer.CurrentCultureIgnoreCase);result.Files.Sort(StringComparer.CurrentCultureIgnoreCase);
   }
   catch(OperationCanceledException){throw;}
   catch(Exception e){result.Error=e.Message;}
   return result;
  },token);
 }
}
