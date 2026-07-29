using UnityEngine;
using UnityEngine.InputSystem;

namespace SAS.DevUtilities
{
    /// <summary>
    /// Owns local Player time control and connects local snapshots to the
    /// shared FrameStepper view. The Debug Host disables this component.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(
        typeof(FrameStepperSnapshotProvider),
        typeof(FrameStepper),
        typeof(MiniToolActionRelay))]
    public sealed class FrameStepperLocalController : MonoBehaviour, IMiniToolLocalController
    {
        [SerializeField] private FrameStepperSnapshotProvider m_SnapshotProvider;
        [SerializeField] private FrameStepper m_View;
        [SerializeField] private MiniToolActionRelay m_ActionRelay;
        private readonly FrameStepperTimeController _timeController = new();
        private InputSettings.UpdateMode _previousInputUpdateMode;
        private bool _overrodeInputUpdateMode;

        private void Awake()
        {
            ResolveDependencies();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            _timeController.Begin();

            if (m_SnapshotProvider == null ||
                m_View == null ||
                m_ActionRelay == null)
                return;

            m_SnapshotProvider.SnapshotChanged += ApplySnapshot;
            m_ActionRelay.ActionRequested += HandleAction;

            if (m_SnapshotProvider.TryGetSnapshot(out FrameStepperSnapshot snapshot))
            {
                ApplySnapshot(snapshot);
            }
        }

        private void Update()
        {
            _timeController.Tick();
        }

        private void OnDisable()
        {
            if (m_SnapshotProvider != null)
                m_SnapshotProvider.SnapshotChanged -= ApplySnapshot;
            if (m_ActionRelay != null)
                m_ActionRelay.ActionRequested -= HandleAction;

            _timeController.Release();
            RestoreInputUpdateMode();
        }

        private void Pause()
        {
            KeepInputResponsiveWhilePaused();
            _timeController.Pause();
        }

        private void Resume()
        {
            _timeController.Resume();
            RestoreInputUpdateMode();
        }

        private void Step()
        {
            if (Time.timeScale > 0f)
                return;

            KeepInputResponsiveWhilePaused();
            _timeController.TryStep();
        }

        private void Toggle()
        {
            if (Time.timeScale > 0f)
                Pause();
            else
                Resume();
        }

        private void HandleAction(string actionId)
        {
            switch (actionId)
            {
                case FrameStepperActionIds.Toggle:
                    Toggle();
                    break;
                case FrameStepperActionIds.Step:
                    Step();
                    break;
            }
        }

        private void KeepInputResponsiveWhilePaused()
        {
            if (_overrodeInputUpdateMode || InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInFixedUpdate)
                return;

            _previousInputUpdateMode = InputSystem.settings.updateMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            _overrodeInputUpdateMode = true;
        }

        private void RestoreInputUpdateMode()
        {
            if (!_overrodeInputUpdateMode)
                return;

            if (InputSystem.settings.updateMode == InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
                InputSystem.settings.updateMode = _previousInputUpdateMode;

            _overrodeInputUpdateMode = false;
        }

        private void ApplySnapshot(FrameStepperSnapshot snapshot)
        {
            if (m_View != null)
                m_View.ApplySnapshot(in snapshot);
        }

        private void ResolveDependencies()
        {
            if (m_SnapshotProvider == null)
                m_SnapshotProvider = GetComponent<FrameStepperSnapshotProvider>();

            if (m_View == null)
                m_View = GetComponent<FrameStepper>();

            if (m_ActionRelay == null)
                m_ActionRelay = GetComponent<MiniToolActionRelay>();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            ResolveDependencies();
        }
#endif
    }
}
