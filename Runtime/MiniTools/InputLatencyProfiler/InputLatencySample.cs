using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public enum InputLatencyEventType
{
    Raw,
    Action
}

public static class InputLatencySharedState
{
    public static float LatestRawLatencyMs;
}

public readonly struct InputLatencySample
{
    public readonly InputLatencyEventType EventType;

    public readonly string ActionName;
    public readonly string ControlName;

    public readonly InputActionPhase Phase;

    public readonly float LatencyMs;

    public readonly int Frame;

    public readonly InputUpdateType UpdateType;

    public InputLatencySample(
        InputLatencyEventType eventType,
        string actionName,
        string controlName,
        InputActionPhase phase,
        float latencyMs,
        int frame,
        InputUpdateType updateType)
    {
        EventType = eventType;

        ActionName = actionName;
        ControlName = controlName;

        Phase = phase;

        LatencyMs = latencyMs;

        Frame = frame;

        UpdateType = updateType;
    }
}