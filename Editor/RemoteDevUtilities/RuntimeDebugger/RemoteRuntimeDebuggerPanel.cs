using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeDebugger
{
    internal sealed class RemoteRuntimeDebuggerPanel
    {
        private readonly RemoteHierarchyView _hierarchy = new();
        private readonly RemoteInspectorView _inspector = new();

        public void Draw(
            RemoteRuntimeDebuggerClient client,
            bool connected,
            Rect windowRect)
        {
            if (!connected)
            {
                EditorGUILayout.HelpBox(
                    "Connect to a runtime Player to inspect its hierarchy and shader values.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox,
                GUILayout.Width(Mathf.Max(250f, windowRect.width * 0.36f)));
            _hierarchy.Draw(client);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _inspector.Draw(client);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
    }
}
