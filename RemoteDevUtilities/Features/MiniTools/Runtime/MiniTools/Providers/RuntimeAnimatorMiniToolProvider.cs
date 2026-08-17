using SAS.DevUtilities;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using Unity.Profiling;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimeAnimatorMiniToolProvider : MiniToolDataProvider<AnimatorStatsSnapshot>, IMiniToolFieldProvider
    {
        private ProfilerRecorder _animationUpdateRecorder;
        private AnimatorStatsSnapshot _latestSnapshot;
        private bool _hasLatestSnapshot;

        public override void Start()
        {
            DisposeRecorder();
            _animationUpdateRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Animation, "Animators.Update");
            _hasLatestSnapshot = false;
        }

        public override void Stop()
        {
            DisposeRecorder();
            _latestSnapshot = default;
            _hasLatestSnapshot = false;
        }

        public override bool TryGetSnapshot(out AnimatorStatsSnapshot snapshot)
        {
            snapshot = AnimatorStatsSnapshotCollector.Capture(in _animationUpdateRecorder);
            _latestSnapshot = snapshot;
            _hasLatestSnapshot = true;
            return true;
        }

        public RemoteMiniToolField[] CaptureFields()
        {
            if (!_hasLatestSnapshot)
                TryGetSnapshot(out _latestSnapshot);

            AnimatorStatsSnapshot snapshot = _latestSnapshot;
            int active = snapshot.ActiveAlways + snapshot.ActiveCullUpdate + snapshot.ActiveCullCompletely;
            int disabled = snapshot.DisabledAlways + snapshot.DisabledCullUpdate + snapshot.DisabledCullCompletely;
            return new[]
            {
                CreateField("total", "Total Animators", snapshot.Total.ToString()),
                CreateField("initialized", "Initialized", snapshot.Initialized.ToString()),
                CreateField("active", "Active Animators", active.ToString()),
                CreateField("disabled", "Disabled Animators", disabled.ToString()),
                CreateField("activeAlways", "Active / Always", snapshot.ActiveAlways.ToString()),
                CreateField("activeCullUpdate", "Active / Cull Update", snapshot.ActiveCullUpdate.ToString()),
                CreateField("activeCullCompletely", "Active / Cull Completely", snapshot.ActiveCullCompletely.ToString()),
                CreateField("cpu", "Animator CPU", snapshot.HasCpuTiming ? snapshot.CpuTimeMs.ToString("F3") : "Unavailable", snapshot.HasCpuTiming ? "ms" : string.Empty)
            };
        }

        private void DisposeRecorder()
        {
            if (_animationUpdateRecorder.Valid)
                _animationUpdateRecorder.Dispose();
        }
    }
}
