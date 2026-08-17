using HP.Utilities.RemoteDevUtilities.Editor.Client;
using HP.Utilities.RemoteDevUtilities.Editor.DebugHost;
using HP.Utilities.RemoteDevUtilities.Editor.UI;
using UnityEditor;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.UI.Panels
{
    [RemoteWorkspaceHeader(400)]
    internal sealed class EditorDebugWorkspacePanel : IRemoteWorkspaceHeader
    {
        private const string ExpandedPreferenceKey = "RemoteDevUtilities.DebugHostPanelExpanded";

        private bool _expanded = EditorPrefs.GetBool(ExpandedPreferenceKey, true);

        public bool Draw(RemoteDevUtilitiesClient client, bool showNativeWorkspace)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            bool active = RemoteDebugHostLauncher.IsActive;
            bool expanded = EditorGUILayout.Foldout(_expanded, active ? "Editor Debug Workspace - Debug Host Active" : "Editor Debug Workspace", true, EditorStyles.foldoutHeader);
            if (expanded != _expanded)
            {
                _expanded = expanded;
                EditorPrefs.SetBool(ExpandedPreferenceKey, _expanded);
            }

            if (!_expanded)
            {
                EditorGUILayout.EndVertical();
                return showNativeWorkspace;
            }

            EditorGUILayout.LabelField("Use Editor-native panels, or launch Runtime Scene Inspector and mini-tool prefabs in an isolated Play Mode Host. The Developer Console UI is optional.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6f);

            RemoteDebugHostSettings settings = RemoteDebugHostSettings.instance;
            bool includeConsoleUi = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Include Developer Console UI",
                    "Instantiate the Developer Console inside the Host. Commands and Sequences remain available when this is disabled."),
                settings.IncludeDeveloperConsoleUi);
            if (includeConsoleUi != settings.IncludeDeveloperConsoleUi)
                settings.SetIncludeDeveloperConsoleUi(includeConsoleUi);

            bool launchOnConnect = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Launch Debug Host on Player Connect",
                    "Launch after a successful Player handshake. Stopping the Host suppresses relaunch until the Player disconnects."),
                settings.LaunchDebugHostOnPlayerConnect);
            if (launchOnConnect != settings.LaunchDebugHostOnPlayerConnect)
                settings.SetLaunchDebugHostOnPlayerConnect(launchOnConnect);

            EditorGUILayout.Space(4f);

            if (active)
            {
                EditorGUILayout.HelpBox("The Debug Host is active. Commands and edits target the connected Player.", MessageType.Info);
            }
            else
            {
                if (!client.IsConnected)
                {
                    EditorGUILayout.HelpBox("Connect to a runtime Player before launching the Debug Host.", MessageType.None);
                }
                else if (EditorApplication.isPlaying)
                {
                    EditorGUILayout.HelpBox("Exit the current Play Mode session before launching the isolated " + "Debug Host scene.", MessageType.None);
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (active)
            {
                if (GUILayout.Button("Stop Debug Host", GUILayout.Height(28f)))
                    RemoteDebugHostLauncher.Stop();
            }
            else
            {
                using (new EditorGUI.DisabledScope(!client.IsConnected || EditorApplication.isPlaying))
                {
                    if (GUILayout.Button("Launch Play Mode Debug Host", GUILayout.Height(28f)))
                        RemoteDebugHostLauncher.Launch();
                }
            }

            string workspaceButtonLabel = showNativeWorkspace ? "Hide Native Workspace" : "Open Native Workspace";
            if (GUILayout.Button(workspaceButtonLabel, GUILayout.Width(150f), GUILayout.Height(28f)))
            {
                showNativeWorkspace = !showNativeWorkspace;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            return showNativeWorkspace;
        }
    }
}
