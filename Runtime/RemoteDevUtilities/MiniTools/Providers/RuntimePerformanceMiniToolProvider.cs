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
    internal sealed class RuntimePerformanceMiniToolProvider : MiniToolDataProvider, IMiniToolFieldProvider, IMiniToolSnapshotProvider<StatsSnapshot>, IMiniToolSnapshotProvider<FPSSnapshot>, IRemoteMiniToolSnapshotCapture
    {
        private const double BytesPerMebibyte = 1024d * 1024d;

        private readonly FrameTiming[] _frameTimings = new FrameTiming[1];

        private PerformanceSampleCursor _statsCursor;
        private PerformanceSampleCursor _fpsCursor;

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
            _hasPendingStatsSnapshot = false;
            _hasPendingFpsSnapshot = false;
            PerformanceSnapshotSource.Tick(PerformanceOverlaySelection.UseDetailedStats);
        }

        bool IRemoteMiniToolSnapshotCapture.TryCapture(out string snapshotTypeName, out string snapshotJson)
        {
            if (PerformanceOverlaySelection.UseDetailedStats)
            {
                if (!TryGetStatsSnapshot(out StatsSnapshot statsSnapshot))
                {
                    snapshotTypeName = string.Empty;
                    snapshotJson = string.Empty;
                    return false;
                }

                return RemoteMiniToolSnapshotSerializer.TrySerialize(in statsSnapshot, out snapshotTypeName, out snapshotJson);
            }

            if (!TryGetFpsSnapshot(out FPSSnapshot fpsSnapshot))
            {
                snapshotTypeName = string.Empty;
                snapshotJson = string.Empty;
                return false;
            }

            return RemoteMiniToolSnapshotSerializer.TrySerialize(in fpsSnapshot, out snapshotTypeName, out snapshotJson);
        }

        bool IMiniToolSnapshotProvider<StatsSnapshot>.TryGetSnapshot(out StatsSnapshot snapshot) => TryGetStatsSnapshot(out snapshot);

        bool IMiniToolSnapshotProvider<FPSSnapshot>.TryGetSnapshot(out FPSSnapshot snapshot) => TryGetFpsSnapshot(out snapshot);

        public RemoteMiniToolField[] CaptureFields()
        {
            bool hasSnapshot = TryGetStatsSnapshot(out StatsSnapshot snapshot);
            var fields = new List<RemoteMiniToolField>(8)
            {
                CreateField("fps", "FPS", (hasSnapshot ? snapshot.AverageFps : 0d).ToString("F1"), "fps"),
                CreateField("frameTime", "Average Frame Time", (hasSnapshot ? snapshot.AverageFrameTimeMs : 0d).ToString("F2"), "ms"),
                CreateField("targetFrameRate", "Target Frame Rate", (hasSnapshot ? snapshot.TargetFrameRate : Application.targetFrameRate).ToString(), "fps"),
                CreateField("vSync", "VSync Count", (hasSnapshot ? snapshot.VSyncCount : QualitySettings.vSyncCount).ToString(), string.Empty),
                CreateField("allocatedMemory", "Allocated Memory", ToMebibytes(hasSnapshot ? snapshot.AllocatedMemoryBytes : 0L).ToString("F2"), "MiB"),
                CreateField("reservedMemory", "Reserved Memory", ToMebibytes(hasSnapshot ? snapshot.ReservedMemoryBytes : 0L).ToString("F2"), "MiB"),
                CreateField("monoHeap", "Mono Heap", ToMebibytes(Profiler.GetMonoHeapSizeLong()).ToString("F2"), "MiB")
            };

            return fields.ToArray();
        }

        private bool TryGetStatsSnapshot(out StatsSnapshot snapshot)
        {
            if (_hasPendingStatsSnapshot)
            {
                snapshot = _pendingStatsSnapshot;
                return true;
            }

            snapshot = default;
            if (!PerformanceSnapshotSource.TryConsume(ref _statsCursor, out double elapsedSeconds, out int frames) || !StatsSnapshotCollector.TryCapture(elapsedSeconds, frames, _frameTimings, out snapshot))
                return false;

            _pendingStatsSnapshot = snapshot;
            _hasPendingStatsSnapshot = true;
            return true;
        }

        private bool TryGetFpsSnapshot(out FPSSnapshot snapshot)
        {
            if (_hasPendingFpsSnapshot)
            {
                snapshot = _pendingFpsSnapshot;
                return true;
            }

            snapshot = default;
            if (!PerformanceSnapshotSource.TryConsume(ref _fpsCursor, out double elapsedSeconds, out int frames) || !FPSSnapshotCollector.TryCapture(elapsedSeconds, frames, FPSSnapshotCollector.DefaultFallbackTargetFrameRate, out snapshot))
                return false;

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
            _statsCursor = PerformanceSnapshotSource.CreateCursor();
            _fpsCursor = PerformanceSnapshotSource.CreateCursor();
            _pendingStatsSnapshot = default;
            _hasPendingStatsSnapshot = false;
            _pendingFpsSnapshot = default;
            _hasPendingFpsSnapshot = false;
        }
    }
}
