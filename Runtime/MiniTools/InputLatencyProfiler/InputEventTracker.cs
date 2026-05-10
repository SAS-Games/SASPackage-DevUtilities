using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class InputEventTracker
{
    private readonly InputLatencyStatistics _statistics;

    public InputEventTracker(
        InputLatencyStatistics statistics)
    {
        _statistics = statistics;
    }

    public void Enable()
    {
        InputSystem.onEvent += OnInputEvent;
    }

    public void Disable()
    {
        InputSystem.onEvent -= OnInputEvent;
    }
    
    private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        if (!eventPtr.IsA<StateEvent>() &&
            !eventPtr.IsA<DeltaStateEvent>())
            return;

        double now = Time.realtimeSinceStartupAsDouble;

        float latencyMs = (float)((now - eventPtr.time) * 1000.0);
        InputLatencySharedState.LatestRawLatencyMs = latencyMs;

        InputLatencySample sample = new InputLatencySample(
                InputLatencyEventType.Raw,
                "RAW",
                device != null ? device.name : "Unknown",
                InputActionPhase.Waiting,
                latencyMs,
                Time.frameCount,
                InputState.currentUpdateType);

        _statistics.AddSample(sample);
    }
}