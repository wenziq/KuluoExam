using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
namespace Sokoban.Editor
{
    public static class UiPrefabBuilder
    {
        public const string ThemePath = "Assets/Project/Settings/SokobanTheme.asset";
        public const string PrefabPath = "Assets/Project/Prefabs/UI/AppShell.prefab";
        [MenuItem("Sokoban/Ensure UI Assets")]
        public static void EnsureAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before scaffolding.");
            foreach (var path in new[] { "Assets/Project/Settings", "Assets/Project/Prefabs/UI", "Assets/Project/Art/Fonts" })
                Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UiTheme>();
                theme.font = EnsureFont();
                AssetDatabase.CreateAsset(theme, ThemePath);
                AssetDatabase.SaveAssetIfDirty(theme);
            }
            if (theme.font == null)
                throw new InvalidOperationException("Existing theme has no font; select its font explicitly to preserve authored settings.");
            string inputPath = "Assets/Project/Settings/SokobanInputActions.inputactions";
            if (!File.Exists(inputPath))
            {
                var actions = ScriptableObject.CreateInstance<InputActionAsset>();
                var map = actions.AddActionMap("Gameplay");
                var move = map.AddAction("Move", InputActionType.Value);
                move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/s").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/a").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/d").With("Right", "<Keyboard>/rightArrow");
                map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
                File.WriteAllText(inputPath, actions.ToJson());
                UnityEngine.Object.DestroyImmediate(actions);
                AssetDatabase.ImportAsset(inputPath);
            }
            if (!File.Exists(PrefabPath))
            {
                var root = new GameObject("AppShellRoot");
                try
                {
                    var app = root.AddComponent<ApplicationController>();
                    app.theme = theme;
                    app.BuildShell();
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            ConnectBoot();
        }
        [MenuItem("Sokoban/Ensure Game Flow Assets")]
        public static void EnsureGameFlowAssets()
        {
            EnsureAssets();
            PlayerSettings.productName = "推箱子";
            const string pages = "Assets/Project/Resources/UI/Pages";
            Directory.CreateDirectory(pages);
            AssetDatabase.Refresh();
            SavePage<Sokoban.Runtime.Presentation.Views.MainMenuView>("MainMenu");
            SavePage<Sokoban.Runtime.Presentation.Views.PackLibraryView>("PackLibrary");
            SavePage<Sokoban.Runtime.Presentation.Views.LevelSelectView>("LevelSelect");
            SavePage<Sokoban.Runtime.Presentation.Views.GameplayView>("Gameplay");
            SavePage<Sokoban.Runtime.Presentation.Views.WorkshopView>("Workshop");
            SavePage<Sokoban.Runtime.Presentation.Analysis.AnalysisResultModal>("AnalysisResult");
            var shell = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                shell.GetComponent<ApplicationController>().startGameFlow = true;
                PrefabUtility.SaveAsPrefabAsset(shell, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(shell); }
            const string builtIn = "Assets/StreamingAssets/BuiltInPacks/u7-flow-fixture.sokopack.json";
            if (!File.Exists(builtIn) && (!Directory.Exists("Assets/StreamingAssets/BuiltInPacks") || Directory.GetFiles("Assets/StreamingAssets/BuiltInPacks", "*.sokopack.json").Length == 0))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(builtIn));
                var fixture = Sokoban.Runtime.Fixtures.PlayableFlowFixture.Create();
                Sokoban.Runtime.Persistence.ContentCatalog.ValidatePlayable(fixture);
                File.WriteAllBytes(builtIn, Sokoban.Runtime.Persistence.StrictPackJson.Serialize(fixture));
                AssetDatabase.ImportAsset(builtIn);
            }
            void SavePage<T>(string name) where T : Component
            {
                string path = pages + "/" + name + ".prefab";
                if (File.Exists(path)) return;
                var root = new GameObject(name, typeof(RectTransform));
                try
                {
                    root.AddComponent<T>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }
        private static TMP_FontAsset EnsureFont()
        {
            const string path = "Assets/Project/Art/Fonts/SokobanCJK.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
                return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Project/Art/Fonts/NotoSansCJKsc-Regular.otf");
            if (source == null)
                throw new InvalidOperationException("Noto font must be imported first.");
            const string fallbackPath = "Assets/Project/Art/Fonts/SokobanCJK-Dynamic.asset";
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fallbackPath);
            if (fallback == null)
            {
                fallback = TMP_FontAsset.CreateFontAsset(source, 48, 5, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                fallback.name = "SokobanCJK-Dynamic";
                var fallbackSettings = new SerializedObject(fallback);
                fallbackSettings.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
                fallbackSettings.ApplyModifiedPropertiesWithoutUndo();
                SaveFont(fallback, fallbackPath);
            }
            var font = TMP_FontAsset.CreateFontAsset(source, 48, 5, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            font.name = "SokobanCJK";
            // Bake the actual shipped UI copy; user authored characters use the bundled dynamic font.
            string corpus = "";
            foreach (var file in Directory.GetFiles("Assets/Project/Runtime", "*.cs", SearchOption.AllDirectories))
                corpus += File.ReadAllText(file);
            corpus += new string(Enumerable.Range(32, 95).Select(x => (char)x).ToArray());
            font.TryAddCharacters(new string(corpus.Distinct().ToArray()), out string missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            if (font.fallbackFontAssetTable == null)
                font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            font.fallbackFontAssetTable.Add(fallback);
            SaveFont(font, path);
            return font;
        }
        private static void SaveFont(TMP_FontAsset font, string path)
        {
            AssetDatabase.CreateAsset(font, path);
            foreach (var texture in font.atlasTextures)
                if (texture != null)
                    AssetDatabase.AddObjectToAsset(texture, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            AssetDatabase.SaveAssetIfDirty(font);
        }
        private static void ConnectBoot()
        {
            ProjectScaffold.EnsureBootstrap();
            var scene = SceneManager.GetSceneByPath(ProjectScaffold.BootScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(ProjectScaffold.BootScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty)
                    throw new InvalidOperationException("Boot has unsaved changes; preserve before updating UI.");
                var root = scene.GetRootGameObjects().Single(x => x.name == "AppRoot");
                if (root.GetComponentInChildren<ApplicationController>(true) == null)
                {
                    PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), root.transform);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
