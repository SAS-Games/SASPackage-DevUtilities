using System;
using HP.Utilities.RuntimeSceneInspector.Core;

namespace HP.Utilities.RemoteDevUtilities.DebugHost
{
    /// <summary>Shared state for the Editor-only Play Mode Debug Host.</summary>
    public static class RemoteDebugHostSession
    {
        public static IRuntimeSceneInspector RuntimeSceneInspector { get; private set; }
        public static bool RuntimeSceneInspectorPresentationVisible { get; private set; }

        public static event Action<bool> RuntimeSceneInspectorPresentationVisibilityChanged;

        public static void Install(IRuntimeSceneInspector runtimeSceneInspector)
        {
            RuntimeSceneInspector = runtimeSceneInspector;
            SetRuntimeSceneInspectorPresentationVisible(false);
        }

        public static void Clear()
        {
            SetRuntimeSceneInspectorPresentationVisible(false);
            RuntimeSceneInspector = null;
        }

        public static void SetRuntimeSceneInspectorPresentationVisible(bool visible)
        {
            RuntimeSceneInspectorPresentationVisible = visible;
            RuntimeSceneInspectorPresentationVisibilityChanged?.Invoke(visible);
        }
    }
}
