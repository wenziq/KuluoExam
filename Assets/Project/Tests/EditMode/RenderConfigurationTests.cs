using NUnit.Framework;
using System.IO;
using System.Linq;
using Sokoban.Editor;
using UnityEditor;

namespace Sokoban.EditModeTests
{
    public sealed class RenderConfigurationTests
    {
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
