using UnityEngine;

public sealed class InputLatencyProfiler : MonoBehaviour
{
    private InputActionTracker _actionTracker;
    private InputEventTracker _eventTracker;

    private InputLatencyStatistics _rawStatistics;
    private InputLatencyStatistics _actionStatistics;
    private InputLatencyStatistics _pipelineStatistics;

    public InputLatencyStatistics RawStatistics => _rawStatistics;
    public InputLatencyStatistics ActionStatistics => _actionStatistics;
    public InputLatencyStatistics PipelineStatistics => _pipelineStatistics;

    private InputLatencyOverlay _overlay;

    private void OnEnable()
    {
        _rawStatistics = new InputLatencyStatistics(2048);
        _actionStatistics = new InputLatencyStatistics(2048);
        _pipelineStatistics = new InputLatencyStatistics(2048);

        _actionTracker = new InputActionTracker(_actionStatistics, _pipelineStatistics);
        _eventTracker = new InputEventTracker(_rawStatistics);

        _overlay = new InputLatencyOverlay(_rawStatistics, _actionStatistics);

        _actionTracker.Enable();

        _eventTracker.Enable();
    }

    private void OnDisable()
    {
        _actionTracker.Disable();

        _eventTracker.Disable();
    }

    private void OnGUI()
    {
        _overlay.Draw();
    }
}