using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole.Editor
{
    public class DebugSettingsWindow : EditorWindow
    {
        [MenuItem("Tools/DevUtilities/Debug Settings")]
        public static void ShowWindow()
        {
            GetWindow<DebugSettingsWindow>("Debug Settings");
        }

        private void OnGUI()
        {
            var settings = DebugEditorSettings.instance;

            EditorGUI.BeginChangeCheck();

            settings.pauseOnEnable = EditorGUILayout.Toggle("Pause On Enable", settings.pauseOnEnable);

            settings.logLevel = (LogLevel)EditorGUILayout.EnumFlagsField("Log Level", settings.logLevel);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Allowed Tags");

            for (int i = 0; i < settings.allowedTags.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                settings.allowedTags[i] = EditorGUILayout.TextField(settings.allowedTags[i]);

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    settings.allowedTags.RemoveAt(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Tag"))
                settings.allowedTags.Add("");

            if (EditorGUI.EndChangeCheck())
            {
                settings.SaveSettings();

                if (Application.isPlaying)
                {
                    DebugSettings.ApplyFromEditor();
                }
            }
        }
    }
}