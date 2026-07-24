using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
#if UNITY_RENDER_PIPELINE_HIGH_DEFINITION
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
#endif

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(menuName = DeveloperConsole.CommandBasePath + "HDRP Quality Command")]
    public class HDRPQualityCommand : CompositeConsoleCommand
    {
        [SerializeField] private string m_HelpText = "Commands for inspecting, modifying, and restoring HDRP quality settings at runtime.";
        public override string HelpText => m_HelpText;

#if UNITY_RENDER_PIPELINE_HIGH_DEFINITION
        private readonly Dictionary<Camera, bool> _cameraDynamicResolution = new();
        private readonly Dictionary<Camera, FilterSnapshot> _cameraFilters = new();
        private bool _hasSettingsSnapshot;
        private RenderPipelineSettings _originalSettings;

        private static readonly FieldInfo CameraFiltersField = typeof(DynamicResolutionHandler)
            .GetField("s_CameraUpscaleFilters", BindingFlags.Static | BindingFlags.NonPublic);

        private readonly struct FilterSnapshot
        {
            public readonly bool HadOverride;
            public readonly DynamicResUpscaleFilter Filter;

            public FilterSnapshot(bool hadOverride, DynamicResUpscaleFilter filter)
            {
                HadOverride = hadOverride;
                Filter = filter;
            }
        }

        private static HDRenderPipelineAsset CurrentHDRPAsset =>
            GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;

        public override bool Contains(string commandName)
        {
            return CurrentHDRPAsset != null && base.Contains(commandName);
        }

        private bool Status(string[] args)
        {
            if (args == null || args.Length != 0 || CurrentHDRPAsset == null)
                return false;
            GlobalDynamicResolutionSettings dynamicResolution = CurrentHDRPAsset.currentPlatformRenderPipelineSettings.dynamicResolutionSettings;
            Debug.Log($"[Quality/HDRP] Asset={CurrentHDRPAsset.name}, DynamicResolution={dynamicResolution.enabled}, Force={dynamicResolution.forceResolution}, ForcedPercentage={dynamicResolution.forcedPercentage:F1}, DefaultUpscaler={dynamicResolution.upsampleFilter}");
            return true;
        }

        private bool SetUpscalingFilter(string[] args)
        {
            if (args == null || args.Length < 1 || CurrentHDRPAsset == null ||
                !Enum.TryParse(args[0], true, out DynamicResUpscaleFilter filter))
                return false;

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (args.Length > 1)
            {
                string selector = string.Join(" ", args, 1, args.Length - 1);
                if (!selector.Equals("all", StringComparison.OrdinalIgnoreCase))
                    cameras = cameras.Where(camera => camera.name.Equals(selector, StringComparison.OrdinalIgnoreCase)).ToArray();
            }
            if (cameras.Length == 0)
                return false;

            foreach (Camera camera in cameras)
            {
                _cameraDynamicResolution.TryAdd(camera, camera.allowDynamicResolution);
                if (!_cameraFilters.ContainsKey(camera))
                    _cameraFilters.Add(camera, CaptureFilter(camera));
                camera.allowDynamicResolution = true;
                DynamicResolutionHandler.SetUpscaleFilter(camera, filter);
            }
            return true;
        }

        private bool SetScreenPercentage(string[] args)
        {
            if (args == null || args.Length != 1 || CurrentHDRPAsset == null ||
                !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float percentage) ||
                percentage < 1f || percentage > 100f)
                return false;

            if (!_hasSettingsSnapshot)
            {
                _originalSettings = CurrentHDRPAsset.currentPlatformRenderPipelineSettings;
                _hasSettingsSnapshot = true;
            }
            RenderPipelineSettings settings = CurrentHDRPAsset.currentPlatformRenderPipelineSettings;
            settings.dynamicResolutionSettings.forceResolution = true;
            settings.dynamicResolutionSettings.forcedPercentage = percentage;
            CurrentHDRPAsset.currentPlatformRenderPipelineSettings = settings;
            return true;
        }

        private bool Restore(string[] args)
        {
            if (args == null || args.Length != 0)
                return false;
            if (_hasSettingsSnapshot && CurrentHDRPAsset != null)
                CurrentHDRPAsset.currentPlatformRenderPipelineSettings = _originalSettings;
            foreach (KeyValuePair<Camera, bool> entry in _cameraDynamicResolution)
                if (entry.Key != null) entry.Key.allowDynamicResolution = entry.Value;
            foreach (KeyValuePair<Camera, FilterSnapshot> entry in _cameraFilters)
                if (entry.Key != null) RestoreFilter(entry.Key, entry.Value);
            _hasSettingsSnapshot = false;
            _cameraDynamicResolution.Clear();
            _cameraFilters.Clear();
            return true;
        }

        private static FilterSnapshot CaptureFilter(Camera camera)
        {
            if (CameraFiltersField?.GetValue(null) is Dictionary<int, DynamicResUpscaleFilter> filters &&
                filters.TryGetValue(camera.GetInstanceID(), out DynamicResUpscaleFilter filter))
                return new FilterSnapshot(true, filter);
            return new FilterSnapshot(false, default);
        }

        private static void RestoreFilter(Camera camera, FilterSnapshot snapshot)
        {
            if (snapshot.HadOverride)
            {
                DynamicResolutionHandler.SetUpscaleFilter(camera, snapshot.Filter);
                return;
            }
            if (CameraFiltersField?.GetValue(null) is Dictionary<int, DynamicResUpscaleFilter> filters)
                filters.Remove(camera.GetInstanceID());
        }
#else
        public override string Name => string.Empty;
        public override string[] Presets => Array.Empty<string>();
#endif
    }
}
