using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "Camera Command", menuName = DeveloperConsole.CommandBasePath + "Camera Command")]
    public sealed class CameraConsoleCommand : CompositeConsoleCommand
    {
        [SerializeField] private string m_HelpText = "Lists cameras and temporarily changes camera rendering settings.";
        public override string HelpText => m_HelpText;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        private static readonly FieldInfo RendererIndexField = typeof(UniversalAdditionalCameraData)
            .GetField("m_RendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo RendererDataListField = typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly Dictionary<Camera, bool> _occlusionStates = new();
        private readonly Dictionary<UniversalAdditionalCameraData, bool> _shadowStates = new();
        private readonly Dictionary<UniversalAdditionalCameraData, bool> _postProcessingStates = new();
        private readonly Dictionary<UniversalAdditionalCameraData, int> _rendererStates = new();

        public override bool Contains(string commandName)
        {
            return GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset && base.Contains(commandName);
        }

        private bool List(string[] args)
        {
            if (!HasNoArguments(args))
                return false;
            Camera[] cameras = GetAllCameras();
            string report = cameras.Length == 0
                ? "[Camera.List] No loaded cameras."
                : "[Camera.List]\n" + string.Join("\n", cameras.OrderBy(camera => camera.depth).Select(DescribeCamera));
            Debug.Log(report);
            return true;
        }

        private bool Status(string[] args)
        {
            if (args == null)
                return false;
            Camera[] cameras = SelectCameras(args, 0);
            if (cameras.Length == 0)
                return false;
            Debug.Log("[Camera.Status]\n" + string.Join("\n", cameras.Select(DescribeCameraDetailed)));
            return true;
        }

        private bool Stack(string[] args)
        {
            if (!HasNoArguments(args))
                return false;
            StringBuilder builder = new StringBuilder("[Camera.Stack]");
            int baseCameraCount = 0;
            foreach (Camera camera in GetAllCameras())
            {
                if (!camera.TryGetComponent(out UniversalAdditionalCameraData data) || data.renderType != CameraRenderType.Base)
                    continue;
                baseCameraCount++;
                List<Camera> stack = data.cameraStack;
                builder.Append("\n- ").Append(camera.name).Append(" -> ");
                builder.Append(stack == null || stack.Count == 0
                    ? "(empty)"
                    : string.Join(", ", stack.Select(overlay => overlay == null ? "Missing" : overlay.name)));
            }
            if (baseCameraCount == 0)
                builder.Append(" No URP Base cameras found.");
            Debug.Log(builder.ToString());
            return true;
        }

        private bool SetOcclusionCulling(string[] args)
        {
            if (!TrySelectForToggle(args, out bool value, out Camera[] cameras))
                return false;
            foreach (Camera camera in cameras)
            {
                _occlusionStates.TryAdd(camera, camera.useOcclusionCulling);
                camera.useOcclusionCulling = value;
            }
            Debug.Log($"[Camera] Occlusion culling={value} on {cameras.Length} camera(s).");
            return true;
        }

        private bool SetRenderShadows(string[] args)
        {
            if (!TrySelectForToggle(args, out bool value, out Camera[] cameras))
                return false;
            int changed = 0;
            foreach (Camera camera in cameras)
            {
                if (!camera.TryGetComponent(out UniversalAdditionalCameraData data))
                    continue;
                _shadowStates.TryAdd(data, data.renderShadows);
                data.renderShadows = value;
                changed++;
            }
            Debug.Log($"[Camera] Shadow rendering={value} on {changed} camera(s).");
            return changed > 0;
        }

        private bool SetPostProcessing(string[] args)
        {
            if (!TrySelectForToggle(args, out bool value, out Camera[] cameras))
                return false;
            int changed = 0;
            foreach (Camera camera in cameras)
            {
                if (!camera.TryGetComponent(out UniversalAdditionalCameraData data))
                    continue;
                _postProcessingStates.TryAdd(data, data.renderPostProcessing);
                data.renderPostProcessing = value;
                changed++;
            }
            Debug.Log($"[Camera] Post-processing={value} on {changed} camera(s).");
            return changed > 0;
        }

        private bool SetRenderer(string[] args)
        {
            if (args == null || args.Length < 1 || !int.TryParse(args[0], out int rendererIndex) || rendererIndex < -1)
                return false;
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset urp || !IsValidRendererIndex(urp, rendererIndex))
                return false;

            Camera[] cameras = SelectCameras(args, 1);
            int changed = 0;
            foreach (Camera camera in cameras)
            {
                if (!camera.TryGetComponent(out UniversalAdditionalCameraData data) || !TryGetRendererIndex(data, out int originalIndex))
                    continue;
                _rendererStates.TryAdd(data, originalIndex);
                data.SetRenderer(rendererIndex);
                changed++;
            }
            Debug.Log($"[Camera] Renderer index={rendererIndex} on {changed} camera(s). Use -1 for the pipeline default.");
            return changed > 0;
        }

        private bool Restore(string[] args)
        {
            if (!HasNoArguments(args))
                return false;
            int count = _occlusionStates.Count + _shadowStates.Count + _postProcessingStates.Count + _rendererStates.Count;
            foreach (KeyValuePair<Camera, bool> entry in _occlusionStates)
                if (entry.Key != null) entry.Key.useOcclusionCulling = entry.Value;
            foreach (KeyValuePair<UniversalAdditionalCameraData, bool> entry in _shadowStates)
                if (entry.Key != null) entry.Key.renderShadows = entry.Value;
            foreach (KeyValuePair<UniversalAdditionalCameraData, bool> entry in _postProcessingStates)
                if (entry.Key != null) entry.Key.renderPostProcessing = entry.Value;
            foreach (KeyValuePair<UniversalAdditionalCameraData, int> entry in _rendererStates)
                if (entry.Key != null) entry.Key.SetRenderer(entry.Value);
            _occlusionStates.Clear();
            _shadowStates.Clear();
            _postProcessingStates.Clear();
            _rendererStates.Clear();
            Debug.Log($"[Camera.Restore] Restored {count} captured camera setting(s).");
            return true;
        }

        private static string DescribeCamera(Camera camera)
        {
            string target = camera.targetTexture == null ? "Screen" : camera.targetTexture.name;
            return $"- {camera.name}: enabled={camera.enabled}, active={camera.gameObject.activeInHierarchy}, depth={camera.depth:F1}, target={target}";
        }

        private static string DescribeCameraDetailed(Camera camera)
        {
            string urpState = "URP data=missing";
            if (camera.TryGetComponent(out UniversalAdditionalCameraData data))
            {
                TryGetRendererIndex(data, out int rendererIndex);
                urpState = $"type={data.renderType}, renderer={rendererIndex}, shadows={data.renderShadows}, PP={data.renderPostProcessing}, depthTexture={data.requiresDepthTexture}, colorTexture={data.requiresColorTexture}";
            }
            return $"- {camera.name}: enabled={camera.enabled}, active={camera.gameObject.activeInHierarchy}, depth={camera.depth:F1}, cullingMask=0x{camera.cullingMask:X8}, occlusion={camera.useOcclusionCulling}, HDR={camera.allowHDR}, dynamicResolution={camera.allowDynamicResolution}, {urpState}";
        }

        private static bool TrySelectForToggle(string[] args, out bool value, out Camera[] cameras)
        {
            value = false;
            cameras = Array.Empty<Camera>();
            if (args == null || args.Length < 1 || !BoolUtil.TryParse(args[0], out value))
                return false;
            cameras = SelectCameras(args, 1);
            return cameras.Length > 0;
        }

        private static Camera[] SelectCameras(string[] args, int selectorStart)
        {
            Camera[] cameras = GetAllCameras();
            if (args.Length <= selectorStart)
                return cameras;
            string selector = string.Join(" ", args, selectorStart, args.Length - selectorStart);
            if (selector.Equals("all", StringComparison.OrdinalIgnoreCase))
                return cameras;
            return cameras.Where(camera => camera.name.Equals(selector, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        private static Camera[] GetAllCameras() => FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        private static bool HasNoArguments(string[] args) => args != null && args.Length == 0;

        private static bool TryGetRendererIndex(UniversalAdditionalCameraData data, out int rendererIndex)
        {
            rendererIndex = -1;
            if (RendererIndexField == null)
                return false;
            rendererIndex = (int)RendererIndexField.GetValue(data);
            return true;
        }

        private static bool IsValidRendererIndex(UniversalRenderPipelineAsset urp, int rendererIndex)
        {
            if (rendererIndex == -1)
                return true;
            ScriptableRendererData[] rendererData = RendererDataListField?.GetValue(urp) as ScriptableRendererData[];
            return rendererData != null && rendererIndex < rendererData.Length && rendererData[rendererIndex] != null;
        }
#else
        public override string Name => string.Empty;
        public override string[] Presets => Array.Empty<string>();
#endif
    }
}
