using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(menuName = DeveloperConsole.CommandBasePath + "URP Console Command")]
    public class URPConsoleCommand : CompositeConsoleCommand
    {
        [SerializeField] private string m_HelpText = "Commands for inspecting and modifying the active URP asset at runtime.";
        public override string HelpText => m_HelpText;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        private static readonly BindingFlags PropertyFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private UniversalRenderPipelineAsset _trackedAsset;
        private float? _renderScale;
        private UpscalingFilterSelection? _upscalingFilter;
        private int? _msaa;
        private bool? _hdr;
        private float? _lodBias;
        private int? _vSync;
        private int? _textureLimit;
        private float? _shadowDistance;
        private int? _shadowCascades;
        private bool? _mainLightShadows;
        private bool? _additionalLightShadows;
        private bool? _softShadows;
        private int? _mainShadowAtlas;
        private int? _additionalShadowAtlas;
        private float? _shadowDepthBias;
        private float? _shadowNormalBias;
        private ColorGradingMode? _colorGradingMode;
        private int? _lutSize;
        private readonly Dictionary<UniversalAdditionalCameraData, bool> _cameraPostProcessing = new();
        private readonly Dictionary<Volume, float> _volumeWeights = new();

        private static UniversalRenderPipelineAsset CurrentURPAsset =>
            GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        public override bool Contains(string commandName)
        {
            return CurrentURPAsset != null && base.Contains(commandName);
        }

        private bool Status(string[] args)
        {
            if (!HasExactArguments(args, 0))
                return false;

            UniversalRenderPipelineAsset urp = CurrentURPAsset;
            if (urp == null)
                return false;

            if (Name.Equals("Shadow", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log(
                    $"[Shadow] Asset={urp.name}, Enabled={urp.supportsMainLightShadows || urp.supportsAdditionalLightShadows}, " +
                    $"Main={urp.supportsMainLightShadows}, Additional={urp.supportsAdditionalLightShadows}, Soft={urp.supportsSoftShadows}, " +
                    $"Distance={urp.shadowDistance:F2}, Cascades={urp.shadowCascadeCount}, MainAtlas={urp.mainLightShadowmapResolution}, " +
                    $"AdditionalAtlas={urp.additionalLightsShadowmapResolution}, DepthBias={urp.shadowDepthBias:F3}, NormalBias={urp.shadowNormalBias:F3}");
                return true;
            }

            if (Name.Equals("PP", StringComparison.OrdinalIgnoreCase))
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                int enabledCameras = cameras.Count(camera =>
                    camera.TryGetComponent(out UniversalAdditionalCameraData data) && data.renderPostProcessing);
                Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Debug.Log(
                    $"[PP] Asset={urp.name}, ColorGrading={urp.colorGradingMode}, LUT={urp.colorGradingLutSize}, " +
                    $"Cameras={enabledCameras}/{cameras.Length} enabled, Volumes={volumes.Count(volume => volume.enabled && volume.weight > 0f)}/{volumes.Length} active");
                return true;
            }

            Debug.Log(
                $"[Quality] Asset={urp.name}, RenderScale={urp.renderScale:F2}, Upscaler={urp.upscalingFilter}, " +
                $"MSAA={(urp.msaaSampleCount == 1 ? "Off" : urp.msaaSampleCount.ToString())}, HDR={urp.supportsHDR}, " +
                $"LODBias={QualitySettings.lodBias:F2}, VSync={QualitySettings.vSyncCount}, TextureMipmapLimit={QualitySettings.globalTextureMipmapLimit}");
            return true;
        }

        private bool Restore(string[] args)
        {
            if (!HasExactArguments(args, 0))
                return false;

            if (_trackedAsset != null)
            {
                if (_renderScale.HasValue) _trackedAsset.renderScale = _renderScale.Value;
                if (_upscalingFilter.HasValue) _trackedAsset.upscalingFilter = _upscalingFilter.Value;
                if (_msaa.HasValue) _trackedAsset.msaaSampleCount = _msaa.Value;
                if (_hdr.HasValue) _trackedAsset.supportsHDR = _hdr.Value;
                if (_shadowDistance.HasValue) _trackedAsset.shadowDistance = _shadowDistance.Value;
                if (_shadowCascades.HasValue) _trackedAsset.shadowCascadeCount = _shadowCascades.Value;
                if (_mainLightShadows.HasValue) TrySetNonPublicBool(_trackedAsset, nameof(UniversalRenderPipelineAsset.supportsMainLightShadows), _mainLightShadows.Value);
                if (_additionalLightShadows.HasValue) TrySetNonPublicBool(_trackedAsset, nameof(UniversalRenderPipelineAsset.supportsAdditionalLightShadows), _additionalLightShadows.Value);
                if (_softShadows.HasValue) TrySetNonPublicBool(_trackedAsset, nameof(UniversalRenderPipelineAsset.supportsSoftShadows), _softShadows.Value);
                if (_mainShadowAtlas.HasValue) _trackedAsset.mainLightShadowmapResolution = _mainShadowAtlas.Value;
                if (_additionalShadowAtlas.HasValue) _trackedAsset.additionalLightsShadowmapResolution = _additionalShadowAtlas.Value;
                if (_shadowDepthBias.HasValue) _trackedAsset.shadowDepthBias = _shadowDepthBias.Value;
                if (_shadowNormalBias.HasValue) _trackedAsset.shadowNormalBias = _shadowNormalBias.Value;
                if (_colorGradingMode.HasValue) _trackedAsset.colorGradingMode = _colorGradingMode.Value;
                if (_lutSize.HasValue) _trackedAsset.colorGradingLutSize = _lutSize.Value;
            }

            if (_lodBias.HasValue) QualitySettings.lodBias = _lodBias.Value;
            if (_vSync.HasValue) QualitySettings.vSyncCount = _vSync.Value;
            if (_textureLimit.HasValue) QualitySettings.globalTextureMipmapLimit = _textureLimit.Value;

            foreach (KeyValuePair<UniversalAdditionalCameraData, bool> entry in _cameraPostProcessing)
                if (entry.Key != null) entry.Key.renderPostProcessing = entry.Value;
            foreach (KeyValuePair<Volume, float> entry in _volumeWeights)
                if (entry.Key != null) entry.Key.weight = entry.Value;

            ClearSnapshot();
            Debug.Log($"[{Name}] Restored captured runtime settings.");
            return true;
        }

        private bool SetRenderScale(string[] args)
        {
            if (!TryParseFloat(args, out float value) || value < 0.1f || value > 2f || !TryTrackAsset(out UniversalRenderPipelineAsset urp))
                return false;
            _renderScale ??= urp.renderScale;
            urp.renderScale = value;
            return true;
        }

        private bool SetUpscalingFilter(string[] args)
        {
            if (!HasExactArguments(args, 1) || !Enum.TryParse(args[0], true, out UpscalingFilterSelection value) || !TryTrackAsset(out UniversalRenderPipelineAsset urp))
                return false;
            _upscalingFilter ??= urp.upscalingFilter;
            urp.upscalingFilter = value;
            return true;
        }

        private bool SetMSAA(string[] args)
        {
            if (!TryParseInt(args, out int value) || (value != 0 && value != 1 && value != 2 && value != 4 && value != 8) || !TryTrackAsset(out UniversalRenderPipelineAsset urp))
                return false;
            _msaa ??= urp.msaaSampleCount;
            urp.msaaSampleCount = value == 0 ? 1 : value;
            return true;
        }

        private bool SetHDR(string[] args)
        {
            if (!TryParseBool(args, out bool value) || !TryTrackAsset(out UniversalRenderPipelineAsset urp))
                return false;
            _hdr ??= urp.supportsHDR;
            urp.supportsHDR = value;
            return true;
        }

        private bool SetLOD(string[] args)
        {
            if (!TryParseFloat(args, out float value) || value <= 0f)
                return false;
            _lodBias ??= QualitySettings.lodBias;
            QualitySettings.lodBias = value;
            return true;
        }

        private bool SetVSync(string[] args)
        {
            if (!TryParseInt(args, out int value) || value < 0 || value > 4)
                return false;
            _vSync ??= QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = value;
            return true;
        }

        private bool SetTextureQuality(string[] args)
        {
            if (!TryParseInt(args, out int value) || value < 0 || value > 3)
                return false;
            _textureLimit ??= QualitySettings.globalTextureMipmapLimit;
            QualitySettings.globalTextureMipmapLimit = value;
            return true;
        }

        private bool SetShadowDistance(string[] args)
        {
            if (!TryParseFloat(args, out float value) || value < 0f || !TryTrackAsset(out UniversalRenderPipelineAsset urp))
                return false;
            _shadowDistance ??= urp.shadowDistance;
            urp.shadowDistance = value;
            return true;
        }

        private bool SetShadowCascadeCount(string[] args)
        {
            if (!TryParseInt(args, out int value) || value < 1 || value > 4 || !TryTrackAsset(out UniversalRenderPipelineAsset urp))
                return false;
            _shadowCascades ??= urp.shadowCascadeCount;
            urp.shadowCascadeCount = value;
            return true;
        }

        private bool SetEnabled(string[] args)
        {
            if (!TryParseBool(args, out bool value) || !TryTrackAsset(out UniversalRenderPipelineAsset urp))
                return false;
            _mainLightShadows ??= urp.supportsMainLightShadows;
            _additionalLightShadows ??= urp.supportsAdditionalLightShadows;
            return TrySetNonPublicBool(urp, nameof(UniversalRenderPipelineAsset.supportsMainLightShadows), value) &&
                   TrySetNonPublicBool(urp, nameof(UniversalRenderPipelineAsset.supportsAdditionalLightShadows), value);
        }

        private bool SetMainLight(string[] args) => SetShadowToggle(args, nameof(UniversalRenderPipelineAsset.supportsMainLightShadows), ref _mainLightShadows);
        private bool SetAdditionalLights(string[] args) => SetShadowToggle(args, nameof(UniversalRenderPipelineAsset.supportsAdditionalLightShadows), ref _additionalLightShadows);
        private bool SetSoftShadows(string[] args) => SetShadowToggle(args, nameof(UniversalRenderPipelineAsset.supportsSoftShadows), ref _softShadows);

        private bool SetShadowToggle(string[] args, string propertyName, ref bool? snapshot)
        {
            if (!TryParseBool(args, out bool value) || !TryTrackAsset(out UniversalRenderPipelineAsset urp))
                return false;
            PropertyInfo property = typeof(UniversalRenderPipelineAsset).GetProperty(propertyName, PropertyFlags);
            if (property == null)
                return false;
            snapshot ??= (bool)property.GetValue(urp);
            return TrySetNonPublicBool(urp, propertyName, value);
        }

        private bool SetMainAtlas(string[] args)
        {
            if (!TryParseShadowResolution(args, out int value) || !TryTrackAsset(out UniversalRenderPipelineAsset urp))
                return false;
            _mainShadowAtlas ??= urp.mainLightShadowmapResolution;
            urp.mainLightShadowmapResolution = value;
            return true;
        }

        private bool SetAdditionalAtlas(string[] args)
        {
            if (!TryParseShadowResolution(args, out int value) || !TryTrackAsset(out UniversalRenderPipelineAsset urp))
                return false;
            _additionalShadowAtlas ??= urp.additionalLightsShadowmapResolution;
            urp.additionalLightsShadowmapResolution = value;
            return true;
        }

        private bool SetBias(string[] args)
        {
            if (!HasExactArguments(args, 2) ||
                !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float depth) ||
                !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float normal) ||
                depth < 0f || depth > 10f || normal < 0f || normal > 10f ||
                !TryTrackAsset(out UniversalRenderPipelineAsset urp))
                return false;
            _shadowDepthBias ??= urp.shadowDepthBias;
            _shadowNormalBias ??= urp.shadowNormalBias;
            urp.shadowDepthBias = depth;
            urp.shadowNormalBias = normal;
            return true;
        }

        private bool SetColorGradingMode(string[] args)
        {
            if (!HasExactArguments(args, 1) || !Enum.TryParse(args[0], true, out ColorGradingMode value) || !TryTrackAsset(out UniversalRenderPipelineAsset urp))
                return false;
            _colorGradingMode ??= urp.colorGradingMode;
            urp.colorGradingMode = value;
            return true;
        }

        private bool SetLUTSize(string[] args)
        {
            if (!TryParseInt(args, out int value) || value < 16 || value > 65 || !TryTrackAsset(out UniversalRenderPipelineAsset urp))
                return false;
            _lutSize ??= urp.colorGradingLutSize;
            urp.colorGradingLutSize = value;
            return true;
        }

        private bool SetPostProcessingEnabled(string[] args)
        {
            if (args == null || args.Length < 1 || !BoolUtil.TryParse(args[0], out bool value) || CurrentURPAsset == null)
                return false;

            string selector = args.Length == 1 ? "all" : string.Join(" ", args, 1, args.Length - 1);
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int changed = 0;
            foreach (Camera camera in cameras)
            {
                if (!selector.Equals("all", StringComparison.OrdinalIgnoreCase) && !camera.name.Equals(selector, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!camera.TryGetComponent(out UniversalAdditionalCameraData data))
                    continue;
                _cameraPostProcessing.TryAdd(data, data.renderPostProcessing);
                data.renderPostProcessing = value;
                changed++;
            }

            Debug.Log($"[PP] Post-processing {(value ? "enabled" : "disabled")} on {changed} camera(s).");
            return changed > 0;
        }

        private bool VolumeList(string[] args)
        {
            if (!HasExactArguments(args, 0))
                return false;
            Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            string report = volumes.Length == 0
                ? "[PP] No loaded Volumes."
                : "[PP] Volumes:\n" + string.Join("\n", volumes.OrderBy(volume => volume.name).Select(volume =>
                    $"- {volume.name}: enabled={volume.enabled}, global={volume.isGlobal}, priority={volume.priority:F1}, weight={volume.weight:F2}, profile={(volume.sharedProfile == null ? "None" : volume.sharedProfile.name)}"));
            Debug.Log(report);
            return true;
        }

        private bool SetVolumeWeight(string[] args)
        {
            if (args == null || args.Length < 2 ||
                !float.TryParse(args[^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || value < 0f || value > 1f)
                return false;

            string volumeName = string.Join(" ", args, 0, args.Length - 1);
            Volume[] matches = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(volume => volume.name.Equals(volumeName, StringComparison.OrdinalIgnoreCase)).ToArray();
            foreach (Volume volume in matches)
            {
                _volumeWeights.TryAdd(volume, volume.weight);
                volume.weight = value;
            }
            return matches.Length > 0;
        }

        private bool TryTrackAsset(out UniversalRenderPipelineAsset urp)
        {
            urp = CurrentURPAsset;
            if (urp == null)
                return false;
            if (_trackedAsset != null && _trackedAsset != urp)
            {
                Debug.LogWarning($"[{Name}] Active URP asset changed. Run {Name}.Restore before modifying the new asset.");
                return false;
            }
            _trackedAsset ??= urp;
            return true;
        }

        private static bool TrySetNonPublicBool(UniversalRenderPipelineAsset urp, string propertyName, bool value)
        {
            try
            {
                PropertyInfo property = typeof(UniversalRenderPipelineAsset).GetProperty(propertyName, PropertyFlags);
                MethodInfo setter = property?.GetSetMethod(true);
                if (setter == null)
                    return false;
                setter.Invoke(urp, new object[] { value });
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Shadow] Could not set {propertyName}: {exception.GetBaseException().Message}");
                return false;
            }
        }

        private void ClearSnapshot()
        {
            _trackedAsset = null;
            _renderScale = null;
            _upscalingFilter = null;
            _msaa = null;
            _hdr = null;
            _lodBias = null;
            _vSync = null;
            _textureLimit = null;
            _shadowDistance = null;
            _shadowCascades = null;
            _mainLightShadows = null;
            _additionalLightShadows = null;
            _softShadows = null;
            _mainShadowAtlas = null;
            _additionalShadowAtlas = null;
            _shadowDepthBias = null;
            _shadowNormalBias = null;
            _colorGradingMode = null;
            _lutSize = null;
            _cameraPostProcessing.Clear();
            _volumeWeights.Clear();
        }

        private static bool HasExactArguments(string[] args, int count) => args != null && args.Length == count;
        private static bool TryParseBool(string[] args, out bool value)
        {
            value = false;
            return HasExactArguments(args, 1) && BoolUtil.TryParse(args[0], out value);
        }

        private static bool TryParseInt(string[] args, out int value)
        {
            value = 0;
            return HasExactArguments(args, 1) && int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseFloat(string[] args, out float value)
        {
            value = 0f;
            return HasExactArguments(args, 1) && float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseShadowResolution(string[] args, out int value)
        {
            return TryParseInt(args, out value) && (value == 256 || value == 512 || value == 1024 || value == 2048 || value == 4096 || value == 8192);
        }
#else
        public override string Name => string.Empty;
        public override string[] Presets => Array.Empty<string>();
#endif
    }
}
