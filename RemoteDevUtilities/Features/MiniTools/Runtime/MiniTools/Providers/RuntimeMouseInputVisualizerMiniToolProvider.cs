using HP.Utilities.DeveloperConsole.InputVisualizers;

namespace HP.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimeMouseInputVisualizerMiniToolProvider : RuntimeInputVisualizerMiniToolProvider
    {
        public RuntimeMouseInputVisualizerMiniToolProvider() : base(InputVisualizerDeviceKind.Mouse)
        {
        }
    }
}
