using SAS.Utilities.RemoteDevUtilities.Agent;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SAS.Utilities.RemoteDevUtilities.DebugHost.Presentation
{
    /// <summary>Initializes the Editor-only Debug Host environment.</summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class RemoteDebugHostEnvironmentView : MonoBehaviour
    {
        private RuntimeBackgroundExecutionLease _backgroundExecution;

        private void Awake()
        {
            _backgroundExecution =
                new RuntimeBackgroundExecutionLease();
            _backgroundExecution.Acquire(true);

            EventSystem eventSystem = GetComponentInChildren<EventSystem>(true);
            if (eventSystem != null)
                eventSystem.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            _backgroundExecution?.Dispose();
            _backgroundExecution = null;
        }
    }
}
