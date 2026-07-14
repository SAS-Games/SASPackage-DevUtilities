#if ENABLE_DEBUG
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class InputActionTracker
{
    private readonly HashSet<InputAction> _tracked = new HashSet<InputAction>();

    private readonly InputLatencyStatistics _statistics;
    private readonly InputLatencyStatistics _queueStatistics;
    private readonly InputLatencyStatistics _pipelineStatistics;
    private readonly InputEventCorrelation _correlation;

    public InputActionTracker(InputLatencyStatistics actionStatistics, InputLatencyStatistics queueStatistics, InputLatencyStatistics pipelineStatistics, InputEventCorrelation correlation)
    {
        _statistics = actionStatistics;
        _queueStatistics = queueStatistics;
        _pipelineStatistics = pipelineStatistics;
        _correlation = correlation;
    }

    public void Enable()
    {
        RegisterExistingActions();

        InputSystem.onActionChange += OnActionChange;
    }

    public void Disable()
    {
        InputSystem.onActionChange -= OnActionChange;

        UnregisterAllActions();
    }

    private void RegisterExistingActions()
    {
        List<InputAction> enabledActions = new List<InputAction>();
        InputSystem.ListEnabledActions(enabledActions);
        foreach (InputAction action in enabledActions)
        {
            RegisterAction(action);
        }
    }

    private void OnActionChange(object obj, InputActionChange change)
    {
        switch (change)
        {
            case InputActionChange.ActionEnabled when obj is InputAction action:
                RegisterAction(action);
                break;

            case InputActionChange.ActionDisabled when obj is InputAction action:
                UnregisterAction(action);
                break;

            case InputActionChange.ActionMapEnabled when obj is InputActionMap enabledMap:
                foreach (InputAction mapAction in enabledMap.actions)
                    RegisterAction(mapAction);
                break;

            case InputActionChange.ActionMapDisabled when obj is InputActionMap disabledMap:
                foreach (InputAction mapAction in disabledMap.actions)
                    UnregisterAction(mapAction);
                break;
        }
    }

    private void RegisterAction(InputAction action)
    {
        if (action == null)
            return;

        if (!_tracked.Add(action))
            return;

        action.started += OnActionStarted;
        action.performed += OnActionPerformed;
        action.canceled += OnActionCanceled;
    }

    private void UnregisterAction(InputAction action)
    {
        if (action == null)
            return;

        if (!_tracked.Remove(action))
            return;

        action.started -= OnActionStarted;
        action.performed -= OnActionPerformed;
        action.canceled -= OnActionCanceled;
    }

    private void UnregisterAllActions()
    {
        foreach (InputAction action in _tracked)
        {
            if (action == null)
                continue;

            action.started -= OnActionStarted;
            action.performed -= OnActionPerformed;
            action.canceled -= OnActionCanceled;
        }

        _tracked.Clear();
    }

    private void OnActionStarted(InputAction.CallbackContext context)
    {
        RecordAction(context, InputActionPhase.Started);
    }

    private void OnActionPerformed(InputAction.CallbackContext context)
    {
        RecordAction(context, InputActionPhase.Performed);
    }
    
    private void OnActionCanceled(InputAction.CallbackContext context)
    {
        RecordAction(context, InputActionPhase.Canceled);
    }

    private void RecordAction(InputAction.CallbackContext context, InputActionPhase phase)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        float latencyMs = InputLatencyCalculator.Between(context.time, now);

        InputLatencySample sample = new InputLatencySample(
                InputLatencyEventType.Action,
                context.action != null ? context.action.name : "Unknown",
                context.control != null ? context.control.path : "Unknown",
                phase,
                latencyMs,
                Time.frameCount,
                InputState.currentUpdateType);
        _statistics.AddSample(sample);

        if (context.control != null && _correlation.TryGetDequeuedTime(context.control.device.deviceId, context.time, out double eventDequeuedTime))
        {
            string actionName = context.action != null ? context.action.name : "Unknown";
            _queueStatistics.AddSample(new InputLatencySample(
                InputLatencyEventType.Raw,
                actionName,
                context.control.path,
                phase,
                InputLatencyCalculator.Between(context.time, eventDequeuedTime),
                Time.frameCount,
                InputState.currentUpdateType));

            float pipelineLatencyMs = InputLatencyCalculator.Between(eventDequeuedTime, now);
            _pipelineStatistics.AddSample(new InputLatencySample(
                InputLatencyEventType.Pipeline,
                actionName,
                context.control.path,
                phase,
                pipelineLatencyMs,
                Time.frameCount,
                InputState.currentUpdateType));
        }
    }
}
#endif
