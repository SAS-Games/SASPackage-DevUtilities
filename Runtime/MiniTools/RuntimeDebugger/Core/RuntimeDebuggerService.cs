using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SAS.Utilities.RuntimeDebugger.Core
{
    public sealed class RuntimeDebuggerService : IRuntimeDebugger, IDisposable
    {
        private static readonly ProfilerMarker HierarchyMarker = new("RuntimeDebugger.Hierarchy.Reconcile");
        private static readonly ProfilerMarker InspectorMarker = new("RuntimeDebugger.Inspector.Build");
        private readonly RuntimeDebuggerSettings _settings;
        private readonly RuntimeObjectRegistry _registry = new();
        private readonly RuntimeValueDrawerRegistry _drawers = new();
        private readonly Dictionary<Type, FieldInfo[]> _fieldCache = new();
        private readonly Dictionary<string, RuntimeObjectId> _sceneIds = new();
        private readonly int _mainThreadId;
        private RuntimeHierarchySnapshot _snapshot = new() { Entries = Array.Empty<RuntimeHierarchyEntry>() };
        private long _nextSceneId = 1L << 60;

        public RuntimeDebuggerService(RuntimeDebuggerSettings settings)
        {
            _settings = settings;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            SceneManager.sceneLoaded += OnSceneChanged;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            RefreshHierarchy();
        }

        public RuntimeHierarchySnapshot GetHierarchySnapshot()
        {
            EnsureMainThread();
            return _snapshot;
        }

        public void RefreshHierarchy()
        {
            EnsureMainThread();
            using (HierarchyMarker.Auto())
            {
                _registry.BeginReconciliation();
                var entries = new List<RuntimeHierarchyEntry>();
                var normalHandles = new HashSet<int>();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.IsValid() || !scene.isLoaded) continue;
                    normalHandles.Add(scene.handle);
                    RuntimeObjectId sceneId = GetSceneId(scene, false);
                    entries.Add(new RuntimeHierarchyEntry
                    {
                        Id = sceneId, Kind = RuntimeHierarchyKind.Scene, Name = scene.name, ActiveSelf = true,
                        ActiveInHierarchy = true
                    });
                    foreach (GameObject root in scene.GetRootGameObjects()) AddObject(entries, root, sceneId, sceneId);
                }

                RuntimeObjectId persistentId = default;
                foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (transform == null || transform.parent != null) continue;
                    GameObject gameObject = transform.gameObject;
                    Scene scene = gameObject.scene;
                    if (!scene.IsValid() || !scene.isLoaded || normalHandles.Contains(scene.handle)) continue;
                    if (!persistentId.IsValid)
                    {
                        persistentId = GetSceneId(scene, true);
                        entries.Add(new RuntimeHierarchyEntry
                        {
                            Id = persistentId, Kind = RuntimeHierarchyKind.Scene, Name = "Persistent Objects",
                            ActiveSelf = true, ActiveInHierarchy = true
                        });
                    }

                    AddObject(entries, gameObject, persistentId, persistentId);
                }

                _registry.EndReconciliation();
                _snapshot = new RuntimeHierarchySnapshot { Revision = _snapshot.Revision + 1, Entries = entries };
            }
        }

        public RuntimeObjectDetails InspectObject(RuntimeObjectId objectId)
        {
            EnsureMainThread();
            if (!_registry.TryResolve(objectId, out GameObject gameObject)) return null;
            using (InspectorMarker.Auto())
            {
                var components = new List<RuntimeComponentDescriptor>();
                foreach (Component component in gameObject.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        components.Add(new RuntimeComponentDescriptor
                        {
                            TypeName = "Missing Script", Missing = true,
                            Members = Array.Empty<RuntimeMemberDescriptor>()
                        });
                        continue;
                    }

                    Type type = component.GetType();
                    if (IsBlocked(type))
                    {
                        components.Add(new RuntimeComponentDescriptor
                        {
                            Id = _registry.GetOrCreate(component), TypeName = type.FullName,
                            Members = Array.Empty<RuntimeMemberDescriptor>()
                        });
                        continue;
                    }

                    bool hasEnabled = TryGetEnabled(component, out bool enabled);
                    components.Add(new RuntimeComponentDescriptor
                    {
                        Id = _registry.GetOrCreate(component), TypeName = type.FullName, HasEnabledState = hasEnabled,
                        Enabled = enabled, Members = BuildMembers(component)
                    });
                }

                string tag;
                try
                {
                    tag = gameObject.tag;
                }
                catch
                {
                    tag = "<unavailable>";
                }

                return new RuntimeObjectDetails
                {
                    Id = objectId, Name = gameObject.name, Active = gameObject.activeSelf, Tag = tag,
                    Layer = gameObject.layer, Components = components
                };
            }
        }

        public RuntimeCommandResult Execute(RuntimeDebuggerCommand command)
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                return RuntimeCommandResult.Fail("Commands must execute on Unity's main thread.");
            try
            {
                if (command is SetGameObjectActiveCommand active)
                {
                    if (!_settings.AllowActivationChanges)
                        return RuntimeCommandResult.Fail("GameObject activation changes are disabled.");
                    if (!_registry.TryResolve(active.ObjectId, out GameObject target))
                        return RuntimeCommandResult.Fail("The GameObject no longer exists.");
                    target.SetActive(active.Active);
                    return RuntimeCommandResult.Ok();
                }

                if (command is SetComponentEnabledCommand componentEnabled)
                {
                    if (!_settings.AllowComponentEnableChanges)
                        return RuntimeCommandResult.Fail("Component enable changes are disabled.");
                    if (!_registry.TryResolve(componentEnabled.ComponentId, out Component component))
                        return RuntimeCommandResult.Fail("The component no longer exists.");
                    if (IsBlocked(component.GetType()))
                        return RuntimeCommandResult.Fail("The component type is blocked.");
                    return TrySetEnabled(component, componentEnabled.Enabled)
                        ? RuntimeCommandResult.Ok()
                        : RuntimeCommandResult.Fail("This component has no supported enabled state.");
                }

                if (command is SetMemberValueCommand setValue)
                {
                    if (!_settings.AllowValueChanges) return RuntimeCommandResult.Fail("Value changes are disabled.");
                    if (!_registry.TryResolve(setValue.ComponentId, out Component component))
                        return RuntimeCommandResult.Fail("The component no longer exists.");
                    if (IsBlocked(component.GetType()))
                        return RuntimeCommandResult.Fail("The component type is blocked.");
                    return SetMember(component, setValue.MemberName, setValue.Value);
                }

                return RuntimeCommandResult.Fail("Unsupported debugger command.");
            }
            catch (Exception ex)
            {
                return RuntimeCommandResult.Fail(ex.GetType().Name + ": " + ex.Message);
            }
        }

        public void Dispose()
        {
            SceneManager.sceneLoaded -= OnSceneChanged;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void AddObject(List<RuntimeHierarchyEntry> entries, GameObject gameObject, RuntimeObjectId sceneId,
            RuntimeObjectId parentId)
        {
            if (gameObject == null || (!_settings.IncludeInactiveObjects && !gameObject.activeInHierarchy)) return;
            RuntimeObjectId id = _registry.GetOrCreate(gameObject);
            Component[] components = gameObject.GetComponents<Component>();
            var names = new string[components.Length];
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                names[i] = component == null ? "Missing Script" : component.GetType().Name;
                if (component != null) _registry.GetOrCreate(component);
            }

            entries.Add(new RuntimeHierarchyEntry
            {
                Id = id, ParentId = parentId, SceneId = sceneId, Kind = RuntimeHierarchyKind.GameObject,
                Name = gameObject.name, ActiveSelf = gameObject.activeSelf,
                ActiveInHierarchy = gameObject.activeInHierarchy, ComponentTypeNames = names
            });
            Transform transform = gameObject.transform;
            for (int i = 0; i < transform.childCount; i++)
                AddObject(entries, transform.GetChild(i).gameObject, sceneId, id);
        }

        private IReadOnlyList<RuntimeMemberDescriptor> BuildMembers(Component component)
        {
            var result = new List<RuntimeMemberDescriptor>();
            if (component is Transform transform)
            {
                AddSynthetic(result, "localPosition", typeof(Vector3), transform.localPosition);
                AddSynthetic(result, "localEulerAngles", typeof(Vector3), transform.localEulerAngles);
                AddSynthetic(result, "localScale", typeof(Vector3), transform.localScale);
            }

            foreach (FieldInfo field in GetInspectableFields(component.GetType()))
            {
                IRuntimeValueDrawer drawer = _drawers.Resolve(field.FieldType);
                bool readOnly = drawer == null || field.IsInitOnly ||
                                field.IsDefined(typeof(RuntimeReadOnlyAttribute), true) ||
                                typeof(Object).IsAssignableFrom(field.FieldType);
                try
                {
                    object value = field.GetValue(component);
                    result.Add(new RuntimeMemberDescriptor
                    {
                        Name = field.Name, DisplayName = Nicify(field.Name), TypeName = field.FieldType.FullName,
                        Value = drawer?.Format(value, field.FieldType) ?? value?.ToString() ?? "null",
                        ReadOnly = readOnly
                    });
                }
                catch (Exception ex)
                {
                    result.Add(new RuntimeMemberDescriptor
                    {
                        Name = field.Name, DisplayName = field.Name, TypeName = field.FieldType.FullName,
                        ReadOnly = true, Error = ex.Message
                    });
                }
            }

            return result;
        }

        private void AddSynthetic(List<RuntimeMemberDescriptor> list, string name, Type type, object value) => list.Add(
            new RuntimeMemberDescriptor
            {
                Name = name, DisplayName = Nicify(name), TypeName = type.FullName,
                Value = _drawers.Resolve(type).Format(value, type)
            });

        private RuntimeCommandResult SetMember(Component component, string name, string text)
        {
            if (component is Transform transform)
            {
                if (!_drawers.Resolve(typeof(Vector3))
                        .TryParse(text, typeof(Vector3), out object parsed, out string parseError))
                    return RuntimeCommandResult.Fail(parseError);
                if (name == "localPosition") transform.localPosition = (Vector3)parsed;
                else if (name == "localEulerAngles") transform.localEulerAngles = (Vector3)parsed;
                else if (name == "localScale") transform.localScale = (Vector3)parsed;
                else return RuntimeCommandResult.Fail("Unknown Transform member.");
                return RuntimeCommandResult.Ok();
            }

            FieldInfo field = GetInspectableFields(component.GetType()).FirstOrDefault(item => item.Name == name);
            if (field == null || field.IsInitOnly || field.IsDefined(typeof(RuntimeReadOnlyAttribute), true))
                return RuntimeCommandResult.Fail("The member is not writable.");
            IRuntimeValueDrawer drawer = _drawers.Resolve(field.FieldType);
            if (drawer == null) return RuntimeCommandResult.Fail("Unsupported value type.");
            if (!drawer.TryParse(text, field.FieldType, out object value, out string error))
                return RuntimeCommandResult.Fail(error ?? "Invalid value.");
            field.SetValue(component, value);
            return RuntimeCommandResult.Ok();
        }

        private FieldInfo[] GetInspectableFields(Type type)
        {
            if (_fieldCache.TryGetValue(type, out FieldInfo[] fields)) return fields;
            fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(field =>
                !field.IsStatic && !field.IsDefined(typeof(RuntimeHiddenAttribute), true) && (field.IsPublic ||
                    field.IsDefined(typeof(SerializeField), true) ||
                    field.IsDefined(typeof(RuntimeInspectableAttribute), true))).ToArray();
            _fieldCache[type] = fields;
            return fields;
        }

        private bool IsBlocked(Type type) => _settings.BlockedComponentTypes.Contains(type.FullName) ||
                                             _settings.BlockedNamespaces.Any(value =>
                                                 !string.IsNullOrEmpty(value) &&
                                                 (type.Namespace?.StartsWith(value, StringComparison.Ordinal) ??
                                                  false));

        private static string Nicify(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            var chars = new List<char>(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1])) chars.Add(' ');
                chars.Add(i == 0 ? char.ToUpperInvariant(c) : c);
            }

            return new string(chars.ToArray()).TrimStart('m', '_', ' ');
        }

        private RuntimeObjectId GetSceneId(Scene scene, bool persistent)
        {
            string key = persistent ? "persistent" : scene.handle + ":" + scene.name;
            if (!_sceneIds.TryGetValue(key, out RuntimeObjectId id))
                _sceneIds[key] = id = new RuntimeObjectId(_nextSceneId++);
            return id;
        }

        private static bool TryGetEnabled(Component component, out bool enabled)
        {
            if (component is Behaviour b)
            {
                enabled = b.enabled;
                return true;
            }

            if (component is Renderer r)
            {
                enabled = r.enabled;
                return true;
            }

            if (component is Collider c)
            {
                enabled = c.enabled;
                return true;
            }

            if (component is Collider2D c2)
            {
                enabled = c2.enabled;
                return true;
            }

            enabled = false;
            return false;
        }

        private static bool TrySetEnabled(Component component, bool value)
        {
            if (component is Behaviour b) b.enabled = value;
            else if (component is Renderer r) r.enabled = value;
            else if (component is Collider c) c.enabled = value;
            else if (component is Collider2D c2) c2.enabled = value;
            else return false;
            return true;
        }

        private void EnsureMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                throw new InvalidOperationException("Runtime debugger Unity access must occur on the main thread.");
        }

        private void OnSceneChanged(Scene scene, LoadSceneMode mode) => RefreshHierarchy();
        private void OnSceneUnloaded(Scene scene) => RefreshHierarchy();
        private void OnActiveSceneChanged(Scene oldScene, Scene newScene) => RefreshHierarchy();
    }
}