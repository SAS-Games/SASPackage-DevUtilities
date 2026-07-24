using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

namespace SAS.Utilities.DeveloperConsole
{
    internal static class RuntimeRenderingReporter
    {
        private const double BytesPerMebibyte = 1048576d;

        public static string BuildTextureReport(int topCount)
        {
            Texture[] textures = Resources.FindObjectsOfTypeAll<Texture>()
                .Where(texture => texture != null && texture is not RenderTexture)
                .OrderByDescending(Profiler.GetRuntimeMemorySizeLong)
                .Take(topCount)
                .ToArray();

            StringBuilder builder = new StringBuilder(512);
            builder.Append("[Rendering.Textures] Top ").Append(textures.Length).Append(" loaded textures by runtime memory:");
            foreach (Texture texture in textures)
            {
                builder.Append("\n- ").Append(texture.name)
                    .Append(": ").Append(texture.width).Append('x').Append(texture.height)
                    .Append(", ").Append(texture.GetType().Name)
                    .Append(", ").Append(FormatMemory(Profiler.GetRuntimeMemorySizeLong(texture)));
            }
            return builder.ToString();
        }

        public static string BuildRenderTargetReport(int topCount)
        {
            RenderTexture[] targets = Resources.FindObjectsOfTypeAll<RenderTexture>()
                .Where(target => target != null)
                .OrderByDescending(Profiler.GetRuntimeMemorySizeLong)
                .Take(topCount)
                .ToArray();

            StringBuilder builder = new StringBuilder(512);
            builder.Append("[Rendering.RenderTargets] Top ").Append(targets.Length).Append(" loaded render textures by runtime memory:");
            foreach (RenderTexture target in targets)
            {
                builder.Append("\n- ").Append(target.name)
                    .Append(": ").Append(target.width).Append('x').Append(target.height)
                    .Append(", depth=").Append(target.depth)
                    .Append(", MSAA=").Append(target.antiAliasing)
                    .Append(", format=").Append(target.graphicsFormat)
                    .Append(", created=").Append(target.IsCreated())
                    .Append(", ").Append(FormatMemory(Profiler.GetRuntimeMemorySizeLong(target)));
            }
            return builder.ToString();
        }

        public static string BuildMaterialReport(int topCount)
        {
            Material[] materials = Resources.FindObjectsOfTypeAll<Material>()
                .Where(material => material != null)
                .OrderByDescending(Profiler.GetRuntimeMemorySizeLong)
                .Take(topCount)
                .ToArray();

            StringBuilder builder = new StringBuilder(512);
            builder.Append("[Rendering.Materials] Top ").Append(materials.Length).Append(" loaded materials by runtime memory:");
            foreach (Material material in materials)
            {
                builder.Append("\n- ").Append(material.name)
                    .Append(": shader=").Append(material.shader == null ? "None" : material.shader.name)
                    .Append(", passes=").Append(material.passCount)
                    .Append(", keywords=").Append(material.shaderKeywords.Length)
                    .Append(", queue=").Append(material.renderQueue)
                    .Append(", ").Append(FormatMemory(Profiler.GetRuntimeMemorySizeLong(material)));
            }
            return builder.ToString();
        }

        public static string BuildShaderReport()
        {
            Shader[] shaders = Resources.FindObjectsOfTypeAll<Shader>().Where(shader => shader != null).ToArray();
            Material[] materials = Resources.FindObjectsOfTypeAll<Material>().Where(material => material != null).ToArray();
            IGrouping<Shader, Material>[] usage = materials
                .Where(material => material.shader != null)
                .GroupBy(material => material.shader)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key.name)
                .ToArray();

            int enabledKeywordCount = materials.Sum(material => material.shaderKeywords.Length);
            StringBuilder builder = new StringBuilder(768);
            builder.Append("[Rendering.Shaders] Loaded shaders=").Append(shaders.Length)
                .Append(", loaded materials=").Append(materials.Length)
                .Append(", enabled material keywords=").Append(enabledKeywordCount)
                .Append("\nMost-used shaders:");
            foreach (IGrouping<Shader, Material> group in usage.Take(20))
            {
                builder.Append("\n- ").Append(group.Key.name)
                    .Append(": materials=").Append(group.Count())
                    .Append(", passes=").Append(group.Key.passCount)
                    .Append(", maxLOD=").Append(group.Key.maximumLOD);
            }
            return builder.ToString();
        }

        public static string BuildStatusReport()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            StringBuilder builder = new StringBuilder(384);
            builder.Append("[Rendering.Status] Pipeline=")
                .Append(pipeline == null ? "Built-in" : pipeline.name)
                .Append(" (").Append(pipeline == null ? "BuiltInRenderPipeline" : pipeline.GetType().Name).Append(')')
                .Append(", Quality=").Append(QualitySettings.names[QualitySettings.GetQualityLevel()])
                .Append(", Resolution=").Append(Screen.width).Append('x').Append(Screen.height)
                .Append(", Mode=").Append(Screen.fullScreenMode)
                .Append(", ColorSpace=").Append(QualitySettings.activeColorSpace)
                .Append(", Cameras=").Append(cameras.Length)
                .Append(", VSync=").Append(QualitySettings.vSyncCount)
                .Append(", TargetFPS=").Append(Application.targetFrameRate);
#if UNITY_RENDER_PIPELINE_UNIVERSAL
            if (pipeline is UniversalRenderPipelineAsset urp)
            {
                int postProcessingCameras = cameras.Count(camera =>
                    camera.TryGetComponent(out UniversalAdditionalCameraData data) && data.renderPostProcessing);
                builder.Append("\nURP: RenderScale=").Append(urp.renderScale.ToString("F2"))
                    .Append(", MSAA=").Append(urp.msaaSampleCount == 1 ? "Off" : urp.msaaSampleCount.ToString())
                    .Append(", HDR=").Append(urp.supportsHDR)
                    .Append(", MainShadows=").Append(urp.supportsMainLightShadows)
                    .Append(", AdditionalShadows=").Append(urp.supportsAdditionalLightShadows)
                    .Append(", ShadowDistance=").Append(urp.shadowDistance.ToString("F1"))
                    .Append(", PostProcessingCameras=").Append(postProcessingCameras).Append('/').Append(cameras.Length);
            }
#endif
            return builder.ToString();
        }

        private static string FormatMemory(long bytes) => $"{bytes / BytesPerMebibyte:F2} MiB";
    }
}
