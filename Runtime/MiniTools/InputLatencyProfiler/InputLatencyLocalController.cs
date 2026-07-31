using System;
using SAS.DevUtilities;
using SAS.Utilities.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

#if ENABLE_DEBUG
internal interface IInputLatencyProfilerCloseInput :
    IDisposable
{
    bool CloseRequested { get; }
    void Update();
}

internal sealed class InputSystemInputLatencyProfilerCloseInput : IInputLatencyProfilerCloseInput
{
    private readonly InputAction _closeAction;
    private bool _queued;
    private bool _current;

    public InputSystemInputLatencyProfilerCloseInput(bool includeController)
    {
        _closeAction = new InputAction("CloseInputLatencyProfiler", InputActionType.Button);
        _closeAction.AddBinding("<Keyboard>/escape");
        if (includeController)
            _closeAction.AddBinding("<Gamepad>/buttonEast");
        _closeAction.performed += OnClosePerformed;
        _closeAction.Enable();
    }

    public bool CloseRequested => _current;

    public void Update()
    {
        _current = _queued;
        _queued = false;
    }

    public void Dispose()
    {
        _closeAction.performed -= OnClosePerformed;
        _closeAction.Disable();
        _closeAction.Dispose();
        _queued = false;
        _current = false;
    }

    private void OnClosePerformed(InputAction.CallbackContext context)
    {
        _queued = true;
    }
}
#endif

/// <summary>
/// Connects the local Input Latency provider to the shared presentation and
/// owns Player-only close input. The Editor Debug Host bypasses this
/// controller and applies remote data directly to the same view.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InputLatencySnapshotProvider), typeof(InputLatencyView))]
public sealed class InputLatencyLocalController : MonoBehaviour, IMiniToolLocalController
{
    [SerializeField] private DevUtilityPresentation m_Presentation;

    [SerializeField, Tooltip("Allow gamepad B / button East to close the overlay. Disable this " + "when gameplay owns the controller.")]
    private bool m_EnableControllerClose;

    [SerializeField] private InputLatencySnapshotProvider m_SnapshotProvider;
    [SerializeField] private InputLatencyView m_View;

#if ENABLE_DEBUG
    private IInputLatencyProfilerCloseInput _closeInput;
#endif

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        if (m_SnapshotProvider == null || m_View == null)
            return;

        m_View.ConfigureLocalPresentation(m_EnableControllerClose, true);
        m_SnapshotProvider.SnapshotChanged += ApplySnapshot;
        m_SnapshotProvider.EventsChanged += ApplyEvents;

        if (m_SnapshotProvider.TryGetSnapshot(out InputLatencySnapshot snapshot))
            ApplySnapshot(snapshot);

        if (m_SnapshotProvider.TryGetEvents(out InputLatencySampleEvent[] events, out int droppedEventCount))
            ApplyEvents(events, droppedEventCount);

#if ENABLE_DEBUG
        _closeInput = new InputSystemInputLatencyProfilerCloseInput(m_EnableControllerClose);
#endif
    }

    private void OnDisable()
    {
        if (m_SnapshotProvider != null)
        {
            m_SnapshotProvider.SnapshotChanged -= ApplySnapshot;
            m_SnapshotProvider.EventsChanged -= ApplyEvents;
        }

#if ENABLE_DEBUG
        _closeInput?.Dispose();
        _closeInput = null;
#endif

        m_View?.ConfigureLocalPresentation(false, false);
    }

    private void Update()
    {
#if ENABLE_DEBUG
        _closeInput?.Update();
        if (_closeInput?.CloseRequested == true)
            Close();
#endif
    }

    public void Close()
    {
        m_Presentation?.SetRequestedVisible(false);
    }

    private void ApplySnapshot(InputLatencySnapshot snapshot)
    {
        if (m_View != null)
            m_View.ApplySnapshot(in snapshot);
    }

    private void ApplyEvents(InputLatencySampleEvent[] events, int droppedEventCount)
    {
        if (m_View != null)
            m_View.ApplyEvents(events, droppedEventCount);
    }

    private void ResolveDependencies()
    {
        if (m_SnapshotProvider == null)
            m_SnapshotProvider = GetComponent<InputLatencySnapshotProvider>();
        if (m_View == null)
            m_View = GetComponent<InputLatencyView>();
        if (m_Presentation == null)
            m_Presentation = GetComponentInParent<DevUtilityPresentation>(true);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveDependencies();
    }
#endif
}

/// <summary>
/// Measures event-to-user-method-entry latency and places the user callback
/// body in the Unity Profiler. Call <see cref="Measure"/> as the first
/// statement of an InputAction callback.
/// </summary>
public static class InputLatencyProfilerMarker
{
#if ENABLE_DEBUG
    private static readonly Unity.Profiling.ProfilerMarker UserCallbackMarker = new Unity.Profiling.ProfilerMarker("InputLatency.UserCallback");
    private static InputLatencyCollector _target;

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

    internal static void SetTarget(InputLatencyCollector target)
    {
        _target = target;
    }

    internal static void ClearTarget(InputLatencyCollector target)
    {
        if (_target == target)
            _target = null;
    }

    public readonly struct Scope : IDisposable
    {
        private readonly Unity.Profiling.ProfilerMarker _marker;

        internal Scope(Unity.Profiling.ProfilerMarker marker)
        {
            _marker = marker;
            _marker.Begin();
        }

        public void Dispose()
        {
            _marker.End();
        }
    }
#else
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions
            .AggressiveInlining)]
    public static Scope Measure(
        InputAction.CallbackContext context,
        string markerName = null)
    {
        return default;
    }

    [System.Diagnostics.Conditional("ENABLE_DEBUG")]
    public static void Record(
        InputAction.CallbackContext context,
        string markerName = null)
    {
    }

    public readonly struct Scope : IDisposable
    {
        public void Dispose()
        {
        }
    }
#endif
}
