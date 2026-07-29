#if ENABLE_DEBUG
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

internal interface IInputLatencyOverlayModel
{
    IInputLatencyStatisticsSource RawStatistics { get; }
    IInputLatencyStatisticsSource ActionStatistics { get; }
    IInputLatencyStatisticsSource PipelineStatistics { get; }
    IInputLatencyStatisticsSource UserMethodStatistics { get; }
    int CurrentFrame { get; }
    string CurrentUpdateType { get; }
    int DroppedSampleCount { get; }
}

/// <summary>
/// Owns Input System tracking independently of local or remote presentation.
/// The shared lease prevents duplicate Input System subscriptions when the
/// Player overlay and a remote host are active together.
/// </summary>
internal sealed class InputLatencyCollector : IInputLatencyOverlayModel, IDisposable
{
    private readonly InputActionTracker _actionTracker;
    private readonly InputEventTracker _eventTracker;
    private bool _disposed;

    internal InputLatencyCollector()
    {
        RawStatistics = new InputLatencyStatistics(2048);
        ActionStatistics = new InputLatencyStatistics(2048);
        PipelineStatistics = new InputLatencyStatistics(2048);
        UserMethodStatistics = new InputLatencyStatistics(2048);

        var correlation = new InputEventCorrelation();
        _actionTracker = new InputActionTracker(ActionStatistics, RawStatistics, PipelineStatistics, correlation);
        _eventTracker = new InputEventTracker(correlation);
        _actionTracker.Enable();
        _eventTracker.Enable();
        InputLatencyProfilerMarker.SetTarget(this);
    }

    public InputLatencyStatistics RawStatistics { get; }
    public InputLatencyStatistics ActionStatistics { get; }
    public InputLatencyStatistics PipelineStatistics { get; }
    public InputLatencyStatistics UserMethodStatistics { get; }

    IInputLatencyStatisticsSource IInputLatencyOverlayModel.RawStatistics => RawStatistics;
    IInputLatencyStatisticsSource IInputLatencyOverlayModel.ActionStatistics => ActionStatistics;
    IInputLatencyStatisticsSource IInputLatencyOverlayModel.PipelineStatistics => PipelineStatistics;
    IInputLatencyStatisticsSource IInputLatencyOverlayModel.UserMethodStatistics => UserMethodStatistics;
    public int CurrentFrame => Time.frameCount;
    public string CurrentUpdateType => InputState.currentUpdateType.ToString();

    public int DroppedSampleCount => 0;

    internal void RecordUserMethod(InputAction.CallbackContext context, string markerName)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        float latencyMs = InputLatencyCalculator.Between(context.time, now);
        UserMethodStatistics.AddSample(new InputLatencySample(InputLatencyEventType.UserMethod,
                string.IsNullOrEmpty(markerName) ? context.action != null ? context.action.name : "User method" : markerName,
                context.control != null ? context.control.path : "Unknown",
                context.phase, latencyMs, Time.frameCount, InputState.currentUpdateType));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        InputLatencyProfilerMarker.ClearTarget(this);
        _actionTracker.Disable();
        _eventTracker.Disable();
    }
}

internal static class InputLatencyCollectorPool
{
    private static InputLatencyCollector _collector;
    private static int _leaseCount;

    internal static InputLatencyCollector Acquire()
    {
        if (_collector == null)
            _collector = new InputLatencyCollector();

        _leaseCount++;
        return _collector;
    }

    internal static void Release(InputLatencyCollector collector)
    {
        if (collector == null || !ReferenceEquals(collector, _collector))
        {
            return;
        }

        _leaseCount = Math.Max(0, _leaseCount - 1);
        if (_leaseCount != 0)
            return;

        _collector.Dispose();
        _collector = null;
    }
}
#endif
