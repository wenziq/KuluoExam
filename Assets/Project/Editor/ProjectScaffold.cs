using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sokoban.Editor
{
    public static class ProjectScaffold
    {
        public const string BootScenePath = "Assets/Project/Scenes/Boot.unity";

        [MenuItem("Sokoban/Ensure Bootstrap")]
        public static void EnsureBootstrap()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before scaffolding.");

            Directory.CreateDirectory("Assets/Project/Scenes");
            AssetDatabase.Refresh();
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(BootScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedHere)
                    scene = File.Exists(BootScenePath)
                        ? EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Additive)
                        : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

                var roots = scene.GetRootGameObjects().Where(x => x.name == "AppRoot").ToArray();
                if (roots.Length > 1)
                    throw new InvalidOperationException("Boot contains multiple AppRoot objects; preserve and resolve them manually.");
                if (roots.Length == 0)
                {
                    if (!openedHere && scene.isDirty)
                        throw new InvalidOperationException("Boot has unsaved changes. Save them before adding AppRoot.");
                    var root = new GameObject("AppRoot");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    if (!EditorSceneManager.SaveScene(scene, BootScenePath))
                        throw new IOException("Could not save " + BootScenePath);
                }
                // URP scans this list even when BuildPlayerOptions supplies an explicit scene list.
                // Deleted scenes otherwise abort its preprocessing and omit runtime graphics settings.
                var originalScenes = EditorBuildSettings.scenes;
                var buildScenes = originalScenes.Where(x => File.Exists(x.path)).ToList();
                if (!buildScenes.Any(x => x.path == BootScenePath))
                    buildScenes.Add(new EditorBuildSettingsScene(BootScenePath, true));
                if (buildScenes.Count != originalScenes.Length || !originalScenes.Any(x => x.path == BootScenePath))
                    EditorBuildSettings.scenes = buildScenes.ToArray();
                Debug.Log("Sokoban bootstrap ready: " + BootScenePath);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
