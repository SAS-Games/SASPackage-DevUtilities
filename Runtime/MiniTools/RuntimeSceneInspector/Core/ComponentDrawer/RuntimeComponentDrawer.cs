using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    /// <summary>
    /// Optional extension implemented by component drawers that own writable synthetic members.
    /// Returning false means that the member ID is not owned by this drawer.
    /// </summary>
    internal interface IRuntimeEditableComponentDrawer : IRuntimeComponentDrawer
    {
        bool TrySetValue(Component component, string memberId, string text, out RuntimeCommandResult result);
    }

    /// <summary>Resolves explicit, side-effect-audited drawers for built-in Unity components.</summary>
    internal sealed class RuntimeComponentDrawerRegistry
    {
        private readonly List<IRuntimeComponentDrawer> _drawers = new();

        public RuntimeComponentDrawerRegistry(RuntimeValueDrawerRegistry valueDrawers)
        {
            if (valueDrawers == null) throw new ArgumentNullException(nameof(valueDrawers));
            _drawers.Add(new RuntimeRigidbodyComponentDrawer(valueDrawers));
            _drawers.Add(new RuntimeRigidbody2DComponentDrawer(valueDrawers));
            _drawers.Add(new RuntimeColliderComponentDrawer(valueDrawers));
            _drawers.Add(new RuntimeCollider2DComponentDrawer(valueDrawers));
            _drawers.Add(new RuntimeLightComponentDrawer(valueDrawers));
            _drawers.Add(new RuntimeAnimatorComponentDrawer(valueDrawers));
        }

        public IRuntimeComponentDrawer Resolve(Type componentType) => componentType == null
            ? null
            : _drawers.Find(drawer => drawer.CanDraw(componentType));
    }

    /// <summary>
    /// Delegate-backed base for curated component properties. It deliberately does not reflect
    /// arbitrary Unity properties: every getter and setter below has been explicitly selected.
    /// </summary>
    internal abstract class RuntimeComponentDrawer<TComponent> : IRuntimeEditableComponentDrawer where TComponent : Component
    {
        private readonly List<MemberDefinition> _members = new();
        private readonly RuntimeValueDrawerRegistry _valueDrawers;

        protected RuntimeComponentDrawer(RuntimeValueDrawerRegistry valueDrawers)
        {
            _valueDrawers = valueDrawers ?? throw new ArgumentNullException(nameof(valueDrawers));
        }

        public bool CanDraw(Type componentType) => componentType != null &&
                                                   typeof(TComponent).IsAssignableFrom(componentType);

        public IReadOnlyList<RuntimeMemberDescriptor> BuildInspector(Component component)
        {
            if (component == null || !(component is TComponent target))
                return Array.Empty<RuntimeMemberDescriptor>();

            var result = new List<RuntimeMemberDescriptor>(_members.Count);
            foreach (MemberDefinition member in _members)
            {
                if (!member.AppliesTo(target)) continue;
                result.Add(member.Build(target, _valueDrawers));
            }

            return result;
        }

        public bool TrySetValue(Component component, string memberId, string text,
            out RuntimeCommandResult result)
        {
            bool ownsId = false;
            if (string.IsNullOrEmpty(memberId))
            {
                result = null;
                return false;
            }

            foreach (MemberDefinition member in _members)
            {
                if (!string.Equals(member.Id, memberId, StringComparison.Ordinal)) continue;
                ownsId = true;
                if (component != null && component is TComponent target && member.AppliesTo(target))
                {
                    result = member.Set(target, text, _valueDrawers);
                    return true;
                }
            }

            result = ownsId
                ? RuntimeCommandResult.Fail(component == null
                    ? "The component no longer exists."
                    : "This member is unavailable on the component subtype.")
                : null;
            return ownsId;
        }

        protected void Add<TValue>(string id, string displayName, Func<TComponent, TValue> getter,
            Action<TComponent, TValue> setter = null, Func<TComponent, bool> applies = null,
            Func<TComponent, TValue, string> validator = null)
        {
            _members.Add(new MemberDefinition<TValue>(id, displayName, getter, setter, applies, validator));
        }

        protected void AddReadOnly<TValue>(string id, string displayName, Func<TComponent, TValue> getter,
            Func<TComponent, bool> applies = null)
        {
            Add(id, displayName, getter, null, applies);
        }

        protected static string RequirePositive(float value, string displayName) => value > 0f
            ? null
            : displayName + " must be greater than zero.";

        protected static string RequireNonNegative(float value, string displayName) => value >= 0f
            ? null
            : displayName + " cannot be negative.";

        protected static string RequireNonNegative(Vector2 value, string displayName) =>
            value.x >= 0f && value.y >= 0f ? null : displayName + " components cannot be negative.";

        protected static string RequireNonNegative(Vector3 value, string displayName) =>
            value.x >= 0f && value.y >= 0f && value.z >= 0f
                ? null
                : displayName + " components cannot be negative.";

        private static bool IsNamedEnumValue(string text, Type enumType)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string[] suppliedNames = text.Split(',');
            if (suppliedNames.Length > 1 && !enumType.IsDefined(typeof(FlagsAttribute), false))
                return false;
            string[] declaredNames = Enum.GetNames(enumType);
            foreach (string supplied in suppliedNames)
            {
                string candidate = supplied.Trim();
                bool found = false;
                foreach (string declared in declaredNames)
                {
                    if (!string.Equals(candidate, declared, StringComparison.OrdinalIgnoreCase)) continue;
                    found = true;
                    break;
                }

                if (!found) return false;
            }

            return true;
        }

        private static string ValidateFinite(object value)
        {
            if (value is float f && (float.IsNaN(f) || float.IsInfinity(f))) return "Value must be finite.";
            if (value is double d && (double.IsNaN(d) || double.IsInfinity(d))) return "Value must be finite.";
            if (value is Vector2 v2 && (!Finite(v2.x) || !Finite(v2.y))) return "Value must be finite.";
            if (value is Vector3 v3 && (!Finite(v3.x) || !Finite(v3.y) || !Finite(v3.z)))
                return "Value must be finite.";
            if (value is Vector4 v4 && (!Finite(v4.x) || !Finite(v4.y) || !Finite(v4.z) || !Finite(v4.w)))
                return "Value must be finite.";
            if (value is Quaternion q && (!Finite(q.x) || !Finite(q.y) || !Finite(q.z) || !Finite(q.w)))
                return "Value must be finite.";
            if (value is Color c && (!Finite(c.r) || !Finite(c.g) || !Finite(c.b) || !Finite(c.a)))
                return "Value must be finite.";
            if (value is Rect r && (!Finite(r.x) || !Finite(r.y) || !Finite(r.width) || !Finite(r.height)))
                return "Value must be finite.";
            if (value is Bounds b && (!Finite(b.center.x) || !Finite(b.center.y) || !Finite(b.center.z) ||
                                      !Finite(b.size.x) || !Finite(b.size.y) || !Finite(b.size.z)))
                return "Value must be finite.";
            return null;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private abstract class MemberDefinition
        {
            protected MemberDefinition(string id, string displayName, Type valueType)
            {
                if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("@unity.", StringComparison.Ordinal))
                    throw new ArgumentException("Built-in member IDs must start with '@unity.'.", nameof(id));
                Id = id;
                DisplayName = displayName;
                ValueType = valueType;
            }

            public string Id { get; }
            protected string DisplayName { get; }
            protected Type ValueType { get; }
            public abstract bool AppliesTo(TComponent component);
            public abstract RuntimeMemberDescriptor Build(TComponent component,
                RuntimeValueDrawerRegistry valueDrawers);
            public abstract RuntimeCommandResult Set(TComponent component, string text,
                RuntimeValueDrawerRegistry valueDrawers);
        }

        private sealed class MemberDefinition<TValue> : MemberDefinition
        {
            private readonly Func<TComponent, bool> _applies;
            private readonly Func<TComponent, TValue> _getter;
            private readonly Action<TComponent, TValue> _setter;
            private readonly Func<TComponent, TValue, string> _validator;

            public MemberDefinition(string id, string displayName, Func<TComponent, TValue> getter,
                Action<TComponent, TValue> setter, Func<TComponent, bool> applies,
                Func<TComponent, TValue, string> validator) : base(id, displayName, typeof(TValue))
            {
                _getter = getter ?? throw new ArgumentNullException(nameof(getter));
                _setter = setter;
                _applies = applies;
                _validator = validator;
            }

            public override bool AppliesTo(TComponent component) => _applies == null || _applies(component);

            public override RuntimeMemberDescriptor Build(TComponent component,
                RuntimeValueDrawerRegistry valueDrawers)
            {
                IRuntimeValueDrawer drawer = valueDrawers.Resolve(ValueType);
                try
                {
                    TValue value = _getter(component);
                    return new RuntimeMemberDescriptor
                    {
                        Name = Id,
                        DisplayName = DisplayName,
                        TypeName = ValueType.FullName,
                        Value = drawer?.Format(value, ValueType) ?? value?.ToString() ?? "null",
                        ReadOnly = _setter == null || drawer == null || typeof(Object).IsAssignableFrom(ValueType)
                    };
                }
                catch (Exception ex)
                {
                    return new RuntimeMemberDescriptor
                    {
                        Name = Id,
                        DisplayName = DisplayName,
                        TypeName = ValueType.FullName,
                        ReadOnly = true,
                        Error = ex.GetType().Name + ": " + ex.Message
                    };
                }
            }

            public override RuntimeCommandResult Set(TComponent component, string text,
                RuntimeValueDrawerRegistry valueDrawers)
            {
                if (_setter == null || typeof(Object).IsAssignableFrom(ValueType))
                    return RuntimeCommandResult.Fail("The member is read-only.");

                IRuntimeValueDrawer drawer = valueDrawers.Resolve(ValueType);
                if (drawer == null) return RuntimeCommandResult.Fail("Unsupported value type.");
                if (ValueType.IsEnum && !IsNamedEnumValue(text, ValueType))
                    return RuntimeCommandResult.Fail("Use a named " + ValueType.Name + " value.");
                if (!drawer.TryParse(text, ValueType, out object parsed, out string parseError))
                    return RuntimeCommandResult.Fail(parseError ?? "Invalid value.");
                if (!(parsed is TValue value)) return RuntimeCommandResult.Fail("Parsed value has the wrong type.");

                string finiteError = ValidateFinite(value);
                if (finiteError != null) return RuntimeCommandResult.Fail(finiteError);
                string validationError = _validator?.Invoke(component, value);
                if (!string.IsNullOrEmpty(validationError)) return RuntimeCommandResult.Fail(validationError);

                if (component == null) return RuntimeCommandResult.Fail("The component no longer exists.");

                bool hasPreviousValue = false;
                TValue previousValue = default;
                try
                {
                    previousValue = _getter(component);
                    hasPreviousValue = true;
                }
                catch
                {
                    // The setter can still be safe even if Unity cannot provide the previous value.
                }

                try
                {
                    _setter(component, value);
                }
                catch (Exception ex)
                {
                    return RuntimeCommandResult.Fail(ex.GetType().Name + ": " + ex.Message);
                }

                if (component == null)
                    return RuntimeCommandResult.Fail("The component was destroyed before the change could be verified.");

                try
                {
                    TValue appliedValue = _getter(component);
                    if (EqualityComparer<TValue>.Default.Equals(appliedValue, value))
                        return RuntimeCommandResult.Ok();

                    string requestedText = Format(value, drawer);
                    string appliedText = Format(appliedValue, drawer);
                    if (hasPreviousValue &&
                        EqualityComparer<TValue>.Default.Equals(appliedValue, previousValue))
                    {
                        return RuntimeCommandResult.Fail("Unity rejected the requested value " + requestedText +
                                                         "; the value remains " + appliedText + ".");
                    }

                    return RuntimeCommandResult.Ok("Unity applied " + appliedText +
                                                   " instead of the requested " + requestedText + ".");
                }
                catch (Exception ex)
                {
                    return RuntimeCommandResult.Fail("Unity accepted the setter call, but the applied value could " +
                                                     "not be verified (" + ex.GetType().Name + ": " + ex.Message +
                                                     ").");
                }
            }

            private static string Format(TValue value, IRuntimeValueDrawer drawer)
            {
                try
                {
                    return drawer.Format(value, typeof(TValue));
                }
                catch
                {
                    return value?.ToString() ?? "null";
                }
            }
        }
    }
}

