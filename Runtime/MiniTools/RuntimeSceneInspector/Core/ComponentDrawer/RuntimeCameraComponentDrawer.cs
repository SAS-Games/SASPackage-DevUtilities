using UnityEngine;

namespace HP.Utilities.RuntimeSceneInspector.Core
{
    internal sealed class RuntimeCameraComponentDrawer : RuntimeComponentDrawer<Camera>
    {
        public RuntimeCameraComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.orthographic", "Orthographic", camera => camera.orthographic, (camera, value) => camera.orthographic = value);
            Add("@unity.fieldOfView", "Field Of View", camera => camera.fieldOfView, (camera, value) => camera.fieldOfView = value, camera => !camera.orthographic, (_, value) => value > 0f && value < 180f ? null : "Field of view must be greater than 0 and less than 180 degrees.");
            Add("@unity.orthographicSize", "Orthographic Size", camera => camera.orthographicSize, (camera, value) => camera.orthographicSize = value, camera => camera.orthographic, (_, value) => RequirePositive(value, "Orthographic size"));
            Add("@unity.nearClipPlane", "Near Clip Plane", camera => camera.nearClipPlane, (camera, value) => camera.nearClipPlane = value, validator: (_, value) => RequirePositive(value, "Near clip plane"));
            Add("@unity.farClipPlane", "Far Clip Plane", camera => camera.farClipPlane, (camera, value) => camera.farClipPlane = value, validator: (camera, value) => value > camera.nearClipPlane ? null : "Far clip plane must be greater than the near clip plane.");
            Add("@unity.depth", "Depth", camera => camera.depth, (camera, value) => camera.depth = value);
            Add("@unity.cullingMask", "Culling Mask", camera => camera.cullingMask, (camera, value) => camera.cullingMask = value);
            Add("@unity.clearFlags", "Clear Flags", camera => camera.clearFlags, (camera, value) => camera.clearFlags = value);
            Add("@unity.backgroundColor", "Background Color", camera => camera.backgroundColor, (camera, value) => camera.backgroundColor = value);
            Add("@unity.viewportRect", "Viewport Rect", camera => camera.rect, (camera, value) => camera.rect = value);
            Add("@unity.targetDisplay", "Target Display", camera => camera.targetDisplay, (camera, value) => camera.targetDisplay = value, validator: (_, value) => value >= 0 ? null : "Target display cannot be negative.");
            Add("@unity.allowHDR", "Allow HDR", camera => camera.allowHDR, (camera, value) => camera.allowHDR = value);
            Add("@unity.allowMSAA", "Allow MSAA", camera => camera.allowMSAA, (camera, value) => camera.allowMSAA = value);
            Add("@unity.useOcclusionCulling", "Use Occlusion Culling", camera => camera.useOcclusionCulling, (camera, value) => camera.useOcclusionCulling = value);
            AddReadOnly("@unity.pixelWidth", "Pixel Width", camera => camera.pixelWidth);
            AddReadOnly("@unity.pixelHeight", "Pixel Height", camera => camera.pixelHeight);
            AddReadOnly("@unity.cameraType", "Camera Type", camera => camera.cameraType);
        }
    }
}
