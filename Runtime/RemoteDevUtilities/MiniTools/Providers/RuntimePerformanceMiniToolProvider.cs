using System;
using System.Collections.Generic;
using SAS.DevUtilities;
using SAS.DevUtilities.Stats;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine;
using UnityEngine.Profiling;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimePerformanceMiniToolProvider :
        MiniToolDataProvider,
        IMiniToolFieldProvider,
        IMiniToolSnapshotProvider<StatsSnapshot>,
        IMiniToolSnapshotProvider<FPSSnapshot>,
        IRemoteMiniToolSnapshotCapture
    {
        private const double BytesPerMebibyte = 1024d * 1024d;

        private double _elapsed;
        private int _frames;

        private readonly FrameTiming[] _frameTimings = new FrameTiming[1];

        private StatsSnapshot _pendingStatsSnapshot;
        private bool _hasPendingStatsSnapshot;
        private FPSSnapshot _pendingFpsSnapshot;
        private bool _hasPendingFpsSnapshot;

        public override void Start()
        {
            ResetSample();
        }

        public override void Stop()
        {
            ResetSample();
        }

        public override void Tick()
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f ||
                float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime))
            {
                return;
            }

            FrameTimingManager.CaptureFrameTimings();
            _elapsed += deltaTime;
            _frames++;
        }

        bool IRemoteMiniToolSnapshotCapture.TryCapture(
            out string snapshotTypeName,
            out string snapshotJson)
        {
            if (PerformanceOverlaySelection.UseDetailedStats)
            {
                if (!TryGetStatsSnapshot(out StatsSnapshot statsSnapshot))
                {
                    snapshotTypeName = string.Empty;
                    snapshotJson = string.Empty;
                    return false;
                }

                return RemoteMiniToolSnapshotSerializer.TrySerialize(
                    in statsSnapshot,
                    out snapshotTypeName,
                    out snapshotJson);
            }

            if (!TryGetFpsSnapshot(out FPSSnapshot fpsSnapshot))
            {
                snapshotTypeName = string.Empty;
                snapshotJson = string.Empty;
                return false;
            }

            return RemoteMiniToolSnapshotSerializer.TrySerialize(
                in fpsSnapshot,
                out snapshotTypeName,
                out snapshotJson);
        }

        bool IMiniToolSnapshotProvider<StatsSnapshot>.TryGetSnapshot(
            out StatsSnapshot snapshot) =>
            TryGetStatsSnapshot(out snapshot);

        bool IMiniToolSnapshotProvider<FPSSnapshot>.TryGetSnapshot(
            out FPSSnapshot snapshot) =>
            TryGetFpsSnapshot(out snapshot);

        public RemoteMiniToolField[] CaptureFields()
        {
            bool hasSnapshot =
                TryGetStatsSnapshot(out StatsSnapshot snapshot);
            var fields = new List<RemoteMiniToolField>(8)
            {
                CreateField(
                    "fps",
                    "FPS",
                    (hasSnapshot
                        ? snapshot.AverageFps
                        : 0d).ToString("F1"),
                    "fps"),
                CreateField(
                    "frameTime",
                    "Average Frame Time",
                    (hasSnapshot
                        ? snapshot.AverageFrameTimeMs
                        : 0d).ToString("F2"),
                    "ms"),
                CreateField(
                    "targetFrameRate",
                    "Target Frame Rate",
                    (hasSnapshot
                        ? snapshot.TargetFrameRate
                        : Application.targetFrameRate).ToString(),
                    "fps"),
                CreateField(
                    "vSync",
                    "VSync Count",
                    (hasSnapshot
                        ? snapshot.VSyncCount
                        : QualitySettings.vSyncCount).ToString(),
                    string.Empty),
                CreateField(
                    "allocatedMemory",
                    "Allocated Memory",
                    ToMebibytes(
                        hasSnapshot
                            ? snapshot.AllocatedMemoryBytes
                            : 0L).ToString("F2"),
                    "MiB"),
                CreateField(
                    "reservedMemory",
                    "Reserved Memory",
                    ToMebibytes(
                        hasSnapshot
                            ? snapshot.ReservedMemoryBytes
                            : 0L).ToString("F2"),
                    "MiB"),
                CreateField(
                    "monoHeap",
                    "Mono Heap",
                    ToMebibytes(
                        Profiler.GetMonoHeapSizeLong()).ToString("F2"),
                    "MiB")
            };

            ResetSample();
            return fields.ToArray();
        }

        private bool TryGetStatsSnapshot(
            out StatsSnapshot snapshot)
        {
            if (_hasPendingStatsSnapshot)
            {
                snapshot = _pendingStatsSnapshot;
                return true;
            }

            if (!StatsSnapshotCollector.TryCapture(
                    _elapsed,
                    _frames,
                    _frameTimings,
                    out snapshot))
            {
                return false;
            }

            _pendingStatsSnapshot = snapshot;
            _hasPendingStatsSnapshot = true;
            return true;
        }

        private bool TryGetFpsSnapshot(
            out FPSSnapshot snapshot)
        {
            if (_hasPendingFpsSnapshot)
            {
                snapshot = _pendingFpsSnapshot;
                return true;
            }

            if (!FPSSnapshotCollector.TryCapture(
                    _elapsed,
                    _frames,
                    FPSSnapshotCollector
                        .DefaultFallbackTargetFrameRate,
                    out snapshot))
            {
                return false;
            }

            _pendingFpsSnapshot = snapshot;
            _hasPendingFpsSnapshot = true;
            return true;
        }

        private static double ToMebibytes(long bytes)
        {
            return bytes / BytesPerMebibyte;
        }

        private void ResetSample()
        {
            _elapsed = 0d;
            _frames = 0;
            _pendingStatsSnapshot = default;
            _hasPendingStatsSnapshot = false;
            _pendingFpsSnapshot = default;
            _hasPendingFpsSnapshot = false;
        }

    }
}
