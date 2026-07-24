using System;
using UnityEngine;

namespace SAS.Utilities.RuntimeDebugger.Core
{
    /// <summary>
    /// Internal seam for optional inspector sections that need runtime APIs instead of component reflection.
    /// </summary>
    internal interface IRuntimeDebuggerInspectorExtension : IDisposable
    {
        void Inspect(GameObject target, RuntimeObjectRegistry registry, RuntimeObjectDetails details);

        bool TryExecute(RuntimeDebuggerCommand command, RuntimeObjectRegistry registry,
            out RuntimeCommandResult result);
    }
}
