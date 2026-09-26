using UnityEngine;
namespace Sokoban.Runtime.Presentation.App
{
    // The EditorOnly root is stripped from builds; in editor Play Mode it must not cover the real UI.
    public sealed class EditorScenePreview : MonoBehaviour
    {
        private void Awake() => gameObject.SetActive(false);
    }
}
