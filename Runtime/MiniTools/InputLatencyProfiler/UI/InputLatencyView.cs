using HP.DevUtilities;
using UnityEngine;

/// <summary>
/// Shared Input Latency presentation used by both the local Player and the
/// Editor Debug Host. Data arrives only through the typed mini-tool contracts.
/// </summary>
[DisallowMultipleComponent]
public sealed class InputLatencyView : MonoBehaviour, IMiniToolSnapshotView<InputLatencySnapshot>, IMiniToolStreamView<InputLatencySampleEvent>
{
#if ENABLE_DEBUG
    private sealed class PresentationStatistics : IInputLatencyStatisticsSource
    {
        private readonly InputLatencyStatistics _history = new InputLatencyStatistics(2048);
        private InputLatencyMetricSnapshot _summary;

        public float Average => _summary.AverageMs;
        public float Min => _summary.MinimumMs;
        public float Max => _summary.MaximumMs;
        public int SampleCount => _summary.SampleCount;
        public int RecentSampleCount => _history.RecentSampleCount;

        internal void ApplySummary(in InputLatencyMetricSnapshot summary)
        {
            _summary = summary;
        }

        internal void Add(in InputLatencySample sample)
        {
            _history.AddSample(sample);
        }

        public ref readonly InputLatencySample GetRecentSample(int index)
        {
            return ref _history.GetRecentSample(index);
        }
    }

    private sealed class PresentationModel : IInputLatencyOverlayModel
    {
        private readonly PresentationStatistics _raw = new();
        private readonly PresentationStatistics _action = new();
        private readonly PresentationStatistics _pipeline = new();
        private readonly PresentationStatistics _userMethod = new();

        public bool IsAvailable { get; private set; } = true;
        public string Status { get; private set; } = string.Empty;
        public int CurrentFrame { get; private set; }
        public string CurrentUpdateType { get; private set; } = "Unknown";
        public int DroppedSampleCount { get; private set; }
        public IInputLatencyStatisticsSource RawStatistics => _raw;
        public IInputLatencyStatisticsSource ActionStatistics => _action;
        public IInputLatencyStatisticsSource PipelineStatistics => _pipeline;

        public IInputLatencyStatisticsSource UserMethodStatistics => _userMethod;

        internal void ApplySnapshot(in InputLatencySnapshot snapshot)
        {
            IsAvailable = snapshot.IsAvailable;
            Status = snapshot.Status ?? string.Empty;
            CurrentFrame = snapshot.Frame;
            CurrentUpdateType = string.IsNullOrWhiteSpace(snapshot.UpdateType) ? "Unknown" : snapshot.UpdateType;
            _raw.ApplySummary(in snapshot.EventQueue);
            _action.ApplySummary(in snapshot.Action);
            _pipeline.ApplySummary(in snapshot.Dispatch);
            _userMethod.ApplySummary(in snapshot.UserMethod);
        }

        internal void ApplyEvents(InputLatencySampleEvent[] events, int droppedEventCount)
        {
            DroppedSampleCount += Mathf.Max(0, droppedEventCount);
            foreach (InputLatencySampleEvent portable in events ?? System.Array.Empty<InputLatencySampleEvent>())
            {
                InputLatencySample sample = portable.ToSample();
                CurrentFrame = Mathf.Max(CurrentFrame, sample.Frame);
                CurrentUpdateType = sample.UpdateType.ToString();

                switch (sample.EventType)
                {
                    case InputLatencyEventType.Raw:
                        _raw.Add(in sample);
                        break;
                    case InputLatencyEventType.Action:
                        _action.Add(in sample);
                        break;
                    case InputLatencyEventType.Pipeline:
                        _pipeline.Add(in sample);
                        break;
                    case InputLatencyEventType.UserMethod:
                        _userMethod.Add(in sample);
                        break;
                }
            }
        }
    }

    private PresentationModel _model;
    private InputLatencyOverlay _overlay;
    private bool _controllerCloseEnabled;
    private bool _showCloseHint;

    private void OnEnable()
    {
        EnsurePresentation();
    }

    private void OnDisable()
    {
        _overlay?.Dispose();
        _overlay = null;
        _model = null;
    }

    private void OnGUI()
    {
        if (_model == null)
            return;

        if (!_model.IsAvailable)
        {
            GUI.Box(new Rect(24f, 24f, 460f, 80f), string.IsNullOrWhiteSpace(_model.Status) ? "Input Latency is unavailable." : _model.Status);
            return;
        }

        _overlay?.Draw();
    }

    public void ApplySnapshot(in InputLatencySnapshot snapshot)
    {
        EnsurePresentation();
        _model.ApplySnapshot(in snapshot);
    }

    public void ApplyEvents(InputLatencySampleEvent[] events, int droppedEventCount)
    {
        EnsurePresentation();
        _model.ApplyEvents(events, droppedEventCount);
    }

    internal void ConfigureLocalPresentation(bool controllerCloseEnabled, bool showCloseHint)
    {
        if (_controllerCloseEnabled == controllerCloseEnabled && _showCloseHint == showCloseHint)
        {
            return;
        }

        _controllerCloseEnabled = controllerCloseEnabled;
        _showCloseHint = showCloseHint;
        if (!isActiveAndEnabled)
            return;

        RecreateOverlay();
    }

    private void EnsurePresentation()
    {
        if (_model != null)
            return;

        _model = new PresentationModel();
        RecreateOverlay();
    }

    private void RecreateOverlay()
    {
        _overlay?.Dispose();
        _overlay = _model == null ? null : new InputLatencyOverlay(_model, _controllerCloseEnabled, _showCloseHint);
    }
#else
    private InputLatencySnapshot _snapshot;

    private void OnGUI()
    {
        GUI.Box(new Rect(24f, 24f, 460f, 80f), string.IsNullOrWhiteSpace(_snapshot.Status) ? "Input Latency requires ENABLE_DEBUG." : _snapshot.Status);
    }

    public void ApplySnapshot(in InputLatencySnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public void ApplyEvents(InputLatencySampleEvent[] events, int droppedEventCount)
    {
    }

    internal void ConfigureLocalPresentation(bool controllerCloseEnabled, bool showCloseHint)
    {
    }
#endif
}
