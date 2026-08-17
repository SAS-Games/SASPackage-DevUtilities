using System;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

namespace HP.DevUtilities
{
    [Serializable]
    public struct GraphicsInfoSnapshot : IMiniToolSnapshot
    {
        public bool Verbose;
        public string GraphicsDeviceName;
        public string GraphicsDeviceVendor;
        public string GraphicsApi;
        public string GraphicsDeviceVersion;
        public int GraphicsMemorySizeMb;
        public int GraphicsShaderLevel;
        public int MaxTextureSize;
        public bool SupportsComputeShaders;
        public bool SupportsInstancing;
        public bool SupportsRayTracing;
        public string QualityName;
        public int VSyncCount;
        public string Shadows;
        public float LodBias;
        public int TargetFrameRate;
        public bool HasRenderScale;
        public float RenderScale;
        public string RenderResolution;
        public string ScreenResolution;
        public string WindowMode;
        public string AntiAliasing;
        public bool HdrEnabled;
        public string AnisotropicFiltering;
    }

    public static class GraphicsInfoSnapshotCollector
    {
        public static GraphicsInfoSnapshot Capture(bool verbose)
        {
            int qualityLevel = QualitySettings.GetQualityLevel();
            string[] qualityNames = QualitySettings.names;
            string qualityName = qualityNames != null && qualityLevel >= 0 && qualityLevel < qualityNames.Length ? qualityNames[qualityLevel] : qualityLevel.ToString();

            string shadows = QualitySettings.shadows.ToString();
            string antiAliasing = QualitySettings.antiAliasing > 0 ? $"{QualitySettings.antiAliasing}x MSAA" : "None";
            bool hdrEnabled = Camera.main != null && Camera.main.allowHDR;
            bool hasRenderScale = false;
            float renderScale = 0f;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                shadows = urp.supportsMainLightShadows ? "Enabled" : "Disabled";
                renderScale = urp.renderScale;
                hasRenderScale = true;
                antiAliasing = urp.msaaSampleCount > 1 ? $"{urp.msaaSampleCount}x MSAA" : "None";
                hdrEnabled = urp.supportsHDR;
            }
#endif

            Resolution resolution = Screen.currentResolution;
            return new GraphicsInfoSnapshot
            {
                Verbose = verbose,
                GraphicsDeviceName = SystemInfo.graphicsDeviceName,
                GraphicsDeviceVendor = SystemInfo.graphicsDeviceVendor,
                GraphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                GraphicsDeviceVersion = SystemInfo.graphicsDeviceVersion,
                GraphicsMemorySizeMb = SystemInfo.graphicsMemorySize,
                GraphicsShaderLevel = SystemInfo.graphicsShaderLevel,
                MaxTextureSize = SystemInfo.maxTextureSize,
                SupportsComputeShaders = SystemInfo.supportsComputeShaders,
                SupportsInstancing = SystemInfo.supportsInstancing,
                SupportsRayTracing = SystemInfo.supportsRayTracing,
                QualityName = qualityName,
                VSyncCount = QualitySettings.vSyncCount,
                Shadows = shadows,
                LodBias = QualitySettings.lodBias,
                TargetFrameRate = Application.targetFrameRate,
                HasRenderScale = hasRenderScale,
                RenderScale = renderScale,
                RenderResolution = $"{Screen.width}x{Screen.height}",
                ScreenResolution = $"{resolution.width}x{resolution.height} @ " + $"{resolution.refreshRateRatio}Hz",
                WindowMode = Screen.fullScreen ? "Fullscreen" : "Windowed",
                AntiAliasing = antiAliasing,
                HdrEnabled = hdrEnabled,
                AnisotropicFiltering = QualitySettings.anisotropicFiltering.ToString()
            };
        }
    }
}
