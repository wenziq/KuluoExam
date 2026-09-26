using System.Threading.Tasks;
namespace Sokoban.Runtime.Platform.FileDialogs
{
 public interface IFileDialogService
 {
  Task<string> OpenAsync(string initialDirectory=null);
  Task<string> SaveAsync(string fileName,string initialDirectory=null);
 }
}
