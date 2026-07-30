using SAS.DevUtilities;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimeGraphicsInfoMiniToolProvider : MiniToolDataProvider<GraphicsInfoSnapshot>, IMiniToolFieldProvider
    {
        public override bool TryGetSnapshot(out GraphicsInfoSnapshot snapshot)
        {
            snapshot = CaptureSnapshot();
            return true;
        }

        public RemoteMiniToolField[] CaptureFields()
        {
            GraphicsInfoSnapshot snapshot = CaptureSnapshot();
            return new[]
            {
                CreateField(
                    "device",
                    "Graphics Device",
                    snapshot.GraphicsDeviceName),
                CreateField(
                    "vendor",
                    "Vendor",
                    snapshot.GraphicsDeviceVendor),
                CreateField(
                    "api",
                    "Graphics API",
                    snapshot.GraphicsApi),
                CreateField(
                    "version",
                    "Driver/API Version",
                    snapshot.GraphicsDeviceVersion),
                CreateField(
                    "memory",
                    "Graphics Memory",
                    snapshot.GraphicsMemorySizeMb.ToString(),
                    "MiB"),
                CreateField(
                    "shaderLevel",
                    "Shader Level",
                    snapshot.GraphicsShaderLevel.ToString()),
                CreateField(
                    "maxTexture",
                    "Max Texture Size",
                    snapshot.MaxTextureSize.ToString(),
                    "px"),
                CreateField(
                    "compute",
                    "Compute Shaders",
                    snapshot.SupportsComputeShaders.ToString()),
                CreateField(
                    "instancing",
                    "GPU Instancing",
                    snapshot.SupportsInstancing.ToString()),
                CreateField(
                    "rayTracing",
                    "Ray Tracing",
                    snapshot.SupportsRayTracing.ToString())
            };
        }

        private static GraphicsInfoSnapshot CaptureSnapshot()
        {
            return GraphicsInfoSnapshotProvider.TryGetRequestedSnapshot(out GraphicsInfoSnapshot snapshot) ? snapshot : GraphicsInfoSnapshotCollector.Capture(false);
        }

    }
}
