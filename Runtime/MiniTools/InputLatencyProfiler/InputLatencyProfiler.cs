using System;
using UnityEngine.InputSystem;

#if ENABLE_DEBUG
using Unity.Profiling;
using UnityEngine;

public sealed class InputLatencyProfiler : MonoBehaviour
{
    private InputActionTracker _actionTracker;
    private InputEventTracker _eventTracker;

    private InputLatencyStatistics _rawStatistics;
    private InputLatencyStatistics _actionStatistics;
    private InputLatencyStatistics _pipelineStatistics;
    private InputLatencyStatistics _userMethodStatistics;

    public InputLatencyStatistics RawStatistics => _rawStatistics; 
    public InputLatencyStatistics ActionStatistics => _actionStatistics;
    public InputLatencyStatistics PipelineStatistics => _pipelineStatistics;
    public InputLatencyStatistics UserMethodStatistics => _userMethodStatistics;
    private InputLatencyOverlay _overlay;

    private void OnEnable()
    {
        _rawStatistics = new InputLatencyStatistics(2048);
        _actionStatistics = new InputLatencyStatistics(2048);
        _pipelineStatistics = new InputLatencyStatistics(2048);
        _userMethodStatistics = new InputLatencyStatistics(2048);
        var correlation = new InputEventCorrelation();

        _actionTracker = new InputActionTracker(_actionStatistics, _rawStatistics, _pipelineStatistics, correlation);
        _eventTracker = new InputEventTracker(correlation);

        _overlay = new InputLatencyOverlay(_rawStatistics, _actionStatistics, _pipelineStatistics, _userMethodStatistics);
        InputLatencyProfilerMarker.SetTarget(this);

        Canvas legacyCanvas = GetComponentInParent<Canvas>();
        if (legacyCanvas != null)
            legacyCanvas.enabled = false;
        InputLatencyGraphUI legacyGraph = GetComponent<InputLatencyGraphUI>();
        if (legacyGraph != null)
            legacyGraph.enabled = false;

        _actionTracker.Enable();

        _eventTracker.Enable();
    }

    private void OnDisable()
    {
        InputLatencyProfilerMarker.ClearTarget(this);
        _actionTracker?.Disable();
        _eventTracker?.Disable();
        _overlay?.Dispose();
        _overlay = null;
    }

    private void OnGUI()
    {
        _overlay?.Draw();
    }

    internal void RecordUserMethod(InputAction.CallbackContext context, string markerName)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        float latencyMs = InputLatencyCalculator.Between(context.time, now);
        _userMethodStatistics.AddSample(new InputLatencySample(
            InputLatencyEventType.UserMethod,
            string.IsNullOrEmpty(markerName)
                ? context.action != null ? context.action.name : "User method"
                : markerName,
            context.control != null ? context.control.path : "Unknown",
            context.phase,
            latencyMs,
            Time.frameCount,
            UnityEngine.InputSystem.LowLevel.InputState.currentUpdateType));
    }
}
#endif

/// <summary>
/// Measures event-to-user-method-entry latency and places the user callback body in the Unity Profiler.
/// Call <see cref="Measure"/> as the first statement of an InputAction callback.
/// </summary>
public static class InputLatencyProfilerMarker
{
#if ENABLE_DEBUG
    private static readonly ProfilerMarker UserCallbackMarker = new ProfilerMarker("InputLatency.UserCallback");
    private static InputLatencyProfiler _target;

    public static Scope Measure(InputAction.CallbackContext context, string markerName = null)
    {
        if (_target != null)
            _target.RecordUserMethod(context, markerName);
        return new Scope(UserCallbackMarker);
    }

    [System.Diagnostics.Conditional("ENABLE_DEBUG")]
    public static void Record(InputAction.CallbackContext context, string markerName = null)
    {
        if (_target != null)
            _target.RecordUserMethod(context, markerName);
    }

    internal static void SetTarget(InputLatencyProfiler target) => _target = target;

    internal static void ClearTarget(InputLatencyProfiler target)
    {
        if (_target == target)
            _target = null;
    }

    public readonly struct Scope : IDisposable
    {
        private readonly ProfilerMarker _marker;

        internal Scope(ProfilerMarker marker)
        {
            _marker = marker;
            _marker.Begin();
        }

        public void Dispose() => _marker.End();
    }
#else
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Scope Measure(InputAction.CallbackContext context, string markerName = null) => default;

    [System.Diagnostics.Conditional("ENABLE_DEBUG")]
    public static void Record(InputAction.CallbackContext context, string markerName = null) { }

    public readonly struct Scope : IDisposable
    {
        public void Dispose() { }
    }
#endif
}
