using UnityEngine;

namespace SAS.DevUtilities
{
    /// <summary>
    /// Automatically publishes FrameStepper snapshots when the Player's time
    /// state changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FrameStepperSnapshotProvider : MiniToolSnapshotProviderBehaviour<FrameStepperSnapshot>
    {
        private FrameStepperSnapshot _lastSnapshot;
        private bool _hasLastSnapshot;

        private void OnEnable()
        {
            ClearSnapshot();
            _hasLastSnapshot = false;
            CaptureAndPublishIfChanged();
        }

        private void Update()
        {
            CaptureAndPublishIfChanged();
        }

        private void CaptureAndPublishIfChanged()
        {
            FrameStepperSnapshot snapshot =
                FrameStepperSnapshotCollector.Capture();

            if (_hasLastSnapshot && snapshot.IsPaused == _lastSnapshot.IsPaused &&
                Mathf.Approximately(snapshot.TimeScale, _lastSnapshot.TimeScale))
            {
                return;
            }

            _lastSnapshot = snapshot;
            _hasLastSnapshot = true;
            PublishSnapshot(in snapshot);
        }
    }
}
