using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector
{
    internal sealed class RemoteMaterialInspectorView
    {
        private static readonly string[] MaterialScopes =
        {
            "Selected Renderer",
            "Material Instance",
            "Shared Material",
            "Global Shader Property"
        };

        private readonly HashSet<long> _expandedRenderers = new();
        private readonly HashSet<string> _expandedSlots = new();
        private readonly Dictionary<string, string> _editValues = new();
        private readonly Dictionary<string, int> _materialScopes = new();
        private long _inspectionRevision = long.MinValue;
        private string _shaderSearch = string.Empty;

        public void Draw(RemoteRuntimeSceneInspectorClient client, RemoteMaterialShaderSection section)
        {
            SynchronizeInspection(client.InspectionRevision);
            if (section?.Renderers == null || section.Renderers.Length == 0)
                return;

            EditorGUILayout.Space(5f);
            DrawHeader(section);

            foreach (RemoteRendererMaterialDescriptor renderer in section.Renderers)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                bool rendererExpanded = _expandedRenderers.Contains(renderer.RendererId);
                bool nextRenderer = EditorGUILayout.Foldout(rendererExpanded, $"{renderer.RendererName} " + $"({RemoteInspectorFormatting.ShortTypeName(renderer.RendererType)})", true);
                RemoteInspectorFormatting.SetExpanded(_expandedRenderers, renderer.RendererId, nextRenderer);
                if (nextRenderer)
                    DrawMaterialSlots(client, renderer);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawHeader(RemoteMaterialShaderSection section)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(section.DisplayName ?? "Materials & Shaders", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            _shaderSearch = EditorGUILayout.TextField(_shaderSearch, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.Width(190f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMaterialSlots(RemoteRuntimeSceneInspectorClient client, RemoteRendererMaterialDescriptor renderer)
        {
            foreach (RemoteMaterialSlotDescriptor slot in renderer.MaterialSlots ?? Array.Empty<RemoteMaterialSlotDescriptor>())
            {
                string slotKey = $"{renderer.RendererId}:{slot.MaterialIndex}";
                bool expanded = _expandedSlots.Contains(slotKey);
                bool next = EditorGUILayout.Foldout(expanded, $"Slot {slot.MaterialIndex}: {slot.MaterialName ?? "<null>"}", true);
                RemoteInspectorFormatting.SetExpanded(_expandedSlots, slotKey, next);
                if (!next)
                    continue;

                EditorGUI.indentLevel++;
                DrawSlotMetadata(slot, slotKey, out int scope);
                DrawShaderProperties(client, renderer.RendererId, slot, scope);
                DrawRestoreButton(client, renderer.RendererId, slot, scope);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawSlotMetadata(RemoteMaterialSlotDescriptor slot, string slotKey, out int scope)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Shader", slot.ShaderName ?? "<missing>");
                EditorGUILayout.IntField("Render Queue", slot.RenderQueue);
                EditorGUILayout.Toggle("GPU Instancing", slot.EnableInstancing);
            }

            if (!_materialScopes.TryGetValue(slotKey, out scope))
                scope = 0;
            scope = EditorGUILayout.Popup("Edit Scope", scope, MaterialScopes);
            _materialScopes[slotKey] = scope;

            if (scope == 2 || scope == 3)
            {
                EditorGUILayout.HelpBox(scope == 2 ? "Shared Material changes may affect multiple renderers." : "Global shader changes may affect multiple materials and shaders.", MessageType.Warning);
            }
        }

        private void DrawShaderProperties(RemoteRuntimeSceneInspectorClient client, long rendererId, RemoteMaterialSlotDescriptor slot, int scope)
        {
            foreach (RemoteShaderPropertyView property in slot.Properties ?? Array.Empty<RemoteShaderPropertyView>())
            {
                if (MatchesShaderSearch(property))
                    DrawShaderProperty(client, rendererId, slot, property, scope);
            }

            if (slot.PropertyLimitReached)
            {
                EditorGUILayout.HelpBox($"Showing a limited set of {slot.TotalPropertyCount} shader properties.", MessageType.Info);
            }
        }

        private static void DrawRestoreButton(RemoteRuntimeSceneInspectorClient client, long rendererId, RemoteMaterialSlotDescriptor slot, int scope)
        {
            if (!GUILayout.Button("Restore Slot"))
                return;

            client.Execute(new RemoteSceneInspectorCommandRequest
            {
                Kind = RemoteSceneInspectorCommandKind.RestoreMaterial,
                RendererId = rendererId,
                MaterialIndex = slot.MaterialIndex,
                MaterialScope = scope
            });
        }

        private void DrawShaderProperty(RemoteRuntimeSceneInspectorClient client, long rendererId, RemoteMaterialSlotDescriptor slot, RemoteShaderPropertyView property, int scope)
        {
            string key = $"shader:{rendererId}:{slot.MaterialIndex}:{property.PropertyId}:{scope}";
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(property.DisplayName ?? property.Name, GUILayout.Width(155f));

            if (property.ReadOnly)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField(property.Value ?? string.Empty);
            }
            else
            {
                DrawEditableShaderProperty(client, rendererId, slot, property, scope, key);
            }

            DrawResetButton(client, rendererId, slot, property, scope);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"{property.Name}  •  {property.ValueSource}", EditorStyles.miniLabel);
        }

        private void DrawEditableShaderProperty(RemoteRuntimeSceneInspectorClient client, long rendererId, RemoteMaterialSlotDescriptor slot, RemoteShaderPropertyView property, int scope, string key)
        {
            if (!_editValues.TryGetValue(key, out string value))
                value = property.Value ?? string.Empty;
            string controlName = "remote-shader-value:" + key;
            GUI.SetNextControlName(controlName);
            value = EditorGUILayout.TextField(value);
            _editValues[key] = value;
            bool dirty = value != (property.Value ?? string.Empty);
            bool apply;
            using (new EditorGUI.DisabledScope(!dirty))
                apply = GUILayout.Button("Apply", GUILayout.Width(48f));
            if (dirty && RemoteInspectorInput.IsApplyKey(Event.current,
                    GUI.GetNameOfFocusedControl(), controlName))
            {
                apply = true;
                Event.current.Use();
            }
            if (apply)
            {
                client.Execute(new RemoteSceneInspectorCommandRequest
                {
                    Kind = RemoteSceneInspectorCommandKind.SetShaderProperty,
                    RendererId = rendererId,
                    MaterialIndex = slot.MaterialIndex,
                    PropertyId = property.PropertyId,
                    MaterialScope = scope,
                    Value = value
                });
            }
        }

        private void SynchronizeInspection(long inspectionRevision)
        {
            if (_inspectionRevision == inspectionRevision)
                return;

            _editValues.Clear();
            _inspectionRevision = inspectionRevision;
        }

        private static void DrawResetButton(RemoteRuntimeSceneInspectorClient client, long rendererId, RemoteMaterialSlotDescriptor slot, RemoteShaderPropertyView property, int scope)
        {
            using (new EditorGUI.DisabledScope(!property.HasInspectorOverride))
            {
                if (!GUILayout.Button("Reset", GUILayout.Width(48f)))
                    return;

                client.Execute(new RemoteSceneInspectorCommandRequest
                {
                    Kind = RemoteSceneInspectorCommandKind.RestoreShaderProperty,
                    RendererId = rendererId,
                    MaterialIndex = slot.MaterialIndex,
                    PropertyId = property.PropertyId,
                    MaterialScope = scope
                });
            }
        }

        private bool MatchesShaderSearch(RemoteShaderPropertyView property)
        {
            if (string.IsNullOrWhiteSpace(_shaderSearch))
                return true;

            return (property.Name?.IndexOf(_shaderSearch, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || (property.DisplayName?.IndexOf(_shaderSearch, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        }
    }
}
