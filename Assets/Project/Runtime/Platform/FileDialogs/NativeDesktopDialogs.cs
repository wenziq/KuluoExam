using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Sokoban.Runtime.Platform.FileDialogs
{
    public static class NativeDesktopDialogs
    {
        public static Task<string> ShowAsync(bool save,string fileName,string directory)
        {
#if UNITY_EDITOR
            string path=save
                ?UnityEditor.EditorUtility.SaveFilePanel("导出推箱子关卡包",directory,fileName,"sokopack.json")
                :UnityEditor.EditorUtility.OpenFilePanelWithFilters("导入推箱子关卡包",directory,new[]{"推箱子关卡包","sokopack.json"});
            return Task.FromResult(string.IsNullOrEmpty(path)?null:path);
#else
            if(Application.platform==RuntimePlatform.WindowsPlayer)return ShowWindowsAsync(save,fileName,directory);
            if(Application.platform==RuntimePlatform.OSXPlayer)return Task.FromResult(ShowMac(save,fileName,directory));
            throw new PlatformNotSupportedException("此平台尚未提供系统文件选择器。");
#endif
        }
        public static string ShowMac(bool save,string fileName,string directory)
        {
            IntPtr result=SokobanShowFilePanel(save?1:0,fileName??"",directory??"");
            if(result==IntPtr.Zero)return null;
            try{return Marshal.PtrToStringUTF8(result);}
            finally{SokobanFreeFilePanelResult(result);}
        }
        [DllImport("SokobanFileDialogs",CallingConvention=CallingConvention.Cdecl)]
        static extern IntPtr SokobanShowFilePanel(int save,[MarshalAs(UnmanagedType.LPUTF8Str)]string name,[MarshalAs(UnmanagedType.LPUTF8Str)]string directory);
        [DllImport("SokobanFileDialogs",CallingConvention=CallingConvention.Cdecl)]
        static extern void SokobanFreeFilePanelResult(IntPtr result);

        static Task<string> ShowWindowsAsync(bool save,string fileName,string directory)
        {
            var completion=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            IntPtr owner=GetActiveWindow();
            var thread=new Thread(()=>
            {
                bool initialized=false;IFileDialog dialog=null;IShellItem folder=null,result=null;
                try
                {
                    Marshal.ThrowExceptionForHR(CoInitializeEx(IntPtr.Zero,2));initialized=true;
                    var clsid=new Guid(save?"C0B4E2F3-BA21-4773-8DBA-335EC946EB8B":"DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");
                    var iid=typeof(IFileDialog).GUID;
                    Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid,IntPtr.Zero,1,ref iid,out dialog));
                    dialog.GetOptions(out uint options);
                    dialog.SetOptions(options|0x40u|0x800u|0x8u|(save?0x2u|0x4u:0x1000u));
                    dialog.SetFileTypes(1,new[]{new FilterSpec{name="推箱子关卡包",pattern="*.sokopack.json"}});
                    dialog.SetFileTypeIndex(1);dialog.SetDefaultExtension("sokopack.json");
                    dialog.SetTitle(save?"导出推箱子关卡包":"导入推箱子关卡包");
                    if(save)dialog.SetFileName(fileName??"关卡包.sokopack.json");
                    if(!string.IsNullOrEmpty(directory))
                    {
                        var shellId=typeof(IShellItem).GUID;
                        if(SHCreateItemFromParsingName(directory,IntPtr.Zero,ref shellId,out folder)>=0)dialog.SetFolder(folder);
                    }
                    int hr=dialog.Show(owner);
                    if(hr==unchecked((int)0x800704C7)){completion.TrySetResult(null);return;}
                    Marshal.ThrowExceptionForHR(hr);dialog.GetResult(out result);
                    result.GetDisplayName(0x80058000,out IntPtr path); // SIGDN_FILESYSPATH
                    try{completion.TrySetResult(Marshal.PtrToStringUni(path));}
                    finally{Marshal.FreeCoTaskMem(path);}
                }
                catch(Exception e){completion.TrySetException(e);}
                finally
                {
                    if(result!=null)Marshal.ReleaseComObject(result);
                    if(folder!=null)Marshal.ReleaseComObject(folder);
                    if(dialog!=null)Marshal.ReleaseComObject(dialog);
                    if(initialized)CoUninitialize();
                }
            });
            thread.IsBackground=true;thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task;
        }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        struct FilterSpec
        {
            [MarshalAs(UnmanagedType.LPWStr)]public string name;
            [MarshalAs(UnmanagedType.LPWStr)]public string pattern;
        }
        // Native vtable order includes unused methods before SetDefaultExtension.
        [ComImport,Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IFileDialog
        {
            [PreserveSig]int Show(IntPtr owner);
            void SetFileTypes(uint count,[In,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=0)]FilterSpec[] filters);
            void SetFileTypeIndex(uint index);void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events,out uint cookie);void Unadvise(uint cookie);
            void SetOptions(uint options);void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem folder);void SetFolder(IShellItem folder);
            void GetFolder(out IShellItem folder);void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)]string name);
            void GetFileName(out IntPtr name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)]string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)]string label);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)]string label);
            void GetResult(out IShellItem item);void AddPlace(IShellItem item,uint location);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)]string extension);
        }
        [ComImport,Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItem
        {
            void BindToHandler(IntPtr context,ref Guid handler,ref Guid id,out IntPtr value);
            void GetParent(out IShellItem parent);void GetDisplayName(uint type,out IntPtr name);
            void GetAttributes(uint mask,out uint attributes);void Compare(IShellItem other,uint hint,out int order);
        }
        [DllImport("user32.dll")]static extern IntPtr GetActiveWindow();
        [DllImport("ole32.dll")]static extern int CoInitializeEx(IntPtr reserved,uint mode);
        [DllImport("ole32.dll")]static extern void CoUninitialize();
        [DllImport("ole32.dll")]
        static extern int CoCreateInstance(ref Guid clsid,IntPtr outer,uint context,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IFileDialog dialog);
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)]
        static extern int SHCreateItemFromParsingName(string path,IntPtr context,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IShellItem item);
    }
}
