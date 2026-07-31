using SAS.DevUtilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// View-only FrameStepper presentation. It renders snapshots and exposes
/// user intent without changing the local Player's time scale.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MiniToolActionRelay))]
[AddComponentMenu("Dev Utilities/Frame Stepper/View")]
public sealed class FrameStepper : MonoBehaviour, IMiniToolSnapshotView<FrameStepperSnapshot>
{
    [SerializeField] private GameObject m_Play;
    [SerializeField] private GameObject m_Pause;
    [SerializeField] private Toggle m_Toggle;
    [SerializeField] private Button m_FrameStep;
    [SerializeField] private MiniToolActionRelay m_ActionRelay;

    private FrameStepperControls _controls;
    private bool _isPaused;

    private void Awake()
    {
        ResolveDependencies();
        _controls = new FrameStepperControls();
        _controls.Debug.Pause.performed += OnPausePerformed;
        _controls.Debug.Step.performed += OnStepPerformed;

        if (m_Toggle != null)
            m_Toggle.onValueChanged.AddListener(OnRunningChanged);
        if (m_FrameStep != null)
            m_FrameStep.onClick.AddListener(RequestStep);
    }

    private void OnEnable()
    {
        _controls?.Enable();
    }

    private void OnDisable()
    {
        _controls?.Disable();
    }

    private void OnDestroy()
    {
        if (_controls != null)
        {
            _controls.Debug.Pause.performed -= OnPausePerformed;
            _controls.Debug.Step.performed -= OnStepPerformed;
            _controls.Dispose();
            _controls = null;
        }

        if (m_Toggle != null)
            m_Toggle.onValueChanged.RemoveListener(OnRunningChanged);
        if (m_FrameStep != null)
            m_FrameStep.onClick.RemoveListener(RequestStep);
    }

    public void ApplySnapshot(in FrameStepperSnapshot snapshot)
    {
        _isPaused = snapshot.IsPaused;

        if (m_Toggle != null)
            m_Toggle.SetIsOnWithoutNotify(!_isPaused);
        if (m_Pause != null)
            m_Pause.SetActive(_isPaused);
        if (m_Play != null)
            m_Play.SetActive(!_isPaused);
        if (m_FrameStep != null)
            m_FrameStep.interactable = _isPaused;
    }

    private void OnRunningChanged(bool _)
    {
        RequestAction(FrameStepperActionIds.Toggle);
    }

    private void OnPausePerformed(InputAction.CallbackContext _)
    {
        RequestAction(FrameStepperActionIds.Toggle);
    }

    private void OnStepPerformed(InputAction.CallbackContext _)
    {
        RequestStep();
    }

    private void RequestStep()
    {
        if (_isPaused)
            RequestAction(FrameStepperActionIds.Step);
    }

    private void RequestAction(string actionId)
    {
        m_ActionRelay?.Request(actionId);
    }

    private void ResolveDependencies()
    {
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
