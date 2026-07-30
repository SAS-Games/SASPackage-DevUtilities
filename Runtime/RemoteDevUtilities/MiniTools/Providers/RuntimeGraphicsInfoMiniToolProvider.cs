using SAS.DevUtilities;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimeGraphicsInfoMiniToolProvider :
        MiniToolDataProvider<GraphicsInfoSnapshot>,
        IMiniToolFieldProvider
    {
        public override bool TryGetSnapshot(
            out GraphicsInfoSnapshot snapshot)
        {
            snapshot = CaptureSnapshot();
            return true;
        }

        public RemoteMiniToolField[] CaptureFields()
        {
            GraphicsInfoSnapshot snapshot = CaptureSnapshot();
            return new[]
            {
                Field(
                    "device",
                    "Graphics Device",
                    snapshot.GraphicsDeviceName),
                Field(
                    "vendor",
                    "Vendor",
                    snapshot.GraphicsDeviceVendor),
                Field(
                    "api",
                    "Graphics API",
                    snapshot.GraphicsApi),
                Field(
                    "version",
                    "Driver/API Version",
                    snapshot.GraphicsDeviceVersion),
                Field(
                    "memory",
                    "Graphics Memory",
                    snapshot.GraphicsMemorySizeMb.ToString(),
                    "MiB"),
                Field(
                    "shaderLevel",
                    "Shader Level",
                    snapshot.GraphicsShaderLevel.ToString()),
                Field(
                    "maxTexture",
                    "Max Texture Size",
                    snapshot.MaxTextureSize.ToString(),
                    "px"),
                Field(
                    "compute",
                    "Compute Shaders",
                    snapshot.SupportsComputeShaders.ToString()),
                Field(
                    "instancing",
                    "GPU Instancing",
                    snapshot.SupportsInstancing.ToString()),
                Field(
                    "rayTracing",
                    "Ray Tracing",
                    snapshot.SupportsRayTracing.ToString())
            };
        }

        private static GraphicsInfoSnapshot CaptureSnapshot()
        {
            return GraphicsInfoSnapshotProvider
                .TryGetRequestedSnapshot(out GraphicsInfoSnapshot snapshot)
                    ? snapshot
                    : GraphicsInfoSnapshotCollector.Capture(false);
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
