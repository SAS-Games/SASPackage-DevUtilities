using System.Collections;
using UnityEngine;

public class FrameStepper : MonoBehaviour
{
    [SerializeField] private GUISkin m_GuiSkin;

    private FrameStepperControls _controls;
    private bool _isPaused = false;
    private float _originalTimeScale = 1f;
    private Rect _windowRect;

    private void Awake()
    {
        _controls = new FrameStepperControls();

        _controls.Debug.Pause.performed += _ => TogglePause();
        _controls.Debug.Step.performed += _ =>
        {
            if (_isPaused) StepFrame();
        };

        _windowRect = new Rect(Screen.width - 220, 10, 210, 80);
    }

    public void Show(bool status) => gameObject.SetActive(status);
    
    private void OnEnable() => _controls.Enable();
    private void OnDisable() => _controls.Disable();

    private void TogglePause()
    {
        if (_isPaused)
        {
            Time.timeScale = _originalTimeScale;
            _isPaused = false;
        }
        else
        {
            // If timeScale is already 0 (paused elsewhere), 
            // fall back to 1 when resuming since the intended running scale is unknown.
            _originalTimeScale = Time.timeScale > 0 ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            _isPaused = true;
        }
    }

    private void StepFrame()
    {
        StartCoroutine(StepOneFrameCoroutine());
    }

    private IEnumerator StepOneFrameCoroutine()
    {
        Time.timeScale = _originalTimeScale;
        yield return null;
        Time.timeScale = 0f;
    }

    private void OnGUI()
    {
        if (m_GuiSkin != null)
            GUI.skin = m_GuiSkin;

        _windowRect = GUI.Window(0, _windowRect, DrawUIWindow, "Frame Stepper");
    }

    private void DrawUIWindow(int windowID)
    {
        GUILayout.BeginVertical();

        GUILayout.Label($"Game State: {(_isPaused ? "PAUSED" : "RUNNING")}");

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(_isPaused ? "\u25B6" : "\u23F8", GUILayout.Height(30)))
            TogglePause();

        GUI.enabled = _isPaused;
        if (GUILayout.Button("Step Frame", GUILayout.Height(30)))
            StepFrame();
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        // makes the title bar draggable
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }
}