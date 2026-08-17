using System;
using UnityEngine;

namespace HP.Utilities.RuntimeSceneInspector.Core
{
    /// <summary>
    /// Internal seam for optional inspector sections that need runtime APIs instead of component reflection.
    /// </summary>
    internal interface IRuntimeSceneInspectorExtension : IDisposable
    {
        void Inspect(GameObject target, RuntimeObjectRegistry registry, RuntimeObjectDetails details);

        bool TryExecute(RuntimeSceneInspectorCommand command, RuntimeObjectRegistry registry, out RuntimeCommandResult result);
    }
}
