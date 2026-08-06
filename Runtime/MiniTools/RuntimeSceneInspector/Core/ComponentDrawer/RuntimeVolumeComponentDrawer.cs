using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    /// <summary>
    /// Exposes both the Volume behaviour and the parameters stored by its profile. Reading never
    /// instantiates a profile; the shared profile is cloned only when a profile value is edited.
    /// </summary>
    internal sealed class RuntimeVolumeComponentDrawer : IRuntimeEditableComponentDrawer
    {
        private const string ProfileMemberPrefix = "@volume.";
        private readonly RuntimeVolumeSettingsDrawer _settings;
        private readonly RuntimeValueDrawerRegistry _valueDrawers;

        public RuntimeVolumeComponentDrawer(RuntimeValueDrawerRegistry valueDrawers)
        {
            _valueDrawers = valueDrawers ?? throw new ArgumentNullException(nameof(valueDrawers));
            _settings = new RuntimeVolumeSettingsDrawer(valueDrawers);
        }

        public bool CanDraw(Type componentType) => componentType != null && typeof(Volume).IsAssignableFrom(componentType);

        public IReadOnlyList<RuntimeMemberDescriptor> BuildInspector(Component component)
        {
            if (!(component is Volume volume))
                return Array.Empty<RuntimeMemberDescriptor>();

            var members = new List<RuntimeMemberDescriptor>(_settings.BuildInspector(volume));
            VolumeProfile profile = GetCurrentProfile(volume);
            members.Add(BuildReadOnly("@volume.profile", "Profile", profile, typeof(VolumeProfile)));
            if (profile == null)
                return members;

            for (int componentIndex = 0; componentIndex < profile.components.Count; componentIndex++)
            {
                VolumeComponent profileComponent = profile.components[componentIndex];
                if (profileComponent == null)
                    continue;

                string componentName = Nicify(profileComponent.GetType().Name);
                members.Add(BuildValue(ComponentActiveId(componentIndex), componentName + " / Active", profileComponent.active, typeof(bool), true));
                foreach (VolumeParameterAccessor accessor in GetParameters(profileComponent))
                {
                    string parameterName = componentName + " / " + NicifyPath(accessor.Path);
                    members.Add(BuildValue(ParameterOverrideId(componentIndex, accessor.Path), parameterName + " / Override", accessor.Parameter.overrideState, typeof(bool), true));
                    members.Add(BuildParameterValue(componentIndex, parameterName, accessor));
                }
            }

            return members;
        }

        public bool TrySetValue(Component component, string memberId, string text, out RuntimeCommandResult result)
        {
            if (_settings.TrySetValue(component, memberId, text, out result))
                return true;
            if (string.IsNullOrEmpty(memberId) || !memberId.StartsWith(ProfileMemberPrefix, StringComparison.Ordinal))
            {
                result = null;
                return false;
            }
            if (!(component is Volume volume))
            {
                result = RuntimeCommandResult.Fail("The Volume component no longer exists.");
                return true;
            }

            VolumeProfile currentProfile = GetCurrentProfile(volume);
            if (currentProfile == null)
            {
                result = RuntimeCommandResult.Fail("The Volume has no profile.");
                return true;
            }

            for (int componentIndex = 0; componentIndex < currentProfile.components.Count; componentIndex++)
            {
                VolumeComponent currentComponent = currentProfile.components[componentIndex];
                if (currentComponent == null)
                    continue;
                if (string.Equals(memberId, ComponentActiveId(componentIndex), StringComparison.Ordinal))
                {
                    result = TrySetActive(volume, componentIndex, text);
                    return true;
                }

                foreach (VolumeParameterAccessor accessor in GetParameters(currentComponent))
                {
                    if (string.Equals(memberId, ParameterOverrideId(componentIndex, accessor.Path), StringComparison.Ordinal))
                    {
                        result = TrySetOverride(volume, componentIndex, accessor.Path, text);
                        return true;
                    }
                    if (string.Equals(memberId, ParameterValueId(componentIndex, accessor.Path), StringComparison.Ordinal))
                    {
                        result = TrySetParameterValue(volume, componentIndex, accessor.Path, accessor.ValueType, text);
                        return true;
                    }
                }
            }

            result = RuntimeCommandResult.Fail("The Volume profile member is no longer available.");
            return true;
        }

        private RuntimeMemberDescriptor BuildParameterValue(int componentIndex, string displayName, VolumeParameterAccessor accessor)
        {
            string id = ParameterValueId(componentIndex, accessor.Path);
            if (accessor.ValueProperty == null)
                return Error(id, displayName, accessor.Parameter.GetType(), "This Volume parameter does not expose a value property.");

            try
            {
                object value = accessor.ValueProperty.GetValue(accessor.Parameter);
                bool editable = accessor.ValueProperty.CanWrite && _valueDrawers.Resolve(accessor.ValueType) != null && !typeof(Object).IsAssignableFrom(accessor.ValueType);
                return BuildValue(id, displayName, value, accessor.ValueType, editable);
            }
            catch (Exception ex)
            {
                return Error(id, displayName, accessor.ValueType, ex.GetType().Name + ": " + ex.Message);
            }
        }

        private RuntimeMemberDescriptor BuildValue(string id, string displayName, object value, Type valueType, bool editable)
        {
            IRuntimeValueDrawer drawer = _valueDrawers.Resolve(valueType);
            try
            {
                return new RuntimeMemberDescriptor
                {
                    Name = id,
                    DisplayName = displayName,
                    TypeName = valueType.FullName,
                    Value = drawer?.Format(value, valueType) ?? value?.ToString() ?? "null",
                    ReadOnly = !editable || drawer == null || typeof(Object).IsAssignableFrom(valueType)
                };
            }
            catch (Exception ex)
            {
                return Error(id, displayName, valueType, ex.GetType().Name + ": " + ex.Message);
            }
        }

        private RuntimeMemberDescriptor BuildReadOnly(string id, string displayName, object value, Type valueType) => BuildValue(id, displayName, value, valueType, false);

        private static RuntimeMemberDescriptor Error(string id, string displayName, Type valueType, string message) => new()
        {
            Name = id,
            DisplayName = displayName,
            TypeName = valueType?.FullName ?? string.Empty,
            ReadOnly = true,
            Error = message
        };

        private RuntimeCommandResult TrySetActive(Volume volume, int componentIndex, string text)
        {
            if (!TryParse(text, typeof(bool), out object parsed, out RuntimeCommandResult error))
                return error;

            VolumeComponent profileComponent = GetEditableComponent(volume, componentIndex);
            if (profileComponent == null)
                return RuntimeCommandResult.Fail("The Volume profile component is no longer available.");
            profileComponent.active = (bool)parsed;
            return profileComponent.active == (bool)parsed ? RuntimeCommandResult.Ok() : RuntimeCommandResult.Fail("Unity rejected the requested active state.");
        }

        private RuntimeCommandResult TrySetOverride(Volume volume, int componentIndex, string path, string text)
        {
            if (!TryParse(text, typeof(bool), out object parsed, out RuntimeCommandResult error))
                return error;

            VolumeParameterAccessor accessor = GetEditableParameter(volume, componentIndex, path);
            if (accessor == null)
                return RuntimeCommandResult.Fail("The Volume parameter is no longer available.");
            accessor.Parameter.overrideState = (bool)parsed;
            return accessor.Parameter.overrideState == (bool)parsed ? RuntimeCommandResult.Ok() : RuntimeCommandResult.Fail("Unity rejected the requested override state.");
        }

        private RuntimeCommandResult TrySetParameterValue(Volume volume, int componentIndex, string path, Type expectedType, string text)
        {
            if (expectedType == null || typeof(Object).IsAssignableFrom(expectedType))
                return RuntimeCommandResult.Fail("The Volume parameter is read-only.");
            if (expectedType.IsEnum && !IsNamedEnumValue(text, expectedType))
                return RuntimeCommandResult.Fail("Use a named " + expectedType.Name + " value.");
            if (!TryParse(text, expectedType, out object parsed, out RuntimeCommandResult error))
                return error;
            if (!IsFinite(parsed))
                return RuntimeCommandResult.Fail("Value must be finite.");

            VolumeParameterAccessor accessor = GetEditableParameter(volume, componentIndex, path);
            if (accessor?.ValueProperty == null || !accessor.ValueProperty.CanWrite || accessor.ValueType != expectedType)
                return RuntimeCommandResult.Fail("The Volume parameter is no longer editable.");

            try
            {
                accessor.ValueProperty.SetValue(accessor.Parameter, parsed);
                object applied = accessor.ValueProperty.GetValue(accessor.Parameter);
                if (Equals(applied, parsed))
                    return RuntimeCommandResult.Ok();
                return RuntimeCommandResult.Ok("Unity applied " + Format(applied, expectedType) + " instead of the requested " + Format(parsed, expectedType) + ".");
            }
            catch (Exception ex)
            {
                return RuntimeCommandResult.Fail(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private bool TryParse(string text, Type valueType, out object parsed, out RuntimeCommandResult error)
        {
            IRuntimeValueDrawer drawer = _valueDrawers.Resolve(valueType);
            if (drawer == null)
            {
                parsed = null;
                error = RuntimeCommandResult.Fail("Unsupported value type.");
                return false;
            }
            if (!drawer.TryParse(text, valueType, out parsed, out string parseError))
            {
                error = RuntimeCommandResult.Fail(parseError ?? "Invalid value.");
                return false;
            }

            error = null;
            return true;
        }

        private string Format(object value, Type valueType)
        {
            try
            {
                return _valueDrawers.Resolve(valueType)?.Format(value, valueType) ?? value?.ToString() ?? "null";
            }
            catch
            {
                return value?.ToString() ?? "null";
            }
        }

        private static VolumeProfile GetCurrentProfile(Volume volume) => volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile;

        private static VolumeComponent GetEditableComponent(Volume volume, int componentIndex)
        {
            VolumeProfile profile = volume.profile;
            return componentIndex >= 0 && componentIndex < profile.components.Count ? profile.components[componentIndex] : null;
        }

        private static VolumeParameterAccessor GetEditableParameter(Volume volume, int componentIndex, string path)
        {
            VolumeComponent component = GetEditableComponent(volume, componentIndex);
            if (component == null)
                return null;
            foreach (VolumeParameterAccessor accessor in GetParameters(component))
            {
                if (string.Equals(accessor.Path, path, StringComparison.Ordinal))
                    return accessor;
            }

            return null;
        }

        private static IReadOnlyList<VolumeParameterAccessor> GetParameters(VolumeComponent component)
        {
            var result = new List<VolumeParameterAccessor>();
            var visited = new HashSet<object>(ReferenceComparer.Instance);
            CollectParameters(component, string.Empty, result, visited, 0);
            return result;
        }

        private static void CollectParameters(object owner, string prefix, List<VolumeParameterAccessor> result, HashSet<object> visited, int depth)
        {
            if (owner == null || depth > 6 || !visited.Add(owner))
                return;

            FieldInfo[] fields = owner.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Array.Sort(fields, (left, right) => left.MetadataToken.CompareTo(right.MetadataToken));
            foreach (FieldInfo field in fields)
            {
                if (field.IsStatic)
                    continue;

                object value;
                try
                {
                    value = field.GetValue(owner);
                }
                catch
                {
                    continue;
                }

                string path = string.IsNullOrEmpty(prefix) ? field.Name : prefix + "." + field.Name;
                if (value is VolumeParameter parameter)
                {
                    PropertyInfo valueProperty = parameter.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
                    result.Add(new VolumeParameterAccessor(path, parameter, valueProperty));
                    continue;
                }

                Type fieldType = field.FieldType;
                if (value != null && fieldType.IsClass && fieldType != typeof(string) && !fieldType.IsArray && !typeof(Object).IsAssignableFrom(fieldType))
                    CollectParameters(value, path, result, visited, depth + 1);
            }
        }

        private static string ComponentActiveId(int componentIndex) => ProfileMemberPrefix + componentIndex + ".active";

        private static string ParameterOverrideId(int componentIndex, string path) => ProfileMemberPrefix + componentIndex + "." + path + ".override";

        private static string ParameterValueId(int componentIndex, string path) => ProfileMemberPrefix + componentIndex + "." + path + ".value";

        private static string NicifyPath(string path)
        {
            string[] parts = path.Split('.');
            for (int index = 0; index < parts.Length; index++)
                parts[index] = Nicify(parts[index]);
            return string.Join(" / ", parts);
        }

        private static string Nicify(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            if (value.StartsWith("m_", StringComparison.Ordinal))
                value = value.Substring(2);

            var characters = new List<char>(value.Length + 8) { char.ToUpperInvariant(value[0]) };
            for (int index = 1; index < value.Length; index++)
            {
                char current = value[index];
                if (current == '_')
                {
                    characters.Add(' ');
                    continue;
                }
                if (char.IsUpper(current) && characters[characters.Count - 1] != ' ' && !char.IsUpper(value[index - 1]))
                    characters.Add(' ');
                characters.Add(current);
            }

            return new string(characters.ToArray());
        }

        private static bool IsNamedEnumValue(string text, Type enumType)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            string[] suppliedNames = text.Split(',');
            if (suppliedNames.Length > 1 && !enumType.IsDefined(typeof(FlagsAttribute), false))
                return false;
            string[] declaredNames = Enum.GetNames(enumType);
            foreach (string suppliedName in suppliedNames)
            {
                bool found = false;
                foreach (string declaredName in declaredNames)
                {
                    if (!string.Equals(suppliedName.Trim(), declaredName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    found = true;
                    break;
                }
                if (!found)
                    return false;
            }

            return true;
        }

        private static bool IsFinite(object value)
        {
            if (value is float f)
                return !float.IsNaN(f) && !float.IsInfinity(f);
            if (value is double d)
                return !double.IsNaN(d) && !double.IsInfinity(d);
            if (value is Vector2 v2)
                return Finite(v2.x) && Finite(v2.y);
            if (value is Vector3 v3)
                return Finite(v3.x) && Finite(v3.y) && Finite(v3.z);
            if (value is Vector4 v4)
                return Finite(v4.x) && Finite(v4.y) && Finite(v4.z) && Finite(v4.w);
            if (value is Color color)
                return Finite(color.r) && Finite(color.g) && Finite(color.b) && Finite(color.a);
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class RuntimeVolumeSettingsDrawer : RuntimeComponentDrawer<Volume>
        {
            public RuntimeVolumeSettingsDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
            {
                Add("@unity.isGlobal", "Is Global", volume => volume.isGlobal, (volume, value) => volume.isGlobal = value);
                Add("@unity.priority", "Priority", volume => volume.priority, (volume, value) => volume.priority = value);
                Add("@unity.blendDistance", "Blend Distance", volume => volume.blendDistance, (volume, value) => volume.blendDistance = value, volume => !volume.isGlobal, (_, value) => RequireNonNegative(value, "Blend distance"));
                Add("@unity.weight", "Weight", volume => volume.weight, (volume, value) => volume.weight = value, validator: (_, value) => value >= 0f && value <= 1f ? null : "Weight must be between 0 and 1.");
                AddReadOnly("@unity.sharedProfile", "Shared Profile", volume => volume.sharedProfile);
                AddReadOnly("@unity.hasRuntimeProfile", "Has Runtime Profile", volume => volume.HasInstantiatedProfile());
                AddReadOnly("@unity.colliderCount", "Collider Count", volume => volume.colliders.Count, volume => !volume.isGlobal);
            }
        }

        private sealed class VolumeParameterAccessor
        {
            public VolumeParameterAccessor(string path, VolumeParameter parameter, PropertyInfo valueProperty)
            {
                Path = path;
                Parameter = parameter;
                ValueProperty = valueProperty;
            }

            public string Path { get; }
            public VolumeParameter Parameter { get; }
            public PropertyInfo ValueProperty { get; }
            public Type ValueType => ValueProperty?.PropertyType;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new();
            public new bool Equals(object left, object right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
