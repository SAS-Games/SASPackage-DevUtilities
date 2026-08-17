using System;
using System.Collections.Generic;
using HP.DevUtilities;
using HP.Utilities.DeveloperConsole;
using HP.Utilities.Presentation;
using HP.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry;
using HP.Utilities.RemoteDevUtilities.MiniTools;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HP.Utilities.RemoteDevUtilities.Editor.MiniTools.Scaffolding
{
    [InitializeOnLoad]
    internal static class MiniToolScaffoldCompletion
    {
        private static bool _scheduled;

        static MiniToolScaffoldCompletion()
        {
            ScheduleCompletion();
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            ScheduleCompletion();
        }

        [MenuItem("Tools/Dev Utilities/Complete Pending Mini Tool", priority = 121)]
        private static void CompleteFromMenu()
        {
            TryComplete(true);
        }

        [MenuItem("Tools/Dev Utilities/Complete Pending Mini Tool", true)]
        private static bool ValidateCompleteFromMenu()
        {
            return MiniToolScaffoldPersistence.HasPending;
        }

        [MenuItem("Tools/Dev Utilities/Cancel Pending Mini Tool", priority = 122)]
        private static void CancelFromMenu()
        {
            if (!MiniToolScaffoldPersistence.HasPending || !EditorUtility.DisplayDialog("Cancel Mini Tool Creation", "Cancel the pending mini-tool creation? Generated script files will be kept.", "Cancel Creation", "Keep Waiting"))
                return;

            MiniToolScaffoldPersistence.Clear();
        }

        [MenuItem("Tools/Dev Utilities/Cancel Pending Mini Tool", true)]
        private static bool ValidateCancelFromMenu()
        {
            return MiniToolScaffoldPersistence.HasPending;
        }

        private static void ScheduleCompletion()
        {
            if (_scheduled || !MiniToolScaffoldPersistence.HasPending)
                return;

            _scheduled = true;
            EditorApplication.delayCall += CompleteAfterDelay;
        }

        private static void CompleteAfterDelay()
        {
            _scheduled = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleCompletion();
                return;
            }

            TryComplete(false);
        }

        private static void TryComplete(bool reportFailure)
        {
            if (!MiniToolScaffoldPersistence.TryLoad(out MiniToolScaffoldState state))
                return;

            if (!TryResolveTypes(state, out MiniToolScaffoldTypes types, out string error))
            {
                if (reportFailure)
                    EditorUtility.DisplayDialog("Mini Tool Is Not Ready", error + "\n\nFix any compilation errors and run Complete Pending Mini Tool again.", "OK");
                else
                    Debug.LogWarning("[Remote Dev Utilities] Mini-tool scaffold is waiting for generated scripts to compile. " + error);
                return;
            }

            var createdAssets = new List<string>();
            try
            {
                EnsureCompletionPathsAreAvailable(state);
                createdAssets.Add(state.PrefabPath);
                GameObject prefab = CreatePrefab(state, types);
                ConsoleCommand command = null;
                if (state.Request.CreateCommand)
                {
                    createdAssets.Add(state.CommandAssetPath);
                    command = CreateCommandAsset(state, types.CommandType, prefab);
                }

                createdAssets.Add(state.DefinitionPath);
                MiniToolDefinition definition = CreateDefinition(state, types.DataProviderType, command, prefab);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                MiniToolRegistry.Invalidate();
                MiniToolScaffoldPersistence.Clear();
                Selection.activeObject = definition;
                EditorGUIUtility.PingObject(definition);
                Debug.Log($"[Remote Dev Utilities] Created mini-tool '{state.Request.ToolName}' at '{state.TargetFolder}'.", definition);
            }
            catch (Exception exception)
            {
                for (int i = createdAssets.Count - 1; i >= 0; i--)
                    AssetDatabase.DeleteAsset(createdAssets[i]);
                AssetDatabase.SaveAssets();
                Debug.LogException(exception);
                if (reportFailure)
                    EditorUtility.DisplayDialog("Mini Tool Creation Failed", exception.GetBaseException().Message, "OK");
            }
        }

        private static bool TryResolveTypes(MiniToolScaffoldState state, out MiniToolScaffoldTypes types, out string error)
        {
            types = default;
            if (!TryResolveScriptType(state.SnapshotProviderScriptPath, out types.SnapshotProviderType) || !TryResolveScriptType(state.DataProviderScriptPath, out types.DataProviderType) || !TryResolveScriptType(state.ViewScriptPath, out types.ViewType) || !TryResolveScriptType(state.LocalControllerScriptPath, out types.LocalControllerType) || state.Request.CreateCommand && !TryResolveScriptType(state.CommandScriptPath, out types.CommandType))
            {
                error = "One or more generated script types cannot be loaded.";
                return false;
            }

            types.SnapshotType = GetSnapshotType(types.SnapshotProviderType);
            if (types.SnapshotType == null)
            {
                error = "The generated snapshot provider does not expose a typed snapshot contract.";
                return false;
            }

            Type snapshotProviderContract = typeof(IMiniToolSnapshotProvider<>).MakeGenericType(types.SnapshotType);
            Type snapshotViewContract = typeof(IMiniToolSnapshotView<>).MakeGenericType(types.SnapshotType);
            if (!typeof(IMiniToolSnapshot).IsAssignableFrom(types.SnapshotType) || !snapshotProviderContract.IsAssignableFrom(types.SnapshotProviderType) || !typeof(IMiniToolDataProvider).IsAssignableFrom(types.DataProviderType) || !snapshotViewContract.IsAssignableFrom(types.ViewType) || !typeof(IMiniToolLocalController).IsAssignableFrom(types.LocalControllerType) || state.Request.CreateCommand && !typeof(ConsoleCommand).IsAssignableFrom(types.CommandType))
            {
                error = "Generated types do not implement the expected mini-tool contracts.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static Type GetSnapshotType(Type providerType)
        {
            foreach (Type implementedInterface in providerType.GetInterfaces())
            {
                if (implementedInterface.IsGenericType && implementedInterface.GetGenericTypeDefinition() == typeof(IMiniToolSnapshotProvider<>))
                    return implementedInterface.GetGenericArguments()[0];
            }

            return null;
        }

        private static bool TryResolveScriptType(string path, out Type type)
        {
            MonoScript script = string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            type = script?.GetClass();
            return type != null;
        }

        private static void EnsureCompletionPathsAreAvailable(MiniToolScaffoldState state)
        {
            foreach (string path in new[] { state.PrefabPath, state.Request.CreateCommand ? state.CommandAssetPath : string.Empty, state.DefinitionPath })
            {
                if (!string.IsNullOrWhiteSpace(path) && AssetDatabase.LoadMainAssetAtPath(path) != null)
                    throw new InvalidOperationException($"Cannot complete the mini-tool because '{path}' already exists.");
            }
        }

        private static GameObject CreatePrefab(MiniToolScaffoldState state, MiniToolScaffoldTypes types)
        {
            var root = new GameObject(state.ClassName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                ConfigureCanvas(root);
                Text display = CreateVisuals(root.transform, state.Request.ToolName);
                Component snapshotProvider = root.AddComponent(types.SnapshotProviderType);
                Component view = root.AddComponent(types.ViewType);
                Component localController = root.AddComponent(types.LocalControllerType);
                var presentation = root.AddComponent<DevUtilityPresentation>();

                AssignObjectReference(view, "m_Display", display);
                AssignObjectReference(localController, "m_SnapshotProvider", snapshotProvider);
                AssignObjectReference(localController, "m_View", view);
                AssignObjectReference(presentation, "m_PresentationRoot", root);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, state.PrefabPath, out bool success);
                if (!success || prefab == null)
                    throw new InvalidOperationException($"Could not save the generated prefab at '{state.PrefabPath}'.");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureCanvas(GameObject root)
        {
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10000;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static Text CreateVisuals(Transform root, string displayName)
        {
            var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(root, false);
            var panel = (RectTransform)panelObject.transform;
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(16f, -16f);
            panel.sizeDelta = new Vector2(420f, 140f);
            panelObject.GetComponent<Image>().color = new Color(0.055f, 0.065f, 0.08f, 0.94f);

            var textObject = new GameObject("Display", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panel, false);
            var textTransform = (RectTransform)textObject.transform;
            textTransform.anchorMin = Vector2.zero;
            textTransform.anchorMax = Vector2.one;
            textTransform.offsetMin = new Vector2(12f, 10f);
            textTransform.offsetMax = new Vector2(-12f, -10f);

            Text display = textObject.GetComponent<Text>();
#if UNITY_6000_0_OR_NEWER
            display.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            display.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            display.fontSize = 16;
            display.alignment = TextAnchor.UpperLeft;
            display.color = Color.white;
            display.supportRichText = true;
            display.raycastTarget = false;
            display.text = $"<b>{displayName}</b>\nWaiting for data...";
            return display;
        }

        private static ConsoleCommand CreateCommandAsset(MiniToolScaffoldState state, Type commandType, GameObject prefab)
        {
            var command = ScriptableObject.CreateInstance(commandType) as ConsoleCommand;
            if (command == null)
                throw new InvalidOperationException($"Could not create command '{commandType.FullName}'.");

            command.name = state.ClassName + "Command";
            AssetDatabase.CreateAsset(command, state.CommandAssetPath);
            var serialized = new SerializedObject(command);
            string commandName = state.ClassName;
            serialized.FindProperty("m_CommandName").stringValue = commandName;
            SerializedProperty presets = serialized.FindProperty("m_Presets");
            presets.arraySize = 3;
            presets.GetArrayElementAtIndex(0).stringValue = commandName;
            presets.GetArrayElementAtIndex(1).stringValue = commandName + " On";
            presets.GetArrayElementAtIndex(2).stringValue = commandName + " Off";
            SerializedProperty closeOnCompletion = serialized.FindProperty("<CloseOnCompletion>k__BackingField");
            if (closeOnCompletion != null)
                closeOnCompletion.boolValue = true;
            serialized.FindProperty("m_Prefab").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(command);
            return command;
        }

        private static MiniToolDefinition CreateDefinition(MiniToolScaffoldState state, Type providerType, ConsoleCommand command, GameObject prefab)
        {
            var definition = ScriptableObject.CreateInstance<MiniToolDefinition>();
            definition.name = state.ClassName + " Mini Tool";
            AssetDatabase.CreateAsset(definition, state.DefinitionPath);

            var serialized = new SerializedObject(definition);
            string slug = MiniToolScaffoldNaming.ToSlug(state.Request.ToolName);
            if (string.IsNullOrWhiteSpace(slug))
                slug = "tool";
            serialized.FindProperty("_toolId").stringValue = $"custom.{slug}.{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            serialized.FindProperty("_displayName").stringValue = state.Request.ToolName.Trim();
            serialized.FindProperty("_description").stringValue = state.Request.Description?.Trim() ?? string.Empty;
            serialized.FindProperty("_updateInterval").floatValue = Mathf.Max(0.1f, state.Request.UpdateInterval);
            serialized.FindProperty("_providerScriptGuid").stringValue = AssetDatabase.AssetPathToGUID(state.DataProviderScriptPath);
            serialized.FindProperty("_providerTypeName").stringValue = $"{providerType.FullName}, {providerType.Assembly.GetName().Name}";
            serialized.FindProperty("_command").objectReferenceValue = command;
            serialized.FindProperty("_commandName").stringValue = command != null ? state.ClassName : string.Empty;
            serialized.FindProperty("_commandRouting").enumValueIndex = (int)state.Request.CommandRouting;
            serialized.FindProperty("_debugHostPrefabGuid").stringValue = AssetDatabase.AssetPathToGUID(state.PrefabPath);
            serialized.FindProperty("_visibleByDefault").boolValue = state.Request.VisibleByDefault;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void AssignObjectReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new MissingFieldException(target.GetType().FullName, propertyName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private struct MiniToolScaffoldTypes
        {
            internal Type SnapshotType;
            internal Type SnapshotProviderType;
            internal Type DataProviderType;
            internal Type ViewType;
            internal Type LocalControllerType;
            internal Type CommandType;
        }
    }
}
