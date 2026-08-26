using SAS.Utilities.RemoteDevUtilities.Editor.UI;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.DebugHost;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.UI.Panels
{
    [RemoteWorkspaceHeader(400)]
    internal sealed class EditorDebugWorkspacePanel : IRemoteWorkspaceHeader
    {
        public bool Draw(RemoteDevUtilitiesClient client, bool showNativeWorkspace)
        {
            bool active = RemoteDebugHostLauncher.IsActive;
            GUILayout.Label("Debug Host", EditorStyles.miniBoldLabel);
            GUILayout.Label(active ? "Active" : "Inactive", EditorStyles.miniLabel,
                GUILayout.Width(active ? 38f : 46f));

            if (GUILayout.Button("Options", EditorStyles.toolbarDropDown, GUILayout.Width(70f)))
                ShowOptionsMenu();

            if (active)
            {
                if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(58f)))
                    RemoteDebugHostLauncher.Stop();
            }
            else
            {
                bool canLaunch = client.IsConnected && !EditorApplication.isPlaying;
                using (new EditorGUI.DisabledScope(!canLaunch))
                {
                    if (GUILayout.Button(GetLaunchContent(client), EditorStyles.toolbarButton,
                            GUILayout.Width(62f)))
                        RemoteDebugHostLauncher.Launch();
                }
            }

            return showNativeWorkspace;
        }

        private static GUIContent GetLaunchContent(RemoteDevUtilitiesClient client)
        {
            string tooltip = !client.IsConnected
                ? "Connect to a runtime Player before launching the Debug Host."
                : EditorApplication.isPlaying
                    ? "Exit the current Play Mode session before launching the isolated Debug Host."
                    : "Launch the isolated Play Mode Debug Host for the connected Player.";
            return new GUIContent("Launch", tooltip);
        }

        private static void ShowOptionsMenu()
        {
            RemoteDebugHostSettings settings = RemoteDebugHostSettings.instance;
            var menu = new GenericMenu();
            menu.AddItem(
                new GUIContent("Include Developer Console UI"),
                settings.IncludeDeveloperConsoleUi,
                () => settings.SetIncludeDeveloperConsoleUi(!settings.IncludeDeveloperConsoleUi));
            menu.AddItem(
                new GUIContent("Launch Debug Host on Player Connect"),
                settings.LaunchDebugHostOnPlayerConnect,
                () => settings.SetLaunchDebugHostOnPlayerConnect(!settings.LaunchDebugHostOnPlayerConnect));
            menu.ShowAsContext();
        }
    }
}
