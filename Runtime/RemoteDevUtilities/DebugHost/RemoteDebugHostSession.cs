using System;
using SAS.Utilities.RuntimeDebugger.Core;

namespace SAS.Utilities.RemoteDevUtilities.DebugHost
{
    /// <summary>Shared state for the Editor-only Play Mode Debug Host.</summary>
    public static class RemoteDebugHostSession
    {
        public static IRuntimeDebugger RuntimeDebugger { get; private set; }
        public static bool RuntimeDebuggerPresentationVisible { get; private set; }

        public static event Action<bool> RuntimeDebuggerPresentationVisibilityChanged;

        public static void Install(IRuntimeDebugger runtimeDebugger)
        {
            RuntimeDebugger = runtimeDebugger;
            SetRuntimeDebuggerPresentationVisible(false);
        }

        public static void Clear()
        {
            SetRuntimeDebuggerPresentationVisible(false);
            RuntimeDebugger = null;
        }

        public static void SetRuntimeDebuggerPresentationVisible(bool visible)
        {
            RuntimeDebuggerPresentationVisible = visible;
            RuntimeDebuggerPresentationVisibilityChanged?.Invoke(visible);
        }
    }
}
