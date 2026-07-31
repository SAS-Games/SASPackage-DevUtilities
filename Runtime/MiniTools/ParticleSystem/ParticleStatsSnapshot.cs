using System;
using Unity.Profiling;
using UnityEngine;

namespace SAS.DevUtilities
{
    /// <summary>
    /// Particle-system statistics shared by the Player overlay and Editor
    /// Debug Host.
    /// </summary>
    [Serializable]
    public struct ParticleStatsSnapshot : IMiniToolSnapshot
    {
        public int TotalSystems;
        public int ActiveSystems;
        public int AliveSystems;
        public int DisabledSystems;
        public int LiveParticles;
        public bool HasCpuTiming;
        public double CpuTimeMs;
    }

    /// <summary>
    /// Collects particle-system state without presentation or transport
    /// responsibilities.
    /// </summary>
    public static class ParticleStatsSnapshotCollector
    {
        public static ParticleStatsSnapshot Capture(in ProfilerRecorder particleUpdateRecorder)
        {
            ParticleSystem[] systems = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var snapshot = new ParticleStatsSnapshot
            {
                TotalSystems = systems.Length,
                HasCpuTiming = particleUpdateRecorder.Valid && particleUpdateRecorder.IsRunning
            };

            foreach (ParticleSystem system in systems)
            {
                if (system == null)
                    continue;

                bool active = system.gameObject.activeInHierarchy;
                if (active)
                {
                    snapshot.ActiveSystems++;
                    if (system.IsAlive(false))
                        snapshot.AliveSystems++;
                }
                else
                    snapshot.DisabledSystems++;

                snapshot.LiveParticles += system.particleCount;
            }

            if (snapshot.HasCpuTiming)
                snapshot.CpuTimeMs = particleUpdateRecorder.LastValue * 1e-6d;

            return snapshot;
        }
    }
}
