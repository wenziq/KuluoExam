using UnityEngine;
namespace Sokoban.Runtime.Presentation.App
{
    public static class UiBackgroundCamera
    {
        public static Camera Ensure(Transform parent, Color background)
        {
            foreach (var camera in Camera.allCameras)
                if (camera.cameraType == CameraType.Game && camera.targetDisplay == 0 && camera.targetTexture == null)
                    return camera;
            return Create(parent, background);
        }

        public static Camera Create(Transform parent, Color background)
        {
            var camera = new GameObject("UIBackgroundCamera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(parent, false);
            camera.transform.localPosition = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            // Overlay UI draws after this camera; no scene geometry or extra audio listener is needed.
            camera.cullingMask = 0;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            return camera;
        }
    }
}
