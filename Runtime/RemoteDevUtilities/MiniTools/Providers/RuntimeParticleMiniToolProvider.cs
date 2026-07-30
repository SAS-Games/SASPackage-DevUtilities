using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimeParticleMiniToolProvider :
        MiniToolFieldDataProvider
    {
        public override RemoteMiniToolField[] CaptureFields()
        {
            ParticleSystem[] systems = Object.FindObjectsByType<ParticleSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int active = 0;
            int alive = 0;
            int particles = 0;

            foreach (ParticleSystem system in systems)
            {
                if (system == null)
                    continue;
                if (system.gameObject.activeInHierarchy)
                    active++;
                if (system.IsAlive(true))
                    alive++;
                particles += system.particleCount;
            }

            return new[]
            {
                CreateField(
                    "total",
                    "Particle Systems",
                    systems.Length.ToString()),
                CreateField(
                    "active",
                    "Active Systems",
                    active.ToString()),
                CreateField(
                    "alive",
                    "Alive Systems",
                    alive.ToString()),
                CreateField(
                    "particles",
                    "Live Particles",
                    particles.ToString())
            };
        }
    }
}
