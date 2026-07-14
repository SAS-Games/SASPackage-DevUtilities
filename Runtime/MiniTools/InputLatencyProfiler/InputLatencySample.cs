#if ENABLE_DEBUG
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine;

public enum InputLatencyEventType
{
    Raw,
    Action,
    Pipeline,
    UserMethod
}

public static class InputLatencyCalculator
{
    public static float Since(double timestamp) => Between(timestamp, Time.realtimeSinceStartupAsDouble);

    public static float Between(double startTimestamp, double endTimestamp)
    {
        if (double.IsNaN(startTimestamp) || double.IsInfinity(startTimestamp) ||
            double.IsNaN(endTimestamp) || double.IsInfinity(endTimestamp))
            return 0f;

        return Mathf.Max(0f, (float)((endTimestamp - startTimestamp) * 1000.0));
    }
}

public sealed class InputEventCorrelation
{
    private const int Capacity = 256;
    private readonly System.Collections.Generic.Dictionary<(int deviceId, double time), double> _dequeuedTimes = new();
    private readonly System.Collections.Generic.Queue<(int deviceId, double time)> _order = new();

    public void Record(int deviceId, double eventTime, double dequeuedTime)
    {
        var key = (deviceId, eventTime);
        if (_dequeuedTimes.ContainsKey(key))
            return;
        _dequeuedTimes.Add(key, dequeuedTime);
        _order.Enqueue(key);
        while (_order.Count > Capacity)
            _dequeuedTimes.Remove(_order.Dequeue());
    }

    public bool TryGetDequeuedTime(int deviceId, double eventTime, out double dequeuedTime) =>
        _dequeuedTimes.TryGetValue((deviceId, eventTime), out dequeuedTime);
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
#endif
