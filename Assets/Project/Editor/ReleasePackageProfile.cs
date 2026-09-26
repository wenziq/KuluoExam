using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
namespace Sokoban.Editor
{
 [InitializeOnLoad]
 public static class ReleasePackageProfile
 {
  const string Pending="Sokoban.ReleasePackagesPending";
  static ReleasePackageProfile(){if(SessionState.GetBool(Pending,false))EditorApplication.delayCall+=VerifyResolved;}
  static AddAndRemoveRequest request;static double deadline;
  static readonly string[] DevelopmentPackages={"com.unity.ai.assistant","com.unity.ai.inference","com.unity.pipeline"};
  // Only an explicitly prepared disposable build copy may drop the author's editor tools.
  public static void Prepare()
  {
   if(!File.Exists("Library/SokobanReleaseCopy"))throw new InvalidOperationException("Prepare a separate release copy and its Library/SokobanReleaseCopy marker first.");
   SessionState.SetBool(Pending,true);
   request=Client.AddAndRemove(new[]{"com.unity.nuget.newtonsoft-json@3.2.2"},DevelopmentPackages);deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Poll;
  }
  public static void VerifyResolved()
  {
   SessionState.SetBool(Pending,false);
   var packages=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
   if(packages.Any(p=>DevelopmentPackages.Contains(p.name)))throw new InvalidOperationException("Development packages remain in release copy.");
   Debug.Log("Release package profile resolved: "+string.Join(", ",packages.Select(p=>p.name+"@"+p.version)));EditorApplication.Exit(0);
  }
  static void Poll()
  {
   if(!request.IsCompleted){if(EditorApplication.timeSinceStartup>deadline){EditorApplication.update-=Poll;Debug.LogError("Release package resolution timed out.");EditorApplication.Exit(2);}return;}
   EditorApplication.update-=Poll;
   if(request.Status!=StatusCode.Success){Debug.LogError(request.Error?.message);EditorApplication.Exit(1);return;}
   var remaining=request.Result.Where(p=>DevelopmentPackages.Contains(p.name)).ToArray();
   if(remaining.Length>0){Debug.LogError("Development packages remain in release copy.");EditorApplication.Exit(1);return;}
   VerifyResolved();
  }
 }
}
