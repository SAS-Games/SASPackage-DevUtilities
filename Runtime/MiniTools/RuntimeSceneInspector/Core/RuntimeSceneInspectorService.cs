using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        private readonly List<Component> _componentBuffer = new();
        private readonly List<GameObject> _rootObjectBuffer = new();
        private readonly HashSet<int> _sceneHandleBuffer = new();
        private readonly HashSet<string> _memberIdBuffer = new(StringComparer.Ordinal);
        private readonly HashSet<string> _memberDisplayNameBuffer = new(StringComparer.Ordinal);
        private readonly ConditionalWeakTable<Component, MemberListCache> _memberListCache = new();
        private readonly ConditionalWeakTable<Component, ComponentDescriptorCache> _componentDescriptorCache = new();
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
                bool unchanged = MatchesCurrentHierarchy();
                _registry.EndReconciliation();
                if (!unchanged)
                {
                    _snapshot = new RuntimeHierarchySnapshot
                    {
                        Revision = _snapshot.Revision + 1,
                        Entries = BuildHierarchyEntries()
                    };
                }
                _rootObjectBuffer.Clear();
                _sceneHandleBuffer.Clear();
                _componentBuffer.Clear();
            }
        }

        private bool MatchesCurrentHierarchy()
        {
            IReadOnlyList<RuntimeHierarchyEntry> entries = _snapshot.Entries;
            int entryIndex = 0;
            bool matches = entries != null;
            _sceneHandleBuffer.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;
                _sceneHandleBuffer.Add(scene.handle);
                RuntimeObjectId sceneId = GetSceneId(scene, false);
                matches &= MatchSceneEntry(entries, ref entryIndex, sceneId, scene.name);
                _rootObjectBuffer.Clear();
                scene.GetRootGameObjects(_rootObjectBuffer);
                for (int rootIndex = 0; rootIndex < _rootObjectBuffer.Count; rootIndex++)
                    matches &= MatchObject(entries, ref entryIndex, _rootObjectBuffer[rootIndex], sceneId, sceneId);
            }

            RuntimeObjectId persistentId = default;
            foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform == null || transform.parent != null)
                    continue;
                GameObject gameObject = transform.gameObject;
                Scene scene = gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded || _sceneHandleBuffer.Contains(scene.handle))
                    continue;
                if (!persistentId.IsValid)
                {
                    persistentId = GetSceneId(scene, true);
                    matches &= MatchSceneEntry(entries, ref entryIndex, persistentId, "Persistent Objects");
                }

                matches &= MatchObject(entries, ref entryIndex, gameObject, persistentId, persistentId);
            }

            return matches && entryIndex == (entries?.Count ?? 0);
        }

        private bool MatchObject(IReadOnlyList<RuntimeHierarchyEntry> entries, ref int entryIndex,
            GameObject gameObject, RuntimeObjectId sceneId, RuntimeObjectId parentId)
        {
            if (gameObject == null || (!_settings.IncludeInactiveObjects && !gameObject.activeInHierarchy))
                return true;

            RuntimeObjectId id = _registry.GetOrCreate(gameObject);
            RuntimeHierarchyEntry expected = entryIndex < (entries?.Count ?? 0) ? entries[entryIndex] : null;
            entryIndex++;
            bool matches = expected != null && expected.Id == id && expected.ParentId == parentId &&
                           expected.SceneId == sceneId && expected.Kind == RuntimeHierarchyKind.GameObject &&
                           string.Equals(expected.Name, gameObject.name, StringComparison.Ordinal) &&
                           expected.ActiveSelf == gameObject.activeSelf &&
                           expected.ActiveInHierarchy == gameObject.activeInHierarchy;

            _componentBuffer.Clear();
            gameObject.GetComponents(_componentBuffer);
            string[] expectedNames = expected?.ComponentTypeNames;
            matches &= expectedNames != null && expectedNames.Length == _componentBuffer.Count;
            for (int i = 0; i < _componentBuffer.Count; i++)
            {
                Component component = _componentBuffer[i];
                string typeName = component == null ? "Missing Script" : component.GetType().Name;
                if (expectedNames == null || i >= expectedNames.Length ||
                    !string.Equals(expectedNames[i], typeName, StringComparison.Ordinal))
                    matches = false;
                if (component != null)
                    _registry.GetOrCreate(component);
            }
            _componentBuffer.Clear();

            Transform transform = gameObject.transform;
            for (int i = 0; i < transform.childCount; i++)
                matches &= MatchObject(entries, ref entryIndex, transform.GetChild(i).gameObject, sceneId, id);
            return matches;
        }

        private static bool MatchSceneEntry(IReadOnlyList<RuntimeHierarchyEntry> entries, ref int entryIndex,
            RuntimeObjectId sceneId, string sceneName)
        {
            RuntimeHierarchyEntry expected = entryIndex < (entries?.Count ?? 0) ? entries[entryIndex] : null;
            entryIndex++;
            return expected != null && expected.Id == sceneId && !expected.ParentId.IsValid &&
                   !expected.SceneId.IsValid && expected.Kind == RuntimeHierarchyKind.Scene &&
                   string.Equals(expected.Name, sceneName, StringComparison.Ordinal) && expected.ActiveSelf &&
                   expected.ActiveInHierarchy && expected.ComponentTypeNames == null;
        }

        private List<RuntimeHierarchyEntry> BuildHierarchyEntries()
        {
            var entries = new List<RuntimeHierarchyEntry>();
                _sceneHandleBuffer.Clear();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.IsValid() || !scene.isLoaded)
                        continue;
                    _sceneHandleBuffer.Add(scene.handle);
                    RuntimeObjectId sceneId = GetSceneId(scene, false);
                    entries.Add(new RuntimeHierarchyEntry
                    {
                        Id = sceneId, Kind = RuntimeHierarchyKind.Scene, Name = scene.name, ActiveSelf = true,
                        ActiveInHierarchy = true
                    });
                    _rootObjectBuffer.Clear();
                    scene.GetRootGameObjects(_rootObjectBuffer);
                    for (int rootIndex = 0; rootIndex < _rootObjectBuffer.Count; rootIndex++)
                        AddObject(entries, _rootObjectBuffer[rootIndex], sceneId, sceneId);
                }

                RuntimeObjectId persistentId = default;
                foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (transform == null || transform.parent != null)
                        continue;
                    GameObject gameObject = transform.gameObject;
                    Scene scene = gameObject.scene;
                    if (!scene.IsValid() || !scene.isLoaded ||
                        _sceneHandleBuffer.Contains(scene.handle))
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

            return entries;
        }

        public RuntimeObjectDetails InspectObject(RuntimeObjectId objectId)
        {
            EnsureMainThread();
            if (!_registry.TryResolve(objectId, out GameObject gameObject))
                return null;
            using (InspectorMarker.Auto())
            {
                var components = new List<RuntimeComponentDescriptor>();
                _componentBuffer.Clear();
                gameObject.GetComponents(_componentBuffer);
                for (int componentIndex = 0; componentIndex < _componentBuffer.Count;
                     componentIndex++)
                {
                    Component component = _componentBuffer[componentIndex];
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
                        components.Add(GetComponentDescriptor(component, type, false, false,
                            Array.Empty<RuntimeMemberDescriptor>(),
                            "Inspection is blocked by the runtime scene inspector settings."));
                        continue;
                    }

                    bool hasEnabled = TryGetEnabled(component, out bool enabled);
                    IReadOnlyList<RuntimeMemberDescriptor> members = BuildMembers(component);
                    components.Add(GetComponentDescriptor(component, type, hasEnabled, enabled, members,
                        members.Count == 0 ? "No supported runtime properties." : null));
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
                _componentBuffer.Clear();

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
            _componentBuffer.Clear();
            gameObject.GetComponents(_componentBuffer);
            var names = new string[_componentBuffer.Count];
            for (int i = 0; i < _componentBuffer.Count; i++)
            {
                Component component = _componentBuffer[i];
                names[i] = component == null ? "Missing Script" : component.GetType().Name;
                if (component != null)
                    _registry.GetOrCreate(component);
            }
            _componentBuffer.Clear();

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

        private RuntimeComponentDescriptor GetComponentDescriptor(Component component, Type type,
            bool hasEnabledState, bool enabled, IReadOnlyList<RuntimeMemberDescriptor> members,
            string statusMessage)
        {
            ComponentDescriptorCache cache = _componentDescriptorCache.GetValue(component,
                _ => new ComponentDescriptorCache());
            RuntimeObjectId id = _registry.GetOrCreate(component);
            RuntimeComponentDescriptor current = cache.Current;
            if (current != null && current.Id == id &&
                string.Equals(current.TypeName, type.FullName, StringComparison.Ordinal) &&
                current.HasEnabledState == hasEnabledState && current.Enabled == enabled &&
                current.EnabledReadOnly == !_settings.AllowComponentEnableChanges && !current.Missing &&
                string.Equals(current.StatusMessage, statusMessage, StringComparison.Ordinal) &&
                ReferenceEquals(current.Members, members))
                return current;

            cache.Current = new RuntimeComponentDescriptor
            {
                Id = id,
                TypeName = type.FullName,
                HasEnabledState = hasEnabledState,
                Enabled = enabled,
                EnabledReadOnly = !_settings.AllowComponentEnableChanges,
                StatusMessage = statusMessage,
                Members = members
            };
            return cache.Current;
        }

        private IReadOnlyList<RuntimeMemberDescriptor> BuildMembers(Component component)
        {
            MemberListCache cache = _memberListCache.GetValue(component, _ => new MemberListCache());
            var result = new List<RuntimeMemberDescriptor>();
            _memberIdBuffer.Clear();
            _memberDisplayNameBuffer.Clear();
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
                            _memberDisplayNameBuffer.Add(displayName);
                    }
                }

                IReadOnlyList<RuntimeMemberDescriptor> drawerMembers = componentDrawer.BuildInspector(component);
                if (drawerMembers != null)
                {
                    foreach (RuntimeMemberDescriptor member in drawerMembers)
                    {
                        if (member != null && _memberIdBuffer.Add(member.Name))
                        {
                            result.Add(member);
                            if (!string.IsNullOrEmpty(member.DisplayName))
                                _memberDisplayNameBuffer.Add(member.DisplayName);
                        }
                    }
                }
            }

            foreach (RuntimeMemberDescriptor member in _reflectedMembers.BuildInspector(component,
                         _memberDisplayNameBuffer, includeReflectedProperties))
            {
                if (member == null || _memberDisplayNameBuffer.Contains(member.DisplayName))
                    continue;
                if (!_memberIdBuffer.Add(member.Name))
                    continue;
                result.Add(member);
                if (!string.IsNullOrEmpty(member.DisplayName))
                    _memberDisplayNameBuffer.Add(member.DisplayName);
            }

            if (!_settings.AllowValueChanges)
            {
                foreach (RuntimeMemberDescriptor member in result)
                    member.ReadOnly = true;
            }

            if (MemberListsReferenceEqual(cache.Current, result))
                return cache.Current;

            cache.Current = result;
            return cache.Current;
        }

        private static bool MemberListsReferenceEqual(IReadOnlyList<RuntimeMemberDescriptor> left,
            IReadOnlyList<RuntimeMemberDescriptor> right)
        {
            if (left == null || right == null || left.Count != right.Count)
                return false;
            for (int i = 0; i < left.Count; i++)
            {
                if (!ReferenceEquals(left[i], right[i]))
                    return false;
            }

            return true;
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

        private bool IsBlocked(Type type)
        {
            string[] blockedTypes = _settings.BlockedComponentTypes;
            for (int i = 0; i < blockedTypes.Length; i++)
            {
                if (string.Equals(blockedTypes[i], type.FullName, StringComparison.Ordinal))
                    return true;
            }

            string typeNamespace = type.Namespace ?? string.Empty;
            string[] blockedNamespaces = _settings.BlockedNamespaces;
            for (int i = 0; i < blockedNamespaces.Length; i++)
            {
                string blockedNamespace = blockedNamespaces[i];
                if (!string.IsNullOrEmpty(blockedNamespace) &&
                    typeNamespace.StartsWith(blockedNamespace, StringComparison.Ordinal))
                    return true;
            }
            return false;
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

        private bool IsRuntimeSceneInspectorHost(GameObject gameObject)
        {
            if (gameObject == null)
                return false;
            _componentBuffer.Clear();
            gameObject.GetComponents(_componentBuffer);
            for (int i = 0; i < _componentBuffer.Count; i++)
            {
                if (!IsRuntimeSceneInspectorProtected(_componentBuffer[i]))
                    continue;
                _componentBuffer.Clear();
                return true;
            }
            _componentBuffer.Clear();
            return false;
        }

        private static bool IsRuntimeSceneInspectorProtected(Component component) => component != null && component.GetType().IsDefined(typeof(RuntimeSceneInspectorProtectedAttribute), true);

        private void EnsureMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                throw new InvalidOperationException("Runtime Scene Inspector Unity access must occur on the main thread.");
        }

        private void OnSceneChanged(Scene scene, LoadSceneMode mode) => RefreshHierarchy();
        private void OnSceneUnloaded(Scene scene) => RefreshHierarchy();
        private void OnActiveSceneChanged(Scene oldScene, Scene newScene) => RefreshHierarchy();

        private sealed class MemberListCache
        {
            internal List<RuntimeMemberDescriptor> Current;
        }

        private sealed class ComponentDescriptorCache
        {
            internal RuntimeComponentDescriptor Current;
        }

    }
}
