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
                Field("total", "Particle Systems", systems.Length),
                Field("active", "Active Systems", active),
                Field("alive", "Alive Systems", alive),
                Field("particles", "Live Particles", particles)
            };
        }

        private static RemoteMiniToolField Field(string name, string displayName, int value) => new()
        {
            Name = name,
            DisplayName = displayName,
            Value = value.ToString()
        };
    }
}
