using SAS.DevUtilities;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using Unity.Profiling;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimeParticleMiniToolProvider : MiniToolDataProvider<ParticleStatsSnapshot>, IMiniToolFieldProvider
    {
        private ProfilerRecorder _particleUpdateRecorder;
        private ParticleStatsSnapshot _latestSnapshot;
        private bool _hasLatestSnapshot;

        public override void Start()
        {
            DisposeRecorder();
            _particleUpdateRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Particles, "ParticleSystem.Update");
            _hasLatestSnapshot = false;
        }

        public override void Stop()
        {
            DisposeRecorder();
            _latestSnapshot = default;
            _hasLatestSnapshot = false;
        }

        public override bool TryGetSnapshot(out ParticleStatsSnapshot snapshot)
        {
            snapshot = ParticleStatsSnapshotCollector.Capture(in _particleUpdateRecorder);
            _latestSnapshot = snapshot;
            _hasLatestSnapshot = true;
            return true;
        }

        public RemoteMiniToolField[] CaptureFields()
        {
            if (!_hasLatestSnapshot)
                TryGetSnapshot(out _latestSnapshot);

            ParticleStatsSnapshot snapshot = _latestSnapshot;

            return new[]
            {
                CreateField("total", "Particle Systems", snapshot.TotalSystems.ToString()),
                CreateField("active", "Active Systems", snapshot.ActiveSystems.ToString()),
                CreateField("alive", "Alive Systems", snapshot.AliveSystems.ToString()),
                CreateField("disabled", "Disabled Systems", snapshot.DisabledSystems.ToString()),
                CreateField("particles", "Live Particles", snapshot.LiveParticles.ToString()),
                CreateField("cpu", "Particle CPU", snapshot.HasCpuTiming ? snapshot.CpuTimeMs.ToString("F3") : "Unavailable", snapshot.HasCpuTiming ? "ms" : string.Empty)
            };
        }

        private void DisposeRecorder()
        {
            if (_particleUpdateRecorder.Valid)
                _particleUpdateRecorder.Dispose();
        }
    }
}
