#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace Sokoban.PlayModeTests
{
    internal static class GameViewEvidence
    {
        public static IEnumerator Capture(string path,int width,int height)
        {
            Canvas.ForceUpdateCanvases();var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic;
            var method=type.GetMethod("GetMainPlayModeView",flags|BindingFlags.Static);var field=type.GetField("m_TargetTexture",flags|BindingFlags.Instance);
            RenderTexture source=null;
            for(int frame=0;frame<8;frame++){var view=method.Invoke(null,null);((UnityEditor.EditorWindow)view).Repaint();yield return null;source=field.GetValue(view)as RenderTexture;}
            Assert.That(source,Is.Not.Null);Assert.That(source.width,Is.EqualTo(width));Assert.That(source.height,Is.EqualTo(height));
            var target=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                if(SystemInfo.graphicsUVStartsAtTop)Graphics.Blit(source,target,new Vector2(1,-1),new Vector2(0,1));else Graphics.Blit(source,target);
                RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
                var pixels=texture.GetPixels32();var colors=new HashSet<Color32>();for(int y=0;y<height;y+=11)for(int x=0;x<width;x+=11)colors.Add(pixels[y*width+x]);
                Assert.That(colors.Count,Is.GreaterThan(8));Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(texture);}
        }
    }
}
#endif
