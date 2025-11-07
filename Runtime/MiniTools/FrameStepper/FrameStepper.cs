using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FrameStepper : MonoBehaviour
{
    [SerializeField] private GameObject m_Play;
    [SerializeField] private GameObject m_Pause;
    [SerializeField] private Toggle m_Toggle;
    [SerializeField] private Button m_FrameStep;

    private FrameStepperControls _controls;
    private bool _isPaused = true;
    private float _originalTimeScale = 1f;

    private void Awake()
    {
        _controls = new FrameStepperControls();

        _controls.Debug.Pause.performed += _ => TogglePause();
        _controls.Debug.Step.performed += _ => { StepFrame(); };

        m_Toggle.onValueChanged.AddListener(TogglePause);
        m_FrameStep.onClick.AddListener(StepFrame);
        m_Toggle.isOn = _isPaused;
    }

    public void Show(bool status) => gameObject.SetActive(status);

    private void OnEnable() => _controls?.Enable();
    private void OnDisable() => _controls?.Disable();

    private void TogglePause(bool paused)
    {
        if (paused)
        {
            Time.timeScale = _originalTimeScale;
            paused = false;
        }
        else
        {
            // If timeScale is already 0 (paused elsewhere), 
            // fall back to 1 when resuming since the intended running scale is unknown.
            _originalTimeScale = Time.timeScale > 0 ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            paused = true;
        }

        m_Pause.SetActive(paused);
        m_Play.SetActive(!paused);
        m_FrameStep.interactable = paused;
        _isPaused = paused;
    }

    private void TogglePause()
    {
        m_Toggle.isOn = !m_Toggle.isOn;
        //TogglePause(_isPaused);
    }

    private void StepFrame()
    {
        if (_isPaused)
            StartCoroutine(StepOneFrameCoroutine());
    }

    private IEnumerator StepOneFrameCoroutine()
    {
        Time.timeScale = _originalTimeScale;
        yield return null;
        Time.timeScale = 0f;
    }
}