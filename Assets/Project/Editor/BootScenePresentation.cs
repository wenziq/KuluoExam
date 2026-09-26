using System;
using System.IO;
using System.Linq;
using Sokoban.Domain.Gameplay;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Board;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sokoban.Editor
{
    public static class BootScenePresentation
    {
        [MenuItem("Sokoban/Ensure Scene Preview")]
        public static void Refresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before refreshing the preview.");
            var scene = SceneManager.GetSceneByPath(ProjectScaffold.BootScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ProjectScaffold.BootScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save existing scene changes before refreshing the preview.");
                var root = scene.GetRootGameObjects().Single(x => x.name == "AppRoot");
                var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(UiPrefabBuilder.ThemePath);
                // The empty runtime shell otherwise covers the preview in Scene view. Initialize enables it in Play Mode.
                var runtimeCanvas = root.GetComponentInChildren<ApplicationController>().canvasRoot;
                runtimeCanvas.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(runtimeCanvas.gameObject);
                if (!scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Camera>()).Any())
                    UiBackgroundCamera.Create(root.transform, theme.background);
                var existing = scene.GetRootGameObjects().SingleOrDefault(x => x.GetComponent<EditorScenePreview>() != null);
                if (existing == null) CreatePreview(scene, theme);
                else
                {
                    ConfigurePreview(existing);
                    PersistPreviewFont(existing);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Boot scene.");
                Debug.Log("Boot scene has a background camera and an edit-time main menu preview.");
            }
            finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void CreatePreview(Scene scene, UiTheme theme)
        {
            var root = new GameObject("EditorPreview (main menu)");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.tag = "EditorOnly";
            var app = root.AddComponent<ApplicationController>();
            app.theme = theme;
            app.Initialize();
            ConfigurePreview(root);
            var page = UiFactory.Rect("MainMenuPreview", app.pageRoot).gameObject.AddComponent<MainMenuView>();
            var pack = StrictPackJson.Parse(File.ReadAllBytes("Assets/StreamingAssets/BuiltInPacks/tutorial.sokopack.json"));
            page.Build(app, pack, new ProgressService(), null);
            Canvas.ForceUpdateCanvases();
            foreach (var board in root.GetComponentsInChildren<BoardView>()) board.SetView(1, Vector2.zero);
            PersistRoundedSprite(root);
            PersistPreviewFont(root);
            // Keep serialized visual components only. Preview never reads user data or runs the app coordinator.
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true).Reverse().ToArray())
                if (!(component is UnityEngine.EventSystems.UIBehaviour) && !(component is BoardTileGraphic) && !(component is UiIcon))
                    UnityEngine.Object.DestroyImmediate(component);
            var eventSystem = root.transform.Find("EventSystem");
            if (eventSystem != null) UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);
            foreach (var raycaster in root.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>()) UnityEngine.Object.DestroyImmediate(raycaster);
            var canvasRoot = root.GetComponentInChildren<Canvas>().gameObject;
            canvasRoot.transform.SetParent(null, false);
            canvasRoot.name = "EditorPreview (main menu)";
            canvasRoot.tag = "EditorOnly";
            canvasRoot.AddComponent<EditorScenePreview>();
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void ConfigurePreview(GameObject root)
        {
            root.GetComponentInChildren<Canvas>().sortingOrder = 100;
            var scaler = root.GetComponentInChildren<UnityEngine.UI.CanvasScaler>();
            if (scaler != null) UnityEngine.Object.DestroyImmediate(scaler);
            var canvas = root.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(1280, 720);
            rect.localScale = Vector3.one;
            rect.localPosition = Vector3.zero;
        }

        private static void PersistPreviewFont(GameObject root)
        {
            const string path = "Assets/Project/Art/Fonts/EditorPreviewFont.asset";
            var labels = root.GetComponentsInChildren<TMPro.TMP_Text>(true);
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Project/Art/Fonts/NotoSansCJKsc-Regular.otf");
                font = TMPro.TMP_FontAsset.CreateFontAsset(source, 48, 5,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                    TMPro.AtlasPopulationMode.Dynamic, true);
                font.name = "EditorPreviewFont";
                font.TryAddCharacters(string.Concat(labels.Select(x => x.text)), out string missing);
                font.atlasPopulationMode = TMPro.AtlasPopulationMode.Static;
                AssetDatabase.CreateAsset(font, path);
                foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
                AssetDatabase.AddObjectToAsset(font.material, font);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            foreach (var submesh in root.GetComponentsInChildren<TMPro.TMP_SubMeshUI>())
                UnityEngine.Object.DestroyImmediate(submesh.gameObject);
            foreach (var label in labels)
            {
                label.font = font;
                label.fontSharedMaterial = font.material;
                label.ForceMeshUpdate(true, true);
            }
        }

        private static void PersistRoundedSprite(GameObject root)
        {
            const string path = "Assets/Project/Art/EditorPreviewRounded.asset";
            var images = root.GetComponentsInChildren<UnityEngine.UI.Image>().Where(i => i.sprite != null && !AssetDatabase.Contains(i.sprite)).ToArray();
            if (images.Length == 0) return;
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null)
            {
                sprite = images[0].sprite;
                AssetDatabase.CreateAsset(sprite.texture, path);
                AssetDatabase.AddObjectToAsset(sprite, path);
                AssetDatabase.SaveAssets();
            }
            foreach (var image in images) image.sprite = sprite;
        }
    }
}
