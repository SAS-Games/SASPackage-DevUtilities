using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimeGraphicsInfoMiniToolProvider :
        MiniToolFieldDataProvider
    {
        public override RemoteMiniToolField[] CaptureFields()
        {
            return new[]
            {
                Field("device", "Graphics Device", SystemInfo.graphicsDeviceName),
                Field("vendor", "Vendor", SystemInfo.graphicsDeviceVendor),
                Field("api", "Graphics API", SystemInfo.graphicsDeviceType.ToString()),
                Field("version", "Driver/API Version", SystemInfo.graphicsDeviceVersion),
                Field("memory", "Graphics Memory", SystemInfo.graphicsMemorySize.ToString(), "MiB"),
                Field("shaderLevel", "Shader Level", SystemInfo.graphicsShaderLevel.ToString()),
                Field("maxTexture", "Max Texture Size", SystemInfo.maxTextureSize.ToString(), "px"),
                Field("compute", "Compute Shaders", SystemInfo.supportsComputeShaders.ToString()),
                Field("instancing", "GPU Instancing", SystemInfo.supportsInstancing.ToString()),
                Field("rayTracing", "Ray Tracing", SystemInfo.supportsRayTracing.ToString())
            };
        }

        private static RemoteMiniToolField Field(
            string name,
            string displayName,
            string value,
            string unit = "") =>
            new()
            {
                Name = name,
                DisplayName = displayName,
                Value = value,
                Unit = unit
            };
    }
}
