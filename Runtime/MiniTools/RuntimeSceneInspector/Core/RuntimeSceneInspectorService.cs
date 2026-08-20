using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    public sealed class RuntimeSceneInspectorService : IRuntimeSceneInspector, IRuntimeSceneObjectResolver, IDisposable
    {
        private static readonly ProfilerMarker HierarchyMarker = new("RuntimeSceneInspector.Hierarchy.Reconcile");
        private static readonly ProfilerMarker InspectorMarker = new("RuntimeSceneInspector.Inspector.Build");
        private readonly RuntimeSceneInspectorSettings _settings;
        private readonly RuntimeObjectRegistry _registry = new();
        private readonly RuntimeValueDrawerRegistry _drawers = new();
        private readonly RuntimeComponentDrawerRegistry _componentDrawers;
        private readonly RuntimeReflectedMemberProvider _reflectedMembers;
        private readonly List<IRuntimeSceneInspectorExtension> _inspectorExtensions = new();
        private readonly Dictionary<string, RuntimeObjectId> _sceneIds = new();
        private readonly int _mainThreadId;
        private RuntimeHierarchySnapshot _snapshot = new() { Entries = Array.Empty<RuntimeHierarchyEntry>() };
        private long _nextSceneId = 1L << 60;

        public RuntimeSceneInspectorService(RuntimeSceneInspectorSettings settings)
        {
            _settings = settings;
            _componentDrawers = new RuntimeComponentDrawerRegistry(_drawers);
            _reflectedMembers = new RuntimeReflectedMemberProvider(_drawers);
            _inspectorExtensions.Add(new RuntimeMaterialShaderExtension(settings));
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
                    if (!scene.IsValid() || !scene.isLoaded)
                        continue;
                    normalHandles.Add(scene.handle);
                    RuntimeObjectId sceneId = GetSceneId(scene, false);
                    entries.Add(new RuntimeHierarchyEntry
                    {
                        Id = sceneId, Kind = RuntimeHierarchyKind.Scene, Name = scene.name, ActiveSelf = true,
                        ActiveInHierarchy = true
                    });
                    foreach (GameObject root in scene.GetRootGameObjects())
                        AddObject(entries, root, sceneId, sceneId);
                }

                RuntimeObjectId persistentId = default;
                foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (transform == null || transform.parent != null)
                        continue;
                    GameObject gameObject = transform.gameObject;
                    Scene scene = gameObject.scene;
                    if (!scene.IsValid() || !scene.isLoaded || normalHandles.Contains(scene.handle))
                        continue;
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
            if (!_registry.TryResolve(objectId, out GameObject gameObject))
                return null;
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
                            StatusMessage = "Inspection is blocked by the runtime scene inspector settings.",
                            Members = Array.Empty<RuntimeMemberDescriptor>()
                        });
                        continue;
                    }

                    bool hasEnabled = TryGetEnabled(component, out bool enabled);
                    IReadOnlyList<RuntimeMemberDescriptor> members = BuildMembers(component);
                    components.Add(new RuntimeComponentDescriptor
                    {
                        Id = _registry.GetOrCreate(component), TypeName = type.FullName, HasEnabledState = hasEnabled,
                        Enabled = enabled, EnabledReadOnly = !_settings.AllowComponentEnableChanges,
                        StatusMessage = members.Count == 0 ? "No supported runtime properties." : null,
                        Members = members
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

                var details = new RuntimeObjectDetails
                {
                    Id = objectId, Name = gameObject.name, Active = gameObject.activeSelf,
                    ActiveReadOnly = !_settings.AllowActivationChanges, Tag = tag,
                    Layer = gameObject.layer, LayerReadOnly = !_settings.AllowValueChanges,
                    Components = components
                };
                foreach (IRuntimeSceneInspectorExtension extension in _inspectorExtensions)
                    extension.Inspect(gameObject, _registry, details);
                return details;
            }
        }

        /// <summary>Resolves a hierarchy ID for local preview and tooling integrations.</summary>
        public bool TryResolveObject(RuntimeObjectId objectId, out GameObject gameObject)
        {
            EnsureMainThread();
            return _registry.TryResolve(objectId, out gameObject);
        }

        public bool TryGetObjectId(GameObject target, out RuntimeObjectId objectId)
        {
            EnsureMainThread();
            objectId = default;
            for (Transform current = target != null ? target.transform : null; current != null; current = current.parent)
            {
                if (_registry.TryGetId(current.gameObject, out objectId))
                    return true;
            }

            return false;
        }

        public RuntimeCommandResult Execute(RuntimeSceneInspectorCommand command)
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
                    if (!active.Active && IsRuntimeSceneInspectorHost(target))
                        return RuntimeCommandResult.Fail("The runtime scene inspector cannot disable its own host.");
                    target.SetActive(active.Active);
                    RefreshHierarchy();
                    return RuntimeCommandResult.Ok();
                }

                if (command is SetGameObjectLayerCommand layer)
                {
                    if (!_settings.AllowValueChanges)
                        return RuntimeCommandResult.Fail("Value changes are disabled.");
                    if (layer.Layer < 0 || layer.Layer > 31)
                        return RuntimeCommandResult.Fail("GameObject layer must be between 0 and 31.");
                    if (!_registry.TryResolve(layer.ObjectId, out GameObject target))
                        return RuntimeCommandResult.Fail("The GameObject no longer exists.");
                    target.layer = layer.Layer;
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
                    if (!componentEnabled.Enabled && IsRuntimeSceneInspectorProtected(component))
                        return RuntimeCommandResult.Fail("The runtime scene inspector cannot disable its own host.");
                    return TrySetEnabled(component, componentEnabled.Enabled)
                        ? RuntimeCommandResult.Ok()
                        : RuntimeCommandResult.Fail("This component has no supported enabled state.");
                }

                if (command is SetMemberValueCommand setValue)
                {
                    if (!_settings.AllowValueChanges)
                        return RuntimeCommandResult.Fail("Value changes are disabled.");
                    if (!_registry.TryResolve(setValue.ComponentId, out Component component))
                        return RuntimeCommandResult.Fail("The component no longer exists.");
                    if (IsBlocked(component.GetType()))
                        return RuntimeCommandResult.Fail("The component type is blocked.");
                    return SetMember(component, setValue.MemberName, setValue.Value);
                }

                foreach (IRuntimeSceneInspectorExtension extension in _inspectorExtensions)
                {
                    if (extension.TryExecute(command, _registry, out RuntimeCommandResult result))
                        return result ?? RuntimeCommandResult.Fail("The inspector extension did not return a result.");
                }

                return RuntimeCommandResult.Fail("Unsupported Scene Inspector command.");
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
            foreach (IRuntimeSceneInspectorExtension extension in _inspectorExtensions)
                extension.Dispose();
            _inspectorExtensions.Clear();
        }

        private void AddObject(List<RuntimeHierarchyEntry> entries, GameObject gameObject, RuntimeObjectId sceneId, RuntimeObjectId parentId)
        {
            if (gameObject == null || (!_settings.IncludeInactiveObjects && !gameObject.activeInHierarchy))
                return;
            RuntimeObjectId id = _registry.GetOrCreate(gameObject);
            Component[] components = gameObject.GetComponents<Component>();
            var names = new string[components.Length];
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                names[i] = component == null ? "Missing Script" : component.GetType().Name;
                if (component != null)
                    _registry.GetOrCreate(component);
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
            var memberIds = new HashSet<string>(StringComparer.Ordinal);
            var memberDisplayNames = new HashSet<string>(StringComparer.Ordinal);
            bool includeReflectedProperties = true;
            IReadOnlyList<IRuntimeComponentDrawer> componentDrawers = _componentDrawers.Resolve(component.GetType());
            foreach (IRuntimeComponentDrawer componentDrawer in componentDrawers)
            {
                if (componentDrawer is IRuntimeEditableComponentDrawer ownedDrawer)
                {
                    if (ownedDrawer.ComponentType == component.GetType())
                        includeReflectedProperties = false;
                    foreach (string displayName in ownedDrawer.OwnedDisplayNames)
                    {
                        if (!string.IsNullOrEmpty(displayName))
                            memberDisplayNames.Add(displayName);
                    }
                }

                IReadOnlyList<RuntimeMemberDescriptor> drawerMembers = componentDrawer.BuildInspector(component);
                if (drawerMembers != null)
                {
                    foreach (RuntimeMemberDescriptor member in drawerMembers)
                    {
                        if (member != null && memberIds.Add(member.Name))
                        {
                            result.Add(member);
                            if (!string.IsNullOrEmpty(member.DisplayName))
                                memberDisplayNames.Add(member.DisplayName);
                        }
                    }
                }
            }

            foreach (RuntimeMemberDescriptor member in _reflectedMembers.BuildInspector(component,
                         memberDisplayNames, includeReflectedProperties))
            {
                if (member == null || memberDisplayNames.Contains(member.DisplayName))
                    continue;
                if (!memberIds.Add(member.Name))
                    continue;
                result.Add(member);
                if (!string.IsNullOrEmpty(member.DisplayName))
                    memberDisplayNames.Add(member.DisplayName);
            }

            if (!_settings.AllowValueChanges)
            {
                foreach (RuntimeMemberDescriptor member in result)
                    member.ReadOnly = true;
            }

            return result;
        }

        private RuntimeCommandResult SetMember(Component component, string name, string text)
        {
            IReadOnlyList<IRuntimeComponentDrawer> componentDrawers = _componentDrawers.Resolve(component.GetType());
            foreach (IRuntimeComponentDrawer componentDrawer in componentDrawers)
            {
                if (componentDrawer is IRuntimeEditableComponentDrawer editableDrawer && editableDrawer.TrySetValue(component, name, text, out RuntimeCommandResult drawerResult))
                    return drawerResult ?? RuntimeCommandResult.Fail("The member could not be changed.");
            }

            if (_reflectedMembers.TrySetValue(component, name, text, out RuntimeCommandResult reflectedResult))
                return reflectedResult ?? RuntimeCommandResult.Fail("The member could not be changed.");

            return RuntimeCommandResult.Fail("The member is not writable.");
        }

        private bool IsBlocked(Type type) => _settings.BlockedComponentTypes.Contains(type.FullName) || _settings.BlockedNamespaces.Any(value => !string.IsNullOrEmpty(value) && (type.Namespace?.StartsWith(value, StringComparison.Ordinal) ?? false));

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

            enabled = false;
            return false;
        }

        private static bool TrySetEnabled(Component component, bool value)
        {
            if (component is Behaviour b)
                b.enabled = value;
            else if (component is Renderer r)
                r.enabled = value;
            else if (component is Collider c)
                c.enabled = value;
            else
                return false;
            return true;
        }

        private static bool IsRuntimeSceneInspectorHost(GameObject gameObject) => gameObject != null && gameObject.GetComponents<Component>().Any(IsRuntimeSceneInspectorProtected);

        private static bool IsRuntimeSceneInspectorProtected(Component component) => component != null && component.GetType().IsDefined(typeof(RuntimeSceneInspectorProtectedAttribute), true);

        private void EnsureMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                throw new InvalidOperationException("Runtime Scene Inspector Unity access must occur on the main thread.");
        }

        private void OnSceneChanged(Scene scene, LoadSceneMode mode) => RefreshHierarchy();
        private void OnSceneUnloaded(Scene scene) => RefreshHierarchy();
        private void OnActiveSceneChanged(Scene oldScene, Scene newScene) => RefreshHierarchy();
    }
}
