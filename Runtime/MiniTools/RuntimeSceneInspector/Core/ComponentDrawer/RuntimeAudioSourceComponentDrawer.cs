using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    internal sealed class RuntimeAudioSourceComponentDrawer : RuntimeComponentDrawer<AudioSource>
    {
        public RuntimeAudioSourceComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            AddReadOnly("@unity.clip", "Clip", source => source.clip);
            Add("@unity.volume", "Volume", source => source.volume, (source, value) => source.volume = value, validator: (_, value) => InRange(value, 0f, 1f, "Volume"));
            Add("@unity.pitch", "Pitch", source => source.pitch, (source, value) => source.pitch = value, validator: (_, value) => InRange(value, -3f, 3f, "Pitch"));
            Add("@unity.mute", "Mute", source => source.mute, (source, value) => source.mute = value);
            Add("@unity.loop", "Loop", source => source.loop, (source, value) => source.loop = value);
            Add("@unity.playOnAwake", "Play On Awake", source => source.playOnAwake, (source, value) => source.playOnAwake = value);
            Add("@unity.spatialBlend", "Spatial Blend", source => source.spatialBlend, (source, value) => source.spatialBlend = value, validator: (_, value) => InRange(value, 0f, 1f, "Spatial blend"));
            Add("@unity.priority", "Priority", source => source.priority, (source, value) => source.priority = value, validator: (_, value) => value >= 0 && value <= 256 ? null : "Priority must be between 0 and 256.");
            Add("@unity.panStereo", "Stereo Pan", source => source.panStereo, (source, value) => source.panStereo = value, validator: (_, value) => InRange(value, -1f, 1f, "Stereo pan"));
            Add("@unity.dopplerLevel", "Doppler Level", source => source.dopplerLevel, (source, value) => source.dopplerLevel = value, validator: (_, value) => RequireNonNegative(value, "Doppler level"));
            Add("@unity.spread", "Spread", source => source.spread, (source, value) => source.spread = value, validator: (_, value) => InRange(value, 0f, 360f, "Spread"));
            Add("@unity.minDistance", "Minimum Distance", source => source.minDistance, (source, value) => source.minDistance = value, validator: (_, value) => RequireNonNegative(value, "Minimum distance"));
            Add("@unity.maxDistance", "Maximum Distance", source => source.maxDistance, (source, value) => source.maxDistance = value, validator: (source, value) => value >= source.minDistance ? null : "Maximum distance must be at least the minimum distance.");
            Add("@unity.rolloffMode", "Rolloff Mode", source => source.rolloffMode, (source, value) => source.rolloffMode = value);
            AddReadOnly("@unity.isPlaying", "Is Playing", source => source.isPlaying);
            AddReadOnly("@unity.timeSamples", "Time Samples", source => source.timeSamples);
        }

        private static string InRange(float value, float min, float max, string displayName) => value >= min && value <= max ? null : displayName + " must be between " + min + " and " + max + ".";
    }
}
