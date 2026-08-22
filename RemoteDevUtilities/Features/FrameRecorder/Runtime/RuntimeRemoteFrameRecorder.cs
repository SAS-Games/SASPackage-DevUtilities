using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SAS.Utilities.RemoteDevUtilities.Protocol.FrameRecorder;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.RuntimeSceneInspector.Capture;
using SAS.Utilities.RuntimeSceneInspector.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace SAS.Utilities.RemoteDevUtilities.FrameRecorder
{
    [RuntimeSceneInspectorProtected]
    internal sealed class RuntimeRemoteFrameRecorder : MonoBehaviour
    {
        private const int MaximumPendingMultiplier = 2;
        private const int MaximumPendingFrames = 64;
        private static readonly object JpegEncodingGate = new();
        private readonly RuntimeFrameRecorderBuffer _buffer = new();
        private readonly RuntimeTimeScaleLease _freezeLease = new();
        private Coroutine _recordingCoroutine;
        private long _recordingId;
        private int _capacity;
        private int _maximumWidth;
        private int _jpegQuality;
        private RemoteFrameRecorderInspectorScope _inspectorScope;
        private long _inspectedObjectId;
        private RecordingWorkTracker _workTracker = new();
        private int _missedFrameCount;
        private int _lastScheduledUnityFrame;
        private string _lastError;
        private bool _recording;
        private bool _sealRequested;
        private long _cachedHierarchyRevision = long.MinValue;
        private RemoteSceneInspectorHierarchyResponse _cachedHierarchy;

        internal long RecordingId => _recordingId;
        internal bool IsRecording => _recording;
        internal bool IsFinalizing => _sealRequested && PendingFrameCount > 0;
        internal bool IsSealed => _sealRequested && PendingFrameCount == 0;
        internal int Capacity => _capacity;
        internal int CapturedFrameCount => _buffer.Count;
        internal int PendingFrameCount => _workTracker.PendingFrameCount;
        internal int MissedFrameCount => _missedFrameCount;
        internal long StoredBytes => _buffer.StoredBytes;
        internal long SceneGraphBytesSaved => _buffer.SceneGraphBytesSaved;
        internal bool UsesAsyncGpuReadback => SystemInfo.supportsAsyncGPUReadback;
        internal RemoteFrameRecorderInspectorScope InspectorScope => _inspectorScope;
        internal long InspectedObjectId => _inspectedObjectId;
        internal bool PlayerFrozen => _freezeLease.IsAcquired;
        internal string LastError => _lastError;

        internal void StartRecording(long recordingId, int capacity, int maximumWidth, int jpegQuality,
            RemoteFrameRecorderInspectorScope inspectorScope, long inspectedObjectId)
        {
            Release();
            _recordingId = recordingId;
            _capacity = Mathf.Clamp(capacity, RemoteFrameRecorderLimits.MinimumCapacity,
                RemoteFrameRecorderLimits.MaximumCapacity);
            _maximumWidth = Mathf.Clamp(maximumWidth, 320, 1280);
            _jpegQuality = Mathf.Clamp(jpegQuality, 35, 90);
            _inspectorScope = inspectorScope;
            _inspectedObjectId = inspectedObjectId;
            _workTracker = new RecordingWorkTracker();
            _buffer.Reset(recordingId, _capacity);
            _lastScheduledUnityFrame = -1;
            _missedFrameCount = 0;
            _lastError = string.Empty;
            _cachedHierarchyRevision = long.MinValue;
            _cachedHierarchy = null;
            _sealRequested = false;
            _recording = true;
            _recordingCoroutine = StartCoroutine(RecordConsecutiveFrames(recordingId));
        }

        internal void Seal(long recordingId, bool freezePlayer)
        {
            if (recordingId != 0 && recordingId != _recordingId)
                return;

            _recording = false;
            _sealRequested = _recordingId != 0;
            if (_recordingCoroutine != null)
            {
                StopCoroutine(_recordingCoroutine);
                _recordingCoroutine = null;
            }

            if (freezePlayer && _recordingId != 0)
                _freezeLease.Acquire();
        }

        internal void Release(long recordingId = 0)
        {
            if (recordingId != 0 && recordingId != _recordingId)
                return;

            _recording = false;
            _sealRequested = false;
            if (_recordingCoroutine != null)
            {
                StopCoroutine(_recordingCoroutine);
                _recordingCoroutine = null;
            }

            _freezeLease.Release();
            _buffer.Clear(recordingId);
            _recordingId = 0;
            _capacity = 0;
            _lastScheduledUnityFrame = -1;
            _cachedHierarchyRevision = long.MinValue;
            _cachedHierarchy = null;
        }

        internal RemoteRecordedFrameInfo[] GetManifest(long recordingId)
        {
            return recordingId == _recordingId && IsSealed
                ? _buffer.GetManifest()
                : Array.Empty<RemoteRecordedFrameInfo>();
        }

        internal bool TryGetFrame(long recordingId, int unityFrame, out RuntimeRecordedFrameData frame)
        {
            frame = null;
            return recordingId == _recordingId && IsSealed && _buffer.TryGet(unityFrame, out frame);
        }

        internal bool TryGetSceneGraphBlob(string snapshotId, out byte[] bytes) =>
            _buffer.TryGetSceneGraphBlob(snapshotId, out bytes);

        private IEnumerator RecordConsecutiveFrames(long recordingId)
        {
            while (_recording && recordingId == _recordingId)
            {
                yield return new WaitForEndOfFrame();
                if (!_recording || recordingId != _recordingId)
                    break;
                ScheduleFrame(recordingId);
            }

            _recordingCoroutine = null;
        }

        private void ScheduleFrame(long recordingId)
        {
            int unityFrame = Time.frameCount;
            if (_lastScheduledUnityFrame >= 0 && unityFrame != _lastScheduledUnityFrame + 1)
                _missedFrameCount += Mathf.Max(1, unityFrame - _lastScheduledUnityFrame - 1);
            _lastScheduledUnityFrame = unityFrame;

            int maximumPending = Mathf.Clamp(_capacity * MaximumPendingMultiplier, 4,
                MaximumPendingFrames);
            if (PendingFrameCount >= maximumPending)
            {
                _missedFrameCount++;
                _lastError = "The frame encoder could not keep up with the rendered frame rate.";
                return;
            }

            int width = Mathf.Min(_maximumWidth, Mathf.Max(1, Screen.width));
            var pending = new PendingFrame
            {
                RecordingId = recordingId,
                UnityFrame = unityFrame,
                RealtimeSeconds = Time.realtimeSinceStartupAsDouble,
                WorkTracker = _workTracker
            };

            pending.WorkTracker.Increment();
            try
            {
                pending.SceneGraph = CaptureSceneGraph();
                if (SystemInfo.supportsAsyncGPUReadback)
                    ScheduleAsyncReadback(pending, width);
                else
                    CaptureSynchronously(pending, width);
            }
            catch (Exception exception)
            {
                CompleteWithError(pending,
                    exception.GetType().Name + ": " + exception.Message);
            }
        }

        private void ScheduleAsyncReadback(PendingFrame pending, int width)
        {
            int sourceWidth = Mathf.Max(1, Screen.width);
            int sourceHeight = Mathf.Max(1, Screen.height);
            pending.Width = Mathf.Min(width, sourceWidth);
            pending.Height = Mathf.Max(1,
                Mathf.RoundToInt(sourceHeight * (pending.Width / (float)sourceWidth)));

            RenderTexture captureTarget = null;
            RenderTexture readbackTarget = null;
            try
            {
                // CaptureScreenshotIntoRenderTexture uses the supplied target as a render
                // destination. A reduced-size target can alter camera framing on some render
                // pipelines, producing a cropped or zoomed recording. Capture at the exact
                // Player backbuffer size first, then downsample the completed view on the GPU.
                captureTarget = RenderTexture.GetTemporary(sourceWidth, sourceHeight, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(captureTarget);

                bool needsVerticalFlip = SystemInfo.graphicsUVStartsAtTop;
                bool needsResize = pending.Width != sourceWidth || pending.Height != sourceHeight;
                if (!needsResize && !needsVerticalFlip)
                {
                    readbackTarget = captureTarget;
                    captureTarget = null;
                }
                else
                {
                    readbackTarget = RenderTexture.GetTemporary(pending.Width, pending.Height, 0,
                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                    readbackTarget.filterMode = FilterMode.Bilinear;
                    if (needsVerticalFlip)
                    {
                        Graphics.Blit(captureTarget, readbackTarget,
                            new Vector2(1f, -1f), new Vector2(0f, 1f));
                    }
                    else
                    {
                        Graphics.Blit(captureTarget, readbackTarget);
                    }
                    RenderTexture.ReleaseTemporary(captureTarget);
                    captureTarget = null;
                }

                const GraphicsFormat readbackFormat = GraphicsFormat.R8G8B8A8_UNorm;
                RenderTexture capturedTarget = readbackTarget;
                AsyncGPUReadback.Request(readbackTarget, 0, TextureFormat.RGBA32, request =>
                {
                    try
                    {
                        if (request.hasError)
                            throw new InvalidOperationException(
                                $"GPU readback failed for Unity frame {pending.UnityFrame}.");
                        byte[] pixels = request.GetData<byte>().ToArray();
                        QueueEncoding(pending, pixels, readbackFormat);
                    }
                    catch (Exception exception)
                    {
                        CompleteWithError(pending,
                            exception.GetType().Name + ": " + exception.Message);
                    }
                    finally
                    {
                        RenderTexture.ReleaseTemporary(capturedTarget);
                    }
                });
                readbackTarget = null;
            }
            finally
            {
                if (captureTarget != null)
                    RenderTexture.ReleaseTemporary(captureTarget);
                if (readbackTarget != null)
                    RenderTexture.ReleaseTemporary(readbackTarget);
            }
        }

        private void CaptureSynchronously(PendingFrame pending, int width)
        {
            RuntimeRemoteScreenCaptureData capture = RuntimeRemoteScreenCapture.Capture(width);
            pending.Width = capture.Width;
            pending.Height = capture.Height;
            QueueEncoding(pending, capture.Pixels, capture.GraphicsFormat);
        }

        private void QueueEncoding(PendingFrame pending, byte[] pixels, GraphicsFormat format)
        {
            int quality = _jpegQuality;
            Task.Run(() =>
            {
                try
                {
                    byte[] jpeg;
                    lock (JpegEncodingGate)
                    {
                        jpeg = ImageConversion.EncodeArrayToJPG(
                            pixels, format, (uint)pending.Width, (uint)pending.Height, 0, quality);
                    }
                    if (jpeg == null || jpeg.Length == 0)
                        throw new InvalidOperationException("The frame encoder returned no JPEG data.");

                    RemoteRecordedSceneGraph graph = pending.SceneGraph ?? new RemoteRecordedSceneGraph();
                    RuntimeSceneGraphSectionData hierarchySection =
                        RuntimeSceneGraphSectionData.Create(graph.Hierarchy ??
                                                            new RemoteSceneInspectorHierarchyResponse());
                    RuntimeSceneGraphSectionData inspectorSection =
                        RuntimeSceneGraphSectionData.Create(new RemoteRecordedInspectorSnapshot
                        {
                            Inspections = graph.Inspections ?? Array.Empty<RemoteObjectDetails>(),
                            Error = graph.Error
                        });
                    _buffer.Add(new RuntimeRecordedFrameData
                    {
                        RecordingId = pending.RecordingId,
                        UnityFrame = pending.UnityFrame,
                        RealtimeSeconds = pending.RealtimeSeconds,
                        Width = pending.Width,
                        Height = pending.Height,
                        JpegBytes = jpeg,
                        HierarchySection = hierarchySection,
                        InspectorSection = inspectorSection
                    });
                }
                catch (Exception exception)
                {
                    SetWorkerError(pending.RecordingId,
                        exception.GetType().Name + ": " + exception.Message);
                }
                finally
                {
                    pending.WorkTracker.Decrement();
                }
            });
        }

        private void CompleteWithError(PendingFrame pending, string error)
        {
            SetWorkerError(pending.RecordingId, error);
            pending.WorkTracker.Decrement();
        }

        private void SetWorkerError(long recordingId, string error)
        {
            if (recordingId != _recordingId)
                return;
            _lastError = error ?? "Frame recording failed.";
            Interlocked.Increment(ref _missedFrameCount);
        }

        private RemoteRecordedSceneGraph CaptureSceneGraph()
        {
            RuntimeSceneInspectorService service = RemoteRuntimeSceneInspectorEndpoint.ActiveService;
            if (service == null)
            {
                return new RemoteRecordedSceneGraph
                {
                    Error = "Runtime Scene Inspector is disabled; no recorded inspector data is available."
                };
            }

            service.RefreshHierarchy();
            RuntimeHierarchySnapshot runtimeHierarchy = service.GetHierarchySnapshot();
            if (_cachedHierarchy == null || runtimeHierarchy.Revision != _cachedHierarchyRevision)
            {
                _cachedHierarchy = RuntimeSceneInspectorProtocolMapper.ToRemote(runtimeHierarchy);
                _cachedHierarchyRevision = runtimeHierarchy.Revision;
            }
            RemoteSceneInspectorHierarchyResponse hierarchy = _cachedHierarchy;
            var inspections = new List<RemoteObjectDetails>();
            if (_inspectorScope == RemoteFrameRecorderInspectorScope.SelectedObject)
            {
                AddInspection(service, _inspectedObjectId, inspections);
            }
            else if (_inspectorScope == RemoteFrameRecorderInspectorScope.AllObjects)
            {
                foreach (RemoteHierarchyEntry entry in
                         hierarchy.Entries ?? Array.Empty<RemoteHierarchyEntry>())
                {
                    if (entry != null && entry.Kind != 0)
                        AddInspection(service, entry.Id, inspections);
                }
            }

            return new RemoteRecordedSceneGraph
            {
                Hierarchy = hierarchy,
                Inspections = inspections.ToArray()
            };
        }

        private static void AddInspection(RuntimeSceneInspectorService service, long objectId,
            List<RemoteObjectDetails> inspections)
        {
            if (objectId <= 0)
                return;
            RuntimeObjectDetails runtimeDetails = service.InspectObject(new RuntimeObjectId(objectId));
            RemoteObjectDetails remoteDetails = RuntimeSceneInspectorProtocolMapper.ToRemote(runtimeDetails);
            if (remoteDetails != null)
                inspections.Add(remoteDetails);
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();

        private sealed class PendingFrame
        {
            internal long RecordingId;
            internal int UnityFrame;
            internal double RealtimeSeconds;
            internal int Width;
            internal int Height;
            internal RemoteRecordedSceneGraph SceneGraph;
            internal RecordingWorkTracker WorkTracker;
        }

        private sealed class RecordingWorkTracker
        {
            private int _pendingFrameCount;

            internal int PendingFrameCount => Volatile.Read(ref _pendingFrameCount);

            internal void Increment() => Interlocked.Increment(ref _pendingFrameCount);
            internal void Decrement() => Interlocked.Decrement(ref _pendingFrameCount);
        }
    }

    internal sealed class RuntimeTimeScaleLease
    {
        private float _savedTimeScale;
        internal bool IsAcquired { get; private set; }

        internal void Acquire()
        {
            if (IsAcquired)
                return;
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            IsAcquired = true;
        }

        internal void Release()
        {
            if (!IsAcquired)
                return;
            Time.timeScale = _savedTimeScale;
            IsAcquired = false;
        }
    }
}
