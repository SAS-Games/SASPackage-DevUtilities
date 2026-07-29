#if ENABLE_DEBUG
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class InputEventTracker
{
    private readonly InputEventCorrelation _correlation;

    public InputEventTracker(InputEventCorrelation correlation)
    {
        _correlation = correlation;
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
        if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
            return;

        _correlation.Record(eventPtr.deviceId, eventPtr.time, UnityEngine.Time.realtimeSinceStartupAsDouble);
    }
}
#endif
