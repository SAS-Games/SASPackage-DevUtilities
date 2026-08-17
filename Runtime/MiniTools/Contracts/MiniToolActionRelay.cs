using System;
using UnityEngine;

namespace HP.DevUtilities
{
    /// <summary>
    /// Carries user actions from a shared mini-tool view to whichever
    /// controller owns it: a local Player controller or the Editor Debug Host.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Dev Utilities/Mini Tool/Action Relay")]
    public sealed class MiniToolActionRelay : MonoBehaviour
    {
        public event Action<string> ActionRequested;

        public void Request(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return;

            ActionRequested?.Invoke(actionId.Trim());
        }
    }
}
