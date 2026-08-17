using SAS.Utilities.DeveloperConsole.InputVisualizers;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimeGamepadInputVisualizerMiniToolProvider : RuntimeInputVisualizerMiniToolProvider
    {
        public RuntimeGamepadInputVisualizerMiniToolProvider() : base(InputVisualizerDeviceKind.Gamepad)
        {
        }
    }
}
