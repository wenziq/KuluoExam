using NUnit.Framework;
using System.IO;
using System.Linq;
using Sokoban.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;

namespace Sokoban.EditModeTests
{
    public sealed class RenderConfigurationTests
    {
        [Test]
        public void FreshEditorSceneOpensBootWithoutSelectingPreview()
        {
            WithTemporaryScene(() =>
            {
                ShowStartupPreview();
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ProjectScaffold.BootScenePath));
                var preview = SceneManager.GetActiveScene().GetRootGameObjects()
                    .Single(x => x.name == "EditorPreview (main menu)");
                Assert.That(preview.activeInHierarchy, Is.True);
                Assert.That(SceneManager.GetActiveScene().isDirty, Is.False);
            });
        }

        [Test]
        public void StartupPreservesUnsavedScene()
        {
            WithTemporaryScene(() =>
            {
                var scene = SceneManager.GetActiveScene();
                new GameObject("Unsaved author work");
                EditorSceneManager.MarkSceneDirty(scene);
                ShowStartupPreview();
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
                Assert.That(GameObject.Find("Unsaved author work"), Is.Not.Null);
            });
        }

        [Test]
        public void StartupPreservesMultipleOpenScenes()
        {
            WithTemporaryScene(() =>
            {
                EditorSceneManager.OpenScene(ProjectScaffold.BootScenePath);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var scene = SceneManager.GetActiveScene();
                ShowStartupPreview();
                Assert.That(SceneManager.sceneCount, Is.EqualTo(2));
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            });
        }

        private static void ShowStartupPreview() => typeof(BootScenePresentation)
            .GetMethod("ShowStartupPreview", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        private static void WithTemporaryScene(System.Action check)
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var startupHandled = SessionState.GetBool("Sokoban.ScenePreview.StartupHandled", false);
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                check();
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                SessionState.SetBool("Sokoban.ScenePreview.StartupHandled", startupHandled);
            }
        }

        [Test]
        public void EnabledBuildScenesExistForUrpPreprocessing()
        {
            Assert.That(EditorBuildSettings.scenes.Where(s => s.enabled && !File.Exists(s.path)), Is.Empty,
                "URP reads enabled EditorBuildSettings scenes even when BuildPlayer supplies its own scene list.");
        }

        [Test]
        public void BootstrapRemovesMissingScenesBeforeUrpBuildProcessing()
        {
            var original = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene("Assets/DeletedBuildScene.unity", true),
                    new EditorBuildSettingsScene(ProjectScaffold.BootScenePath, true)
                };
                ProjectScaffold.EnsureBootstrap();
                Assert.That(EditorBuildSettings.scenes.Select(s => s.path),
                    Is.EqualTo(new[] { ProjectScaffold.BootScenePath }));
            }
            finally { EditorBuildSettings.scenes = original; }
        }
    }
}
