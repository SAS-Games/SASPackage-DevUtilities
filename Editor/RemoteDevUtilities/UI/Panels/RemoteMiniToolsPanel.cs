using System;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.UI.Panels
{
    internal sealed class RemoteMiniToolsPanel
    {
        private Vector2 _scroll;

        public void Draw(RemoteMiniToolClient client, bool connected)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Target-side collectors; Editor-side presentation", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Configure...", EditorStyles.toolbarButton))
                RemoteMiniToolConfigurationWindow.Open();
            using (new EditorGUI.DisabledScope(!connected))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                    client.RequestCatalog();
            }
            EditorGUILayout.EndHorizontal();

            if (!connected)
            {
                EditorGUILayout.HelpBox("Connect to a runtime Player to use mini-tools.", MessageType.Info);
                return;
            }

            if (!string.IsNullOrEmpty(client.Error))
                EditorGUILayout.HelpBox(client.Error, MessageType.Warning);

            RemoteMiniToolVisibilitySettings settings = RemoteMiniToolVisibilitySettings.instance;
            settings.RegisterCatalog(client.Tools);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            int visibleTools = 0;
            foreach (RemoteMiniToolDescriptor tool in client.Tools)
            {
                if (tool == null || string.IsNullOrWhiteSpace(tool.Id))
                    continue;
                bool hasActions = HasNativeWorkspaceActions(tool);
                if (tool.Capabilities !=
                        RemoteMiniToolCapabilities.None &&
                    (tool.Capabilities &
                     RemoteMiniToolCapabilities
                         .NativeWorkspaceFields) == 0 &&
                    !hasActions)
                {
                    continue;
                }
                if (!settings.IsVisible(tool.Id))
                    continue;
                visibleTools++;
                DrawTool(client, tool);
            }

            if (visibleTools == 0)
            {
                EditorGUILayout.HelpBox(
                    "No selected mini-tools expose Native Workspace fields or actions. " +
                    "Typed-state-only tools remain available in the Debug Host.",
                    MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawTool(RemoteMiniToolClient client, RemoteMiniToolDescriptor descriptor)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(descriptor.DisplayName, EditorStyles.boldLabel);
            bool subscribed = client.IsSubscribed(descriptor.Id);
            if (GUILayout.Button(subscribed ? "Stop" : "Start", subscribed ? EditorStyles.miniButtonLeft : EditorStyles.miniButton, GUILayout.Width(58f)))
            {
                client.SetSubscription(descriptor.Id, !subscribed, descriptor.DefaultIntervalSeconds);
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(descriptor.Description))
                EditorGUILayout.LabelField(descriptor.Description, EditorStyles.wordWrappedMiniLabel);

            DrawActions(client, descriptor, subscribed);

            if (client.Samples.TryGetValue(descriptor.Id, out RemoteMiniToolSample sample))
            {
                EditorGUILayout.Space(3f);
                RemoteMiniToolField[] fields = sample.Fields ?? Array.Empty<RemoteMiniToolField>();
                foreach (RemoteMiniToolField field in fields)
                {
                    string value = string.IsNullOrWhiteSpace(field.Unit) ? field.Value : $"{field.Value} {field.Unit}";
                    EditorGUILayout.LabelField(field.DisplayName ?? field.Name, value);
                }
                EditorGUILayout.LabelField($"Target frame {sample.Frame}", EditorStyles.centeredGreyMiniLabel);
            }
            else
                EditorGUILayout.LabelField(subscribed ? "Waiting for a target sample…" : "Not subscribed", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private static void DrawActions(
            RemoteMiniToolClient client,
            RemoteMiniToolDescriptor descriptor,
            bool subscribed)
        {
            RemoteMiniToolActionDescriptor[] actions =
                descriptor.Actions ??
                Array.Empty<RemoteMiniToolActionDescriptor>();
            if (actions.Length == 0)
                return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Controls");
            using (new EditorGUI.DisabledScope(!subscribed))
            {
                foreach (RemoteMiniToolActionDescriptor action in actions)
                {
                    if (action == null ||
                        string.IsNullOrWhiteSpace(action.Id) ||
                        action.HideInNativeWorkspace)
                    {
                        continue;
                    }

                    string label =
                        string.IsNullOrWhiteSpace(action.DisplayName)
                            ? action.Id
                            : action.DisplayName;
                    if (GUILayout.Button(
                            label,
                            EditorStyles.miniButton,
                            GUILayout.MinWidth(54f)))
                    {
                        client.ExecuteAction(
                            descriptor.Id,
                            action.Id);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private static bool HasNativeWorkspaceActions(
            RemoteMiniToolDescriptor descriptor)
        {
            foreach (RemoteMiniToolActionDescriptor action in
                     descriptor?.Actions ??
                     Array.Empty<RemoteMiniToolActionDescriptor>())
            {
                if (action != null &&
                    !string.IsNullOrWhiteSpace(action.Id) &&
                    !action.HideInNativeWorkspace)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
