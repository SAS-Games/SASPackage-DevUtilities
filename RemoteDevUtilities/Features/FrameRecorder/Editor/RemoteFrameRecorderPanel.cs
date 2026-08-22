using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.Protocol.FrameRecorder;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.FrameRecorder
{
    internal sealed class RemoteFrameRecorderPanel : IRemoteSceneInspectorMode
    {
        private static readonly string[] WidthLabels = { "480", "640", "960" };
        private static readonly int[] WidthValues = { 480, 640, 960 };
        private static readonly string[] PlaybackSpeedLabels = { "0.25x", "0.5x", "1x", "2x" };
        private static readonly float[] PlaybackSpeedValues = { 0.25f, 0.5f, 1f, 2f };
        private static readonly string[] InspectorScopeLabels =
            { "Selected object", "Hierarchy only", "All objects (slow)" };
        private static readonly RemoteFrameRecorderInspectorScope[] InspectorScopeValues =
        {
            RemoteFrameRecorderInspectorScope.SelectedObject,
            RemoteFrameRecorderInspectorScope.HierarchyOnly,
            RemoteFrameRecorderInspectorScope.AllObjects
        };

        private readonly RemoteHierarchyView _hierarchy = new();
        private readonly RemoteInspectorView _inspector = new();
        private readonly RemoteHierarchyView _liveHierarchy = new();
        private readonly RemoteInspectorView _liveInspector = new();
        private Action _repaint;
        private RemoteRuntimeSceneInspectorClient _replayClient;
        private Texture2D _texture;
        private int _textureUnityFrame = int.MinValue;
        private int _loadedUnityFrame = int.MinValue;
        private int _selectedFrameIndex;
        private int _capacity = RemoteFrameRecorderLimits.DefaultCapacity;
        private int _widthIndex = 1;
        private int _jpegQuality = 60;
        private int _inspectorScopeIndex;
        private bool _freezeWhenFetched;
        private bool _playing;
        private double _nextPlaybackAt;
        private IReadOnlyList<RemoteFrameReplayFrame> _playbackFrames;
        private int _playbackSpeedIndex = 2;
        private int _sessionGeneration = int.MinValue;
        private int _replayGeneration;
        private Vector2 _hierarchyScroll;
        private Vector2 _captureScroll;
        private Vector2 _inspectorScroll;

        public string DisplayName => "Frame Recording";

        public void Initialize(Action repaint)
        {
            _repaint = repaint;
            _replayClient = new RemoteRuntimeSceneInspectorClient(new ReplaySession(repaint));
            EditorApplication.update += TickPlayback;
        }

        public bool Draw(RemoteDevUtilitiesClient client, bool connected, Rect windowRect)
        {
            RemoteFrameRecorderClient recorder = client.GetRequiredFeature<RemoteFrameRecorderClient>();
            RemoteRuntimeSceneInspectorClient liveInspector =
                client.GetRequiredFeature<RemoteRuntimeSceneInspectorClient>();
            if (_sessionGeneration != recorder.SessionGeneration)
            {
                _sessionGeneration = recorder.SessionGeneration;
                ResetReplayView();
            }

            if (!connected)
            {
                EditorGUILayout.HelpBox("Connect to a development Player to record recent frames.",
                    MessageType.Info);
                return false;
            }

            DrawToolbar(recorder, liveInspector);
            DrawStatus(recorder);
            DrawReplay(recorder, liveInspector, windowRect);
            // The workspace-level return value requests forced outer-window scrolling.
            // Playback has its own repaint loop and must never move the workspace scroll.
            return false;
        }

        public void Deactivate() => _playing = false;

        public void Dispose()
        {
            EditorApplication.update -= TickPlayback;
            DestroyTexture();
            _replayClient?.Reset();
            _replayClient = null;
            _repaint = null;
        }

        private void DrawToolbar(RemoteFrameRecorderClient client,
            RemoteRuntimeSceneInspectorClient liveInspector)
        {
            RemoteFrameRecorderState state = client.Status?.State ?? RemoteFrameRecorderState.Idle;
            bool busy = client.IsControlPending || client.IsDownloading;
            RemoteFrameRecorderInspectorScope scope = InspectorScopeValues[_inspectorScopeIndex];
            long inspectedObjectId = liveInspector.InspectionObjectId;
            bool missingSelection = scope == RemoteFrameRecorderInspectorScope.SelectedObject &&
                                    inspectedObjectId <= 0;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(busy || state == RemoteFrameRecorderState.Recording ||
                                                missingSelection))
            {
                if (GUILayout.Button("Arm Recorder", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                {
                    ResetReplayView();
                    client.Start(_capacity, WidthValues[_widthIndex], _jpegQuality,
                        scope, inspectedObjectId);
                }
            }

            using (new EditorGUI.DisabledScope(busy ||
                                                (state != RemoteFrameRecorderState.Recording &&
                                                 state != RemoteFrameRecorderState.Sealed)))
            {
                string fetchLabel = state == RemoteFrameRecorderState.Sealed
                    ? "Download Frames"
                    : "Fetch Last Frames";
                if (GUILayout.Button(fetchLabel, EditorStyles.toolbarButton, GUILayout.Width(112f)))
                {
                    ResetReplayView();
                    client.SealAndFetch(_freezeWhenFetched);
                }
            }

            using (new EditorGUI.DisabledScope(busy || state == RemoteFrameRecorderState.Idle))
            {
                if (GUILayout.Button("Release", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                {
                    _playing = false;
                    client.Release();
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Frames", EditorStyles.miniLabel, GUILayout.Width(42f));
            _capacity = Mathf.Clamp(EditorGUILayout.DelayedIntField(_capacity, GUILayout.Width(48f)),
                RemoteFrameRecorderLimits.MinimumCapacity,
                RemoteFrameRecorderLimits.MaximumCapacity);
            GUILayout.Label("Width", EditorStyles.miniLabel, GUILayout.Width(34f));
            _widthIndex = EditorGUILayout.Popup(_widthIndex, WidthLabels,
                EditorStyles.toolbarPopup, GUILayout.Width(48f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Inspector", GUILayout.Width(58f));
            _inspectorScopeIndex = EditorGUILayout.Popup(_inspectorScopeIndex, InspectorScopeLabels,
                GUILayout.Width(140f));
            if (scope == RemoteFrameRecorderInspectorScope.SelectedObject)
            {
                string selectedName = liveInspector.Inspection?.Details?.Name;
                GUILayout.Label(inspectedObjectId > 0
                        ? $"Recording: {selectedName ?? inspectedObjectId.ToString()}"
                        : "Select an object in the Live Hierarchy below",
                    EditorStyles.miniLabel, GUILayout.MinWidth(180f));
            }
            _freezeWhenFetched = EditorGUILayout.ToggleLeft("Freeze Player when fetched",
                _freezeWhenFetched, GUILayout.Width(178f));
            GUILayout.Label("JPEG quality", GUILayout.Width(76f));
            _jpegQuality = EditorGUILayout.IntSlider(_jpegQuality, 35, 90, GUILayout.MaxWidth(280f));
            EditorGUILayout.EndHorizontal();

            if (scope == RemoteFrameRecorderInspectorScope.AllObjects)
            {
                EditorGUILayout.HelpBox(
                    "All objects rebuilds every component/material inspector each frame and can cause significant Player lag. Prefer Selected object for normal debugging.",
                    MessageType.Warning);
            }
            if (_capacity > 100)
            {
                EditorGUILayout.HelpBox(
                    "Large recordings use more Player memory and take longer to download. Use a smaller width and Selected object or Hierarchy only when approaching 300 frames.",
                    MessageType.Info);
            }
        }

        private static void DrawStatus(RemoteFrameRecorderClient client)
        {
            if (!string.IsNullOrEmpty(client.DownloadError))
            {
                EditorGUILayout.HelpBox(client.DownloadError, MessageType.Error);
                return;
            }

            RemoteFrameRecorderControlResponse status = client.Status;
            if (client.IsControlPending && status.State != RemoteFrameRecorderState.Finalizing)
            {
                EditorGUILayout.HelpBox("Waiting for the Player...", MessageType.Info);
                return;
            }

            if (client.IsDownloading)
            {
                int total = Mathf.Max(1, client.DownloadFrameCount);
                Rect progress = EditorGUILayout.GetControlRect(false, 18f);
                EditorGUI.ProgressBar(progress, client.DownloadedFrameCount / (float)total,
                    $"Downloading frame {Mathf.Min(client.DownloadedFrameCount + 1, total)} of {total}");
                return;
            }

            switch (status.State)
            {
                case RemoteFrameRecorderState.Recording:
                    string inspectorScope = status.InspectorScope switch
                    {
                        RemoteFrameRecorderInspectorScope.SelectedObject => "selected-object inspector",
                        RemoteFrameRecorderInspectorScope.AllObjects => "all-object inspectors",
                        _ => "hierarchy only"
                    };
                    EditorGUILayout.HelpBox(
                        $"Recording each rendered frame locally ({inspectorScope}). The Player retains its latest {status.Capacity} frames and sends no frame data until requested.",
                        MessageType.Info);
                    break;
                case RemoteFrameRecorderState.Finalizing:
                    EditorGUILayout.HelpBox(
                        $"Finalizing {status.PendingFrameCount} background frame operation(s)...",
                        MessageType.Info);
                    break;
                case RemoteFrameRecorderState.Sealed:
                    string gaps = status.MissedFrameCount == 0
                        ? "continuous"
                        : $"{status.MissedFrameCount} missed";
                    string readback = status.UsesAsyncGpuReadback
                        ? "async GPU readback"
                        : "synchronous readback fallback";
                    string graphSavings = status.SceneGraphBytesSaved > 0
                        ? $", graph reuse saved {FormatBytes(status.SceneGraphBytesSaved)}"
                        : string.Empty;
                    EditorGUILayout.LabelField(
                        $"Sealed: {status.CapturedFrameCount}/{status.Capacity} frames, {gaps}, {FormatBytes(status.StoredBytes)}{graphSavings}, {readback} + background encoding",
                        EditorStyles.centeredGreyMiniLabel);
                    if (!string.IsNullOrEmpty(status.Warning))
                        EditorGUILayout.HelpBox(status.Warning, MessageType.Warning);
                    break;
                default:
                    EditorGUILayout.HelpBox(
                        "Arm the recorder, reproduce the problem, then fetch the latest consecutive frames.",
                        MessageType.None);
                    break;
            }
        }

        private void DrawReplay(RemoteFrameRecorderClient client,
            RemoteRuntimeSceneInspectorClient liveInspector, Rect windowRect)
        {
            IReadOnlyList<RemoteFrameReplayFrame> frames = client.ReplayFrames;
            if (frames == null || frames.Count == 0)
            {
                _playbackFrames = null;
                DrawLiveSelection(liveInspector, client.Status?.State ?? RemoteFrameRecorderState.Idle,
                    windowRect);
                return;
            }

            _playbackFrames = frames;
            _selectedFrameIndex = Mathf.Clamp(_selectedFrameIndex, 0, frames.Count - 1);
            DrawTimeline(frames);
            RemoteFrameReplayFrame frame = frames[_selectedFrameIndex];
            EnsureFrame(frame);

            if (!string.IsNullOrEmpty(frame.SceneGraph?.Error))
                EditorGUILayout.HelpBox(frame.SceneGraph.Error, MessageType.Warning);

            float contentWidth = Mathf.Max(680f, windowRect.width - 24f);
            float hierarchyWidth = Mathf.Clamp(contentWidth * 0.24f, 190f, 300f);
            float captureWidth = Mathf.Clamp(contentWidth * 0.4f, 320f, 720f);
            float columnHeight = Mathf.Max(300f, windowRect.height - 285f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(hierarchyWidth),
                GUILayout.Height(columnHeight));
            _hierarchyScroll = EditorGUILayout.BeginScrollView(_hierarchyScroll);
            EditorGUILayout.LabelField("Recorded Hierarchy", EditorStyles.boldLabel);
            _hierarchy.Draw(_replayClient);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(captureWidth),
                GUILayout.Height(columnHeight));
            _captureScroll = EditorGUILayout.BeginScrollView(_captureScroll);
            DrawImage(frame, captureWidth);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(columnHeight));
            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
            EditorGUILayout.LabelField("Recorded Inspector", EditorStyles.boldLabel);
            _inspector.Draw(_replayClient);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLiveSelection(RemoteRuntimeSceneInspectorClient liveInspector,
            RemoteFrameRecorderState recorderState, Rect windowRect)
        {
            float contentWidth = Mathf.Max(680f, windowRect.width - 24f);
            float hierarchyWidth = Mathf.Clamp(contentWidth * 0.24f, 190f, 300f);
            float centerWidth = Mathf.Clamp(contentWidth * 0.4f, 320f, 720f);
            float columnHeight = Mathf.Max(300f, windowRect.height - 265f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(hierarchyWidth),
                GUILayout.Height(columnHeight));
            _hierarchyScroll = EditorGUILayout.BeginScrollView(_hierarchyScroll);
            EditorGUILayout.LabelField("Live Hierarchy", EditorStyles.boldLabel);
            _liveHierarchy.Draw(liveInspector);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(centerWidth),
                GUILayout.Height(columnHeight));
            _captureScroll = EditorGUILayout.BeginScrollView(_captureScroll);
            EditorGUILayout.LabelField("Frame Recording", EditorStyles.boldLabel);
            if (liveInspector.InspectionObjectId <= 0)
            {
                EditorGUILayout.HelpBox(
                    "Select a GameObject from the Live Hierarchy. Selected-object recording stores that object's complete historical inspector on every frame.",
                    MessageType.Info);
            }
            else if (recorderState == RemoteFrameRecorderState.Recording)
            {
                EditorGUILayout.HelpBox(
                    "Recording is armed. The object chosen when Arm Recorder was pressed is being recorded; changing the live selection does not change the active recording.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Selected: {liveInspector.Inspection?.Details?.Name ?? liveInspector.InspectionObjectId.ToString()}. Press Arm Recorder, reproduce the issue, then Fetch Last Frames.",
                    MessageType.None);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(columnHeight));
            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
            EditorGUILayout.LabelField("Live Inspector", EditorStyles.boldLabel);
            _liveInspector.Draw(liveInspector);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTimeline(IReadOnlyList<RemoteFrameReplayFrame> frames)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(_playing ? "Pause" : "Play", EditorStyles.toolbarButton,
                    GUILayout.Width(46f)))
            {
                _playing = !_playing;
                if (_playing)
                    ScheduleNextPlaybackFrame(frames);
            }
            using (new EditorGUI.DisabledScope(_selectedFrameIndex <= 0))
            {
                if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(24f)))
                {
                    _playing = false;
                    _selectedFrameIndex--;
                }
            }
            int next = EditorGUILayout.IntSlider(_selectedFrameIndex, 0, frames.Count - 1);
            if (next != _selectedFrameIndex)
            {
                _playing = false;
                _selectedFrameIndex = next;
            }
            using (new EditorGUI.DisabledScope(_selectedFrameIndex >= frames.Count - 1))
            {
                if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(24f)))
                {
                    _playing = false;
                    _selectedFrameIndex++;
                }
            }
            RemoteRecordedFrameInfo info = frames[_selectedFrameIndex].Info;
            GUILayout.Label($"{_selectedFrameIndex + 1}/{frames.Count}  Unity frame {info.UnityFrame}",
                EditorStyles.miniLabel, GUILayout.Width(150f));
            GUILayout.Label("Speed", EditorStyles.miniLabel, GUILayout.Width(34f));
            int speedIndex = EditorGUILayout.Popup(_playbackSpeedIndex, PlaybackSpeedLabels,
                EditorStyles.toolbarPopup, GUILayout.Width(52f));
            if (speedIndex != _playbackSpeedIndex)
            {
                _playbackSpeedIndex = speedIndex;
                if (_playing)
                    ScheduleNextPlaybackFrame(frames);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawImage(RemoteFrameReplayFrame frame, float width)
        {
            EditorGUILayout.LabelField("Recorded Player View", EditorStyles.boldLabel);
            if (_texture == null)
            {
                EditorGUILayout.HelpBox("The recorded JPEG could not be decoded.", MessageType.Error);
            }
            else
            {
                float imageWidth = Mathf.Max(200f, width - 12f);
                float imageHeight = Mathf.Clamp(imageWidth * frame.Info.Height /
                                                Mathf.Max(1f, frame.Info.Width), 180f, 620f);
                Rect rect = GUILayoutUtility.GetRect(imageWidth, imageHeight, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, _texture, ScaleMode.ScaleToFit, false);
            }
            EditorGUILayout.LabelField(
                $"Frame {frame.Info.UnityFrame}  |  {frame.Info.Width}x{frame.Info.Height}  |  t={frame.Info.RealtimeSeconds:F3}s",
                EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField(
                $"Image {FormatBytes(frame.Info.ImageBytes)}  |  Inspector {FormatBytes(frame.Info.SceneGraphBytes)}",
                EditorStyles.centeredGreyMiniLabel);
        }

        private void EnsureFrame(RemoteFrameReplayFrame frame)
        {
            EnsureTexture(frame);
            if (_loadedUnityFrame == frame.Info.UnityFrame)
                return;
            _loadedUnityFrame = frame.Info.UnityFrame;
            RemoteRecordedSceneGraph graph = frame.SceneGraph ?? new RemoteRecordedSceneGraph();
            _replayClient.LoadRecordedSnapshot(graph.Hierarchy, graph.Inspections, _replayGeneration);
            if (_replayClient.InspectionObjectId == 0 && graph.Inspections?.Length > 0)
            {
                long objectId = graph.Inspections[0].Id;
                if (_hierarchy.SelectAndReveal(objectId, graph.Hierarchy))
                    _replayClient.Inspect(objectId);
            }
        }

        private void EnsureTexture(RemoteFrameReplayFrame frame)
        {
            if (_texture != null && _textureUnityFrame == frame.Info.UnityFrame)
                return;
            DestroyTexture();
            try
            {
                byte[] bytes = Convert.FromBase64String(frame.ImageBase64 ?? string.Empty);
                _texture = new Texture2D(2, 2, TextureFormat.RGB24, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    name = $"Recorded Frame {frame.Info.UnityFrame}"
                };
                if (!_texture.LoadImage(bytes, true))
                    DestroyTexture();
                else
                    _textureUnityFrame = frame.Info.UnityFrame;
            }
            catch
            {
                DestroyTexture();
            }
        }

        private void TickPlayback()
        {
            IReadOnlyList<RemoteFrameReplayFrame> frames = _playbackFrames;
            if (!_playing || frames == null || frames.Count == 0)
                return;
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextPlaybackAt)
                return;

            _selectedFrameIndex++;
            if (_selectedFrameIndex >= frames.Count)
                _selectedFrameIndex = 0;
            _nextPlaybackAt = now + CalculatePlaybackDelay(frames, _selectedFrameIndex,
                PlaybackSpeedValues[_playbackSpeedIndex]);
            _repaint?.Invoke();
        }

        private void ScheduleNextPlaybackFrame(IReadOnlyList<RemoteFrameReplayFrame> frames)
        {
            _nextPlaybackAt = EditorApplication.timeSinceStartup +
                              CalculatePlaybackDelay(frames, _selectedFrameIndex,
                                  PlaybackSpeedValues[_playbackSpeedIndex]);
        }

        internal static double CalculatePlaybackDelay(IReadOnlyList<RemoteFrameReplayFrame> frames,
            int currentIndex, float playbackSpeed)
        {
            const double fallbackInterval = 1d / 30d;
            if (frames == null || frames.Count < 2)
                return fallbackInterval / Math.Max(0.01f, playbackSpeed);

            int index = Mathf.Clamp(currentIndex, 0, frames.Count - 1);
            double interval;
            if (index < frames.Count - 1)
            {
                interval = frames[index + 1].Info.RealtimeSeconds -
                           frames[index].Info.RealtimeSeconds;
            }
            else
            {
                interval = frames[index].Info.RealtimeSeconds -
                           frames[index - 1].Info.RealtimeSeconds;
            }

            if (interval <= 0d || double.IsNaN(interval) || double.IsInfinity(interval))
                interval = fallbackInterval;
            double scaled = interval / Math.Max(0.01f, playbackSpeed);
            return Math.Max(1d / 240d, Math.Min(2d, scaled));
        }

        private void ResetReplayView()
        {
            _playing = false;
            _playbackFrames = null;
            _selectedFrameIndex = 0;
            _loadedUnityFrame = int.MinValue;
            _replayGeneration++;
            _replayClient?.LoadRecordedSnapshot(null, null, _replayGeneration);
            DestroyTexture();
        }

        private void DestroyTexture()
        {
            if (_texture != null)
                UnityEngine.Object.DestroyImmediate(_texture);
            _texture = null;
            _textureUnityFrame = int.MinValue;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return bytes + " B";
            if (bytes < 1024 * 1024)
                return (bytes / 1024f).ToString("F1") + " KB";
            return (bytes / (1024f * 1024f)).ToString("F1") + " MB";
        }

        private sealed class ReplaySession : IRemoteEditorSession
        {
            private readonly Action _notify;

            internal ReplaySession(Action notify) => _notify = notify;

            public bool IsConnected => true;
            public long Send<T>(string messageType, T payload) => 0;
            public void NotifyStateChanged() => _notify?.Invoke();
        }
    }
}
