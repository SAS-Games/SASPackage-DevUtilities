using UnityEngine;

namespace HP.Utilities.RuntimeSceneInspector.Core
{
    internal sealed class RuntimeParticleSystemComponentDrawer : RuntimeComponentDrawer<ParticleSystem>
    {
        public RuntimeParticleSystemComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.main.loop", "Main / Loop", system => system.main.loop, SetLoop);
            Add("@unity.main.prewarm", "Main / Prewarm", system => system.main.prewarm, SetPrewarm, system => system.main.loop);
            Add("@unity.main.playOnAwake", "Main / Play On Awake", system => system.main.playOnAwake, SetPlayOnAwake);
            Add("@unity.main.duration", "Main / Duration", system => system.main.duration, SetDuration, validator: (_, value) => RequirePositive(value, "Duration"));
            Add("@unity.main.simulationSpeed", "Main / Simulation Speed", system => system.main.simulationSpeed, SetSimulationSpeed, validator: (_, value) => RequireNonNegative(value, "Simulation speed"));
            Add("@unity.main.maxParticles", "Main / Maximum Particles", system => system.main.maxParticles, SetMaximumParticles, validator: (_, value) => value >= 0 ? null : "Maximum particles cannot be negative.");
            Add("@unity.main.simulationSpace", "Main / Simulation Space", system => system.main.simulationSpace, SetSimulationSpace);
            Add("@unity.main.scalingMode", "Main / Scaling Mode", system => system.main.scalingMode, SetScalingMode);
            Add("@unity.main.stopAction", "Main / Stop Action", system => system.main.stopAction, SetStopAction);
            Add("@unity.main.cullingMode", "Main / Culling Mode", system => system.main.cullingMode, SetCullingMode);
            Add("@unity.emission.enabled", "Emission / Enabled", system => system.emission.enabled, SetEmissionEnabled);
            Add("@unity.shape.enabled", "Shape / Enabled", system => system.shape.enabled, SetShapeEnabled);
            Add("@unity.shape.shapeType", "Shape / Type", system => system.shape.shapeType, SetShapeType, system => system.shape.enabled);
            Add("@unity.shape.radius", "Shape / Radius", system => system.shape.radius, SetShapeRadius, system => system.shape.enabled, (_, value) => RequireNonNegative(value, "Shape radius"));
            Add("@unity.shape.angle", "Shape / Angle", system => system.shape.angle, SetShapeAngle, system => system.shape.enabled, (_, value) => value >= 0f && value <= 90f ? null : "Shape angle must be between 0 and 90 degrees.");
            AddReadOnly("@unity.particleCount", "Particle Count", system => system.particleCount);
            AddReadOnly("@unity.isPlaying", "Is Playing", system => system.isPlaying);
            AddReadOnly("@unity.isPaused", "Is Paused", system => system.isPaused);
            AddReadOnly("@unity.isStopped", "Is Stopped", system => system.isStopped);
            AddReadOnly("@unity.time", "Time", system => system.time);
        }

        private static void SetLoop(ParticleSystem system, bool value) { ParticleSystem.MainModule module = system.main; module.loop = value; }
        private static void SetPrewarm(ParticleSystem system, bool value) { ParticleSystem.MainModule module = system.main; module.prewarm = value; }
        private static void SetPlayOnAwake(ParticleSystem system, bool value) { ParticleSystem.MainModule module = system.main; module.playOnAwake = value; }
        private static void SetDuration(ParticleSystem system, float value) { ParticleSystem.MainModule module = system.main; module.duration = value; }
        private static void SetSimulationSpeed(ParticleSystem system, float value) { ParticleSystem.MainModule module = system.main; module.simulationSpeed = value; }
        private static void SetMaximumParticles(ParticleSystem system, int value) { ParticleSystem.MainModule module = system.main; module.maxParticles = value; }
        private static void SetSimulationSpace(ParticleSystem system, ParticleSystemSimulationSpace value) { ParticleSystem.MainModule module = system.main; module.simulationSpace = value; }
        private static void SetScalingMode(ParticleSystem system, ParticleSystemScalingMode value) { ParticleSystem.MainModule module = system.main; module.scalingMode = value; }
        private static void SetStopAction(ParticleSystem system, ParticleSystemStopAction value) { ParticleSystem.MainModule module = system.main; module.stopAction = value; }
        private static void SetCullingMode(ParticleSystem system, ParticleSystemCullingMode value) { ParticleSystem.MainModule module = system.main; module.cullingMode = value; }
        private static void SetEmissionEnabled(ParticleSystem system, bool value) { ParticleSystem.EmissionModule module = system.emission; module.enabled = value; }
        private static void SetShapeEnabled(ParticleSystem system, bool value) { ParticleSystem.ShapeModule module = system.shape; module.enabled = value; }
        private static void SetShapeType(ParticleSystem system, ParticleSystemShapeType value) { ParticleSystem.ShapeModule module = system.shape; module.shapeType = value; }
        private static void SetShapeRadius(ParticleSystem system, float value) { ParticleSystem.ShapeModule module = system.shape; module.radius = value; }
        private static void SetShapeAngle(ParticleSystem system, float value) { ParticleSystem.ShapeModule module = system.shape; module.angle = value; }
    }
}
