using UnityEngine;

namespace SAS.Utilities.RuntimeDebugger.Core
{
    internal sealed class RuntimeLightComponentDrawer : RuntimeComponentDrawer<Light>
    {
        public RuntimeLightComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.type", "Type", light => light.type, (light, value) => light.type = value);
            Add("@unity.color", "Color", light => light.color, (light, value) => light.color = value);
            Add("@unity.intensity", "Intensity", light => light.intensity,
                (light, value) => light.intensity = value,
                validator: (_, value) => RequireNonNegative(value, "Intensity"));
            Add("@unity.range", "Range", light => light.range, (light, value) => light.range = value,
                light => light.type == LightType.Point || light.type == LightType.Spot,
                (_, value) => RequirePositive(value, "Range"));
            Add("@unity.spotAngle", "Spot Angle", light => light.spotAngle,
                (light, value) => light.spotAngle = value,
                light => light.type == LightType.Spot,
                (_, value) => value > 0f && value <= 179f
                    ? null
                    : "Spot angle must be greater than 0 and at most 179 degrees.");
            Add("@unity.innerSpotAngle", "Inner Spot Angle", light => light.innerSpotAngle,
                (light, value) => light.innerSpotAngle = value,
                light => light.type == LightType.Spot,
                (light, value) => value >= 0f && value <= light.spotAngle
                    ? null
                    : "Inner spot angle must be between 0 and the outer spot angle.");
            Add("@unity.shadows", "Shadows", light => light.shadows, (light, value) => light.shadows = value);
            Add("@unity.shadowStrength", "Shadow Strength", light => light.shadowStrength,
                (light, value) => light.shadowStrength = value,
                validator: (_, value) => value >= 0f && value <= 1f
                    ? null
                    : "Shadow strength must be between 0 and 1.");
            Add("@unity.shadowBias", "Shadow Bias", light => light.shadowBias,
                (light, value) => light.shadowBias = value,
                validator: (_, value) => RequireNonNegative(value, "Shadow bias"));
            Add("@unity.shadowNormalBias", "Shadow Normal Bias", light => light.shadowNormalBias,
                (light, value) => light.shadowNormalBias = value,
                validator: (_, value) => RequireNonNegative(value, "Shadow normal bias"));
            Add("@unity.bounceIntensity", "Bounce Intensity", light => light.bounceIntensity,
                (light, value) => light.bounceIntensity = value,
                validator: (_, value) => RequireNonNegative(value, "Bounce intensity"));
            Add("@unity.cullingMask", "Culling Mask", light => light.cullingMask,
                (light, value) => light.cullingMask = value);
            Add("@unity.renderMode", "Render Mode", light => light.renderMode,
                (light, value) => light.renderMode = value);
            Add("@unity.useColorTemperature", "Use Color Temperature", light => light.useColorTemperature,
                (light, value) => light.useColorTemperature = value);
            Add("@unity.colorTemperature", "Color Temperature", light => light.colorTemperature,
                (light, value) => light.colorTemperature = value,
                validator: (_, value) => RequirePositive(value, "Color temperature"));
        }
    }
}

