using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class InputActionTracker
{
    private readonly HashSet<InputAction> _tracked = new HashSet<InputAction>();

    private readonly InputLatencyStatistics _statistics;

    public InputActionTracker(InputLatencyStatistics actionStatistics)
    {
        _statistics = actionStatistics;
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
        InputActionAsset[] assets = Resources.FindObjectsOfTypeAll<InputActionAsset>();

        foreach (InputActionAsset asset in assets)
        {
            if (asset == null)
                continue;

            foreach (InputActionMap map in asset.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    if (action.enabled)
                    {
                        RegisterAction(action);
                    }
                }
            }
        }
    }

    private void OnActionChange(object obj, InputActionChange change)
    {
        if (obj is not InputAction action)
            return;

        switch (change)
        {
            case InputActionChange.ActionEnabled:
                RegisterAction(action);
                break;

            case InputActionChange.ActionDisabled:
                UnregisterAction(action);
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
        float latencyMs = (float)((now - context.time) * 1000.0);
        float pipelineDelta = latencyMs - InputLatencySharedState.LatestRawLatencyMs;
        pipelineDelta = Mathf.Max(0f, pipelineDelta);

        InputLatencySample sample = new InputLatencySample(
                InputLatencyEventType.Action,
                context.action != null ? context.action.name : "Unknown",
                context.control != null ? context.control.path : "Unknown",
                phase,
                latencyMs,
                Time.frameCount,
                InputState.currentUpdateType);
        _statistics.AddSample(sample);
    }
}
