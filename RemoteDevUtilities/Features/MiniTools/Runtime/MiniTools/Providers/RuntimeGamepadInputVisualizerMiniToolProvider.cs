using HP.Utilities.DeveloperConsole.InputVisualizers;

namespace HP.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimeGamepadInputVisualizerMiniToolProvider : RuntimeInputVisualizerMiniToolProvider
    {
        public RuntimeGamepadInputVisualizerMiniToolProvider() : base(InputVisualizerDeviceKind.Gamepad)
        {
        }
    }
}
