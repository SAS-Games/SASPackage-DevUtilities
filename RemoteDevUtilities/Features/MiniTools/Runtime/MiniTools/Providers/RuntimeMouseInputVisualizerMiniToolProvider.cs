using SAS.Utilities.DeveloperConsole.InputVisualizers;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimeMouseInputVisualizerMiniToolProvider : RuntimeInputVisualizerMiniToolProvider
    {
        public RuntimeMouseInputVisualizerMiniToolProvider() : base(InputVisualizerDeviceKind.Mouse)
        {
        }
    }
}
