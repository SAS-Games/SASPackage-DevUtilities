using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    /// <summary>
    /// Cached reflection fallback for components that do not have a curated drawer. Public
    /// properties are inspectable automatically; non-public members must opt in explicitly.
    /// Unity-owned reflected members remain read-only because their setters are not guaranteed
    /// to be side-effect free. Curated component drawers continue to own audited Unity writes.
    /// </summary>
    internal sealed class RuntimeReflectedMemberProvider
    {
        private const string PropertyIdPrefix = "@property:";

        private readonly RuntimeValueDrawerRegistry _valueDrawers;
        private readonly Dictionary<Type, ReflectedTypeMetadata> _metadataCache = new();
        private readonly ConditionalWeakTable<Component, InspectionCache> _inspectionCache = new();

        internal RuntimeReflectedMemberProvider(RuntimeValueDrawerRegistry valueDrawers)
        {
            _valueDrawers = valueDrawers ?? throw new ArgumentNullException(nameof(valueDrawers));
        }

        internal IReadOnlyList<RuntimeMemberDescriptor> BuildInspector(Component component,
            ISet<string> excludedDisplayNames, bool includeProperties)
        {
            if (component == null)
                return Array.Empty<RuntimeMemberDescriptor>();

            ReflectedTypeMetadata metadata = Resolve(component.GetType());
            InspectionCache cache = _inspectionCache.GetValue(component,
                _ => new InspectionCache(metadata.Members.Length));
            bool changed = cache.Descriptors == null;
            for (int i = 0; i < metadata.Members.Length; i++)
            {
                ReflectedMember member = metadata.Members[i];
                bool included = !(member.IsProperty && !includeProperties) &&
                                (excludedDisplayNames == null || !excludedDisplayNames.Contains(member.DisplayName));
                if (cache.Included[i] != included)
                    changed = true;
                cache.Included[i] = included;
                if (!included)
                {
                    cache.HasValues[i] = false;
                    cache.Values[i] = null;
                    continue;
                }

                if (member.Capture(component, _valueDrawers, cache.Values[i], cache.HasValues[i],
                        out object capturedValue))
                    changed = true;
                cache.Values[i] = capturedValue;
                cache.HasValues[i] = true;
            }

            if (!changed)
                return cache.Descriptors;

            var descriptors = new List<RuntimeMemberDescriptor>(metadata.Members.Length);
            for (int i = 0; i < metadata.Members.Length; i++)
            {
                if (cache.Included[i])
                    descriptors.Add(metadata.Members[i].BuildCaptured(cache.Values[i], _valueDrawers));
            }

            cache.Descriptors = descriptors;
            return descriptors;
        }

        internal bool TrySetValue(Component component, string memberId, string text,
            out RuntimeCommandResult result)
        {
            result = null;
            if (component == null || string.IsNullOrEmpty(memberId))
                return false;

            ReflectedTypeMetadata metadata = Resolve(component.GetType());
            if (!metadata.MembersById.TryGetValue(memberId, out ReflectedMember member))
                return false;

            result = member.Set(component, text, _valueDrawers);
            return true;
        }

        private ReflectedTypeMetadata Resolve(Type componentType)
        {
            if (_metadataCache.TryGetValue(componentType, out ReflectedTypeMetadata metadata))
                return metadata;

            metadata = BuildMetadata(componentType);
            _metadataCache.Add(componentType, metadata);
            return metadata;
        }

        private static ReflectedTypeMetadata BuildMetadata(Type componentType)
        {
            var members = new List<ReflectedMember>();
            var memberIds = new HashSet<string>(StringComparer.Ordinal);
            var propertyNames = new HashSet<string>(StringComparer.Ordinal);

            for (Type current = componentType;
                 current != null && typeof(Component).IsAssignableFrom(current);
                 current = current.BaseType)
            {
                if (IsInfrastructureType(current))
                    continue;

                PropertyInfo[] properties = current.GetProperties(BindingFlags.Instance |
                                                                  BindingFlags.Public |
                                                                  BindingFlags.NonPublic |
                                                                  BindingFlags.DeclaredOnly);
                Array.Sort(properties, CompareMetadataTokens);
                foreach (PropertyInfo property in properties)
                {
                    if (!propertyNames.Add(property.Name) || !CanInspect(property))
                        continue;

                    string id = PropertyIdPrefix + property.DeclaringType.FullName + "." + property.Name;
                    if (!memberIds.Add(id))
                        continue;

                    bool explicitlyInspectable = property.IsDefined(typeof(RuntimeInspectableAttribute), true);
                    bool writable = CanWrite(property, explicitlyInspectable);
                    members.Add(ReflectedMember.ForProperty(id, Nicify(property.Name), property, writable));
                }
            }

            FieldInfo[] fields = componentType.GetFields(BindingFlags.Instance |
                                                         BindingFlags.Public |
                                                         BindingFlags.NonPublic);
            Array.Sort(fields, CompareMetadataTokens);
            foreach (FieldInfo field in fields)
            {
                if (!CanInspect(field) || !memberIds.Add(field.Name))
                    continue;

                bool writable = CanWrite(field);
                members.Add(ReflectedMember.ForField(field.Name, Nicify(field.Name), field, writable));
            }

            return new ReflectedTypeMetadata(members.ToArray());
        }

        private static bool CanInspect(PropertyInfo property)
        {
            if (property == null || property.GetIndexParameters().Length != 0 ||
                property.PropertyType == typeof(void) || property.PropertyType.IsByRef ||
                property.PropertyType.IsPointer ||
                property.IsDefined(typeof(ObsoleteAttribute), true) ||
                property.IsDefined(typeof(RuntimeHiddenAttribute), true))
                return false;

            MethodInfo getter = property.GetGetMethod(true);
            if (getter == null || getter.IsStatic || getter.ContainsGenericParameters)
                return false;

            bool explicitlyInspectable = property.IsDefined(typeof(RuntimeInspectableAttribute), true);
            if (!getter.IsPublic && !explicitlyInspectable)
                return false;

            return explicitlyInspectable || !IsKnownSideEffectGetter(property);
        }

        private static bool CanInspect(FieldInfo field) =>
            field != null && !field.IsStatic &&
            !field.IsDefined(typeof(RuntimeHiddenAttribute), true) &&
            (field.IsPublic || field.IsDefined(typeof(SerializeField), true) ||
             field.IsDefined(typeof(RuntimeInspectableAttribute), true));

        private static bool CanWrite(PropertyInfo property, bool explicitlyInspectable)
        {
            if (property.IsDefined(typeof(RuntimeReadOnlyAttribute), true) ||
                typeof(Object).IsAssignableFrom(property.PropertyType))
                return false;

            MethodInfo setter = property.GetSetMethod(true);
            if (setter == null || setter.IsStatic || setter.ContainsGenericParameters ||
                (!setter.IsPublic && !explicitlyInspectable))
                return false;

            return explicitlyInspectable || !IsUnityOwned(property.DeclaringType);
        }

        private static bool CanWrite(FieldInfo field) =>
            !field.IsInitOnly && !field.IsLiteral &&
            !field.IsDefined(typeof(RuntimeReadOnlyAttribute), true) &&
            !typeof(Object).IsAssignableFrom(field.FieldType) &&
            (field.IsDefined(typeof(RuntimeInspectableAttribute), true) ||
             !IsUnityOwned(field.DeclaringType));

        private static bool IsInfrastructureType(Type type) =>
            type == typeof(Object) || type == typeof(Component) || type == typeof(Behaviour) ||
            type == typeof(MonoBehaviour);

        private static bool IsUnityOwned(Type type)
        {
            if (type == null)
                return false;

            string typeNamespace = type.Namespace ?? string.Empty;
            string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
            return typeNamespace == "UnityEngine" ||
                   typeNamespace.StartsWith("UnityEngine.", StringComparison.Ordinal) ||
                   assemblyName == "UnityEngine" ||
                   assemblyName.StartsWith("UnityEngine.", StringComparison.Ordinal) ||
                   assemblyName.StartsWith("Unity.", StringComparison.Ordinal);
        }

        private static bool IsKnownSideEffectGetter(PropertyInfo property)
        {
            string propertyName = property.Name;
            Type declaringType = property.DeclaringType;

            if (declaringType != null && typeof(Renderer).IsAssignableFrom(declaringType) &&
                (propertyName == "material" || propertyName == "materials" ||
                 propertyName == "sharedMaterials"))
                return true;

            if (declaringType != null && typeof(Collider).IsAssignableFrom(declaringType) &&
                propertyName == "material")
                return true;

            if (declaringType != null && typeof(MeshFilter).IsAssignableFrom(declaringType) &&
                propertyName == "mesh")
                return true;

            if (declaringType?.FullName == "UnityEngine.Rendering.Volume" && propertyName == "profile")
                return true;

            if (InheritsFrom(declaringType, "UnityEngine.UI.Graphic") &&
                propertyName == "materialForRendering")
                return true;

            if (InheritsFrom(declaringType, "TMPro.TMP_Text") &&
                (propertyName == "fontMaterial" || propertyName == "fontMaterials"))
                return true;

            // ParticleSystem modules are already represented by the curated particle-system
            // drawer. Reflecting the module structs adds duplicate, non-actionable rows.
            return declaringType == typeof(ParticleSystem) &&
                   property.PropertyType.Name.EndsWith("Module", StringComparison.Ordinal);
        }

        private static bool InheritsFrom(Type type, string fullName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (string.Equals(current.FullName, fullName, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static int CompareMetadataTokens(MemberInfo left, MemberInfo right) =>
            left.MetadataToken.CompareTo(right.MetadataToken);

        private static string Nicify(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value.StartsWith("m_", StringComparison.Ordinal))
                value = value.Substring(2);
            else
                value = value.TrimStart('_');

            var characters = new List<char>(value.Length + 8);
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (current == '_')
                {
                    if (characters.Count > 0 && characters[characters.Count - 1] != ' ')
                        characters.Add(' ');
                    continue;
                }

                if (characters.Count > 0 && char.IsUpper(current) &&
                    characters[characters.Count - 1] != ' ' &&
                    !char.IsUpper(value[index - 1]))
                    characters.Add(' ');
                characters.Add(characters.Count == 0 ? char.ToUpperInvariant(current) : current);
            }

            return new string(characters.ToArray());
        }

        private sealed class InspectionCache
        {
            internal InspectionCache(int count)
            {
                Included = new bool[count];
                HasValues = new bool[count];
                Values = new object[count];
            }

            internal bool[] Included { get; }
            internal bool[] HasValues { get; }
            internal object[] Values { get; }
            internal IReadOnlyList<RuntimeMemberDescriptor> Descriptors { get; set; }
        }

        private sealed class CapturedUnityObject
        {
            internal CapturedUnityObject(Object value, int instanceId, string name)
            {
                Value = value;
                InstanceId = instanceId;
                Name = name;
            }

            internal Object Value { get; }
            internal int InstanceId { get; }
            internal string Name { get; }
        }

        private sealed class CapturedValue
        {
            internal CapturedValue(string value) => Value = value;
            internal string Value { get; }

            public override bool Equals(object obj) =>
                obj is CapturedValue other && string.Equals(Value, other.Value, StringComparison.Ordinal);

            public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        }

        private sealed class CapturedError
        {
            internal CapturedError(string value) => Value = value;
            internal string Value { get; }
        }

        private sealed class ReflectedTypeMetadata
        {
            internal ReflectedTypeMetadata(ReflectedMember[] members)
            {
                Members = members;
                MembersById = members.ToDictionary(member => member.Id, StringComparer.Ordinal);
            }

            internal ReflectedMember[] Members { get; }
            internal Dictionary<string, ReflectedMember> MembersById { get; }
        }

        private sealed class ReflectedMember
        {
            private readonly Func<Component, object> _getter;
            private readonly Action<Component, object> _setter;
            private readonly RuntimeRangeAttribute _range;

            private ReflectedMember(string id, string displayName, Type valueType,
                Func<Component, object> getter, Action<Component, object> setter,
                RuntimeRangeAttribute range, bool isProperty)
            {
                Id = id;
                DisplayName = displayName;
                ValueType = valueType;
                _getter = getter;
                _setter = setter;
                _range = range;
                IsProperty = isProperty;
            }

            internal string Id { get; }
            internal string DisplayName { get; }
            internal bool IsProperty { get; }
            private Type ValueType { get; }
            private bool CanSet => _setter != null;

            internal static ReflectedMember ForProperty(string id, string displayName,
                PropertyInfo property, bool writable) => new(
                id,
                displayName,
                property.PropertyType,
                component => property.GetValue(component),
                writable ? (component, value) => property.SetValue(component, value) : null,
                GetRange(property),
                true);

            internal static ReflectedMember ForField(string id, string displayName,
                FieldInfo field, bool writable) => new(
                id,
                displayName,
                field.FieldType,
                component => field.GetValue(component),
                writable ? (component, value) => field.SetValue(component, value) : null,
                GetRange(field),
                false);

            private static RuntimeRangeAttribute GetRange(MemberInfo member)
            {
                RuntimeRangeAttribute runtimeRange =
                    member.GetCustomAttribute<RuntimeRangeAttribute>(true);
                if (runtimeRange != null)
                    return runtimeRange;

                RangeAttribute unityRange = member.GetCustomAttribute<RangeAttribute>(true);
                return unityRange == null
                    ? null
                    : new RuntimeRangeAttribute(unityRange.min, unityRange.max);
            }

            internal bool Capture(Component component, RuntimeValueDrawerRegistry valueDrawers,
                object previousValue, bool hasPreviousValue, out object capturedValue)
            {
                try
                {
                    object value = _getter(component);
                    IRuntimeValueDrawer drawer = valueDrawers.Resolve(ValueType);
                    if (typeof(Object).IsAssignableFrom(ValueType))
                    {
                        var unityObject = value as Object;
                        int instanceId = unityObject == null ? 0 : unityObject.GetInstanceID();
                        string name = unityObject == null ? null : unityObject.name;
                        if (hasPreviousValue && previousValue is CapturedUnityObject previousObject &&
                            previousObject.InstanceId == instanceId &&
                            string.Equals(previousObject.Name, name, StringComparison.Ordinal))
                        {
                            capturedValue = previousValue;
                            return false;
                        }

                        capturedValue = new CapturedUnityObject(unityObject, instanceId, name);
                        return true;
                    }

                    if (drawer != null)
                    {
                        if (hasPreviousValue && !(previousValue is CapturedValue) && Equals(previousValue, value))
                        {
                            capturedValue = previousValue;
                            return false;
                        }

                        capturedValue = value;
                        return true;
                    }

                    CapturedValue formatted = CaptureFallbackValue(value, ValueType);
                    if (hasPreviousValue && previousValue is CapturedValue previousFormatted &&
                        previousFormatted.Equals(formatted))
                    {
                        capturedValue = previousValue;
                        return false;
                    }

                    capturedValue = formatted;
                    return true;
                }
                catch (Exception exception)
                {
                    Exception actual = Unwrap(exception);
                    string error = actual.GetType().Name + ": " + actual.Message;
                    if (hasPreviousValue && previousValue is CapturedError previousError &&
                        string.Equals(previousError.Value, error, StringComparison.Ordinal))
                    {
                        capturedValue = previousValue;
                        return false;
                    }

                    capturedValue = new CapturedError(error);
                    return true;
                }
            }

            internal RuntimeMemberDescriptor BuildCaptured(object capturedValue,
                RuntimeValueDrawerRegistry valueDrawers)
            {
                var descriptor = new RuntimeMemberDescriptor
                {
                    Name = Id,
                    DisplayName = DisplayName,
                    TypeName = ValueType.FullName
                };

                if (capturedValue is CapturedError error)
                {
                    descriptor.ReadOnly = true;
                    descriptor.Error = error.Value;
                }
                else
                {
                    IRuntimeValueDrawer drawer = valueDrawers.Resolve(ValueType);
                    object value = capturedValue is CapturedUnityObject unityObject
                        ? unityObject.Value
                        : capturedValue is CapturedValue formatted
                            ? formatted.Value
                            : capturedValue;
                    descriptor.Value = capturedValue is CapturedValue
                        ? (string)value
                        : Format(value, ValueType, drawer, DisplayName, Id);
                    descriptor.ReadOnly = !CanSet || drawer == null;
                }

                RuntimeInspectorControlMetadata.Populate(descriptor, ValueType, DisplayName, Id,
                    range: _range);
                return descriptor;
            }

            private static CapturedValue CaptureFallbackValue(object value, Type valueType)
            {
                if (value == null)
                    return new CapturedValue("null");
                if (value is Array array)
                    return new CapturedValue(valueType.Name + " (Length = " + array.Length + ")");
                if (value is ICollection collection)
                    return new CapturedValue(valueType.Name + " (Count = " + collection.Count + ")");
                return new CapturedValue(value.ToString());
            }

            internal RuntimeCommandResult Set(Component component, string text,
                RuntimeValueDrawerRegistry valueDrawers)
            {
                if (!CanSet)
                    return RuntimeCommandResult.Fail("The member is read-only.");

                IRuntimeValueDrawer drawer = valueDrawers.Resolve(ValueType);
                if (drawer == null)
                    return RuntimeCommandResult.Fail("Unsupported value type.");

                if (!TryParseNamedUnityValue(text, ValueType, Id, DisplayName, out object value, out string error) &&
                    !drawer.TryParse(text, ValueType, out value, out error))
                    return RuntimeCommandResult.Fail(error ?? "Invalid value.");

                if (!ValidateRange(value, out error))
                    return RuntimeCommandResult.Fail(error);

                try
                {
                    _setter(component, value);
                    if (component == null)
                        return RuntimeCommandResult.Fail("The component was destroyed before the change could be verified.");

                    object appliedValue = _getter(component);
                    if (Equals(appliedValue, value))
                        return RuntimeCommandResult.Ok();

                    return RuntimeCommandResult.Ok("Unity applied " +
                                                   Format(appliedValue, ValueType, drawer, DisplayName, Id) +
                                                   " instead of the requested " +
                                                   Format(value, ValueType, drawer, DisplayName, Id) + ".");
                }
                catch (Exception exception)
                {
                    Exception actual = Unwrap(exception);
                    return RuntimeCommandResult.Fail(actual.GetType().Name + ": " + actual.Message);
                }
            }

            private bool ValidateRange(object value, out string error)
            {
                if (_range == null || !(value is IConvertible convertible))
                {
                    error = null;
                    return true;
                }

                try
                {
                    double number = convertible.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
                    if (number >= _range.Min && number <= _range.Max)
                    {
                        error = null;
                        return true;
                    }

                    error = DisplayName + " must be between " + _range.Min + " and " + _range.Max + ".";
                    return false;
                }
                catch
                {
                    error = null;
                    return true;
                }
            }

            private static string Format(object value, Type valueType, IRuntimeValueDrawer drawer,
                string displayName, string memberId)
            {
                if (TryFormatUnityNamedValue(value, valueType, displayName, memberId, out string namedValue))
                    return namedValue;
                if (drawer != null)
                    return drawer.Format(value, valueType);
                if (value == null)
                    return "null";
                if (value is Array array)
                    return valueType.Name + " (Length = " + array.Length + ")";
                if (value is ICollection collection)
                    return valueType.Name + " (Count = " + collection.Count + ")";
                return value.ToString();
            }

            private static bool TryFormatUnityNamedValue(object value, Type valueType, string displayName,
                string memberId, out string formatted)
            {
                formatted = null;
                if (value is null || valueType != typeof(int))
                    return false;

                int numericValue = System.Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
                if (RuntimeInspectorControlMetadata.IsLayerIdentifier(displayName, memberId))
                {
                    string layerName = LayerMask.LayerToName(numericValue);
                    formatted = string.IsNullOrEmpty(layerName) ? ("Layer " + numericValue) : layerName;
                    return true;
                }

                if (RuntimeInspectorControlMetadata.IsSortingLayerIdentifier(displayName, memberId))
                {
                    string layerName = SortingLayer.IDToName(numericValue);
                    formatted = string.IsNullOrEmpty(layerName) ? (numericValue.ToString(System.Globalization.CultureInfo.InvariantCulture)) : layerName;
                    return true;
                }

                return false;
            }

            private static bool TryParseNamedUnityValue(string text, Type valueType, string memberId,
                string displayName, out object value, out string error)
            {
                value = null;
                error = null;
                if (valueType != typeof(int) || string.IsNullOrWhiteSpace(text))
                    return false;

                if (RuntimeInspectorControlMetadata.IsLayerIdentifier(displayName, memberId))
                {
                    if (int.TryParse(text, System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture, out int numeric))
                    {
                        value = numeric;
                        return true;
                    }

                    int resolved = LayerMask.NameToLayer(text);
                    if (resolved >= 0)
                    {
                        value = resolved;
                        return true;
                    }

                    error = "The layer name is not valid.";
                    return false;
                }

                if (RuntimeInspectorControlMetadata.IsSortingLayerIdentifier(displayName, memberId))
                {
                    if (int.TryParse(text, System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture, out int numeric))
                    {
                        value = numeric;
                        return true;
                    }

                    int resolved = SortingLayer.NameToID(text);
                    if (resolved >= 0)
                    {
                        value = resolved;
                        return true;
                    }

                    error = "The sorting layer name is not valid.";
                    return false;
                }

                return false;
            }

            private static Exception Unwrap(Exception exception)
            {
                while (exception is TargetInvocationException invocation && invocation.InnerException != null)
                    exception = invocation.InnerException;
                return exception;
            }
        }
    }
}
