using System;
using SAS.DevUtilities;

[Serializable]
public struct InputLatencyMetricSnapshot
{
    public float MinimumMs;
    public float AverageMs;
    public float MaximumMs;
    public int SampleCount;

#if ENABLE_DEBUG
    internal static InputLatencyMetricSnapshot Capture(InputLatencyStatistics statistics)
    {
        return statistics == null ? default : new InputLatencyMetricSnapshot
            {
                MinimumMs = statistics.Min,
                AverageMs = statistics.Average,
                MaximumMs = statistics.Max,
                SampleCount = statistics.SampleCount
            };
    }
#endif
}

/// <summary>
/// Recoverable snapshot used to initialize and reconcile the remote latency
/// presentation. Detailed history is carried by InputLatencySampleEvent.
/// </summary>
[Serializable]
public struct InputLatencySnapshot : IMiniToolSnapshot
{
    public bool IsAvailable;
    public string Status;
    public int Frame;
    public string UpdateType;
    public InputLatencyMetricSnapshot EventQueue;
    public InputLatencyMetricSnapshot Action;
    public InputLatencyMetricSnapshot Dispatch;
    public InputLatencyMetricSnapshot UserMethod;

#if ENABLE_DEBUG
    internal static InputLatencySnapshot Capture(InputLatencyCollector collector)
    {
        if (collector == null)
        {
            return new InputLatencySnapshot
            {
                Status = "Input latency collection has not started."
            };
        }

        return new InputLatencySnapshot
        {
            IsAvailable = true,
            Status = string.Empty,
            Frame = collector.CurrentFrame,
            UpdateType = collector.CurrentUpdateType,
            EventQueue = InputLatencyMetricSnapshot.Capture(collector.RawStatistics),
            Action = InputLatencyMetricSnapshot.Capture(collector.ActionStatistics),
            Dispatch = InputLatencyMetricSnapshot.Capture(collector.PipelineStatistics),
            UserMethod = InputLatencyMetricSnapshot.Capture(collector.UserMethodStatistics)
        };
    }
#endif
}

/// <summary>
/// Portable form of one latency measurement. Mutable fields are intentional:
/// Unity's current JSON transport serializes fields rather than constructors.
/// </summary>
[Serializable]
public struct InputLatencySampleEvent : IMiniToolStreamEvent
{
    public int EventType;
    public string ActionName;
    public string ControlName;
    public int Phase;
    public float LatencyMs;
    public int Frame;
    public int UpdateType;

#if ENABLE_DEBUG
    internal static InputLatencySampleEvent FromSample(in InputLatencySample sample)
    {
        return new InputLatencySampleEvent
        {
            EventType = (int)sample.EventType,
            ActionName = sample.ActionName,
            ControlName = sample.ControlName,
            Phase = (int)sample.Phase,
            LatencyMs = sample.LatencyMs,
            Frame = sample.Frame,
            UpdateType = (int)sample.UpdateType
        };
    }

    internal InputLatencySample ToSample()
    {
        return new InputLatencySample((InputLatencyEventType)EventType, ActionName, ControlName,
            (UnityEngine.InputSystem.InputActionPhase)Phase, LatencyMs, Frame,
            (UnityEngine.InputSystem.LowLevel.InputUpdateType)UpdateType);
    }
#endif
}
