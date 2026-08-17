using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HP.Utilities.RuntimeSceneInspector.Core
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
            var result = new List<RuntimeMemberDescriptor>(metadata.Members.Length);
            foreach (ReflectedMember member in metadata.Members)
            {
                if (member.IsProperty && !includeProperties)
                    continue;
                if (excludedDisplayNames != null && excludedDisplayNames.Contains(member.DisplayName))
                    continue;
                result.Add(member.Build(component, _valueDrawers));
            }
            return result;
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

            if (declaringType != null && typeof(MeshFilter).IsAssignableFrom(declaringType) &&
                propertyName == "mesh")
                return true;

            if (declaringType?.FullName == "UnityEngine.Rendering.Volume" && propertyName == "profile")
                return true;

            // ParticleSystem modules are already represented by the curated particle-system
            // drawer. Reflecting the module structs adds duplicate, non-actionable rows.
            return declaringType == typeof(ParticleSystem) &&
                   property.PropertyType.Name.EndsWith("Module", StringComparison.Ordinal);
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
                property.GetCustomAttribute<RuntimeRangeAttribute>(true),
                true);

            internal static ReflectedMember ForField(string id, string displayName,
                FieldInfo field, bool writable) => new(
                id,
                displayName,
                field.FieldType,
                component => field.GetValue(component),
                writable ? (component, value) => field.SetValue(component, value) : null,
                field.GetCustomAttribute<RuntimeRangeAttribute>(true),
                false);

            internal RuntimeMemberDescriptor Build(Component component,
                RuntimeValueDrawerRegistry valueDrawers)
            {
                try
                {
                    object value = _getter(component);
                    IRuntimeValueDrawer drawer = valueDrawers.Resolve(ValueType);
                    return new RuntimeMemberDescriptor
                    {
                        Name = Id,
                        DisplayName = DisplayName,
                        TypeName = ValueType.FullName,
                        Value = Format(value, ValueType, drawer),
                        ReadOnly = !CanSet || drawer == null
                    };
                }
                catch (Exception exception)
                {
                    Exception actual = Unwrap(exception);
                    return new RuntimeMemberDescriptor
                    {
                        Name = Id,
                        DisplayName = DisplayName,
                        TypeName = ValueType.FullName,
                        ReadOnly = true,
                        Error = actual.GetType().Name + ": " + actual.Message
                    };
                }
            }

            internal RuntimeCommandResult Set(Component component, string text,
                RuntimeValueDrawerRegistry valueDrawers)
            {
                if (!CanSet)
                    return RuntimeCommandResult.Fail("The member is read-only.");

                IRuntimeValueDrawer drawer = valueDrawers.Resolve(ValueType);
                if (drawer == null)
                    return RuntimeCommandResult.Fail("Unsupported value type.");
                if (!drawer.TryParse(text, ValueType, out object value, out string error))
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
                                                   Format(appliedValue, ValueType, drawer) +
                                                   " instead of the requested " +
                                                   Format(value, ValueType, drawer) + ".");
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

            private static string Format(object value, Type valueType, IRuntimeValueDrawer drawer)
            {
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

            private static Exception Unwrap(Exception exception)
            {
                while (exception is TargetInvocationException invocation && invocation.InnerException != null)
                    exception = invocation.InnerException;
                return exception;
            }
        }
    }
}
