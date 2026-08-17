using SAS.Utilities.DeveloperConsole;
using UnityEditor;

[CustomEditor(typeof(ConsoleCommand), true)]
public class ConsoleCommandEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var command = (ConsoleCommand)target;
        bool hasName = !string.IsNullOrEmpty(command.Name);

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject(command), typeof(MonoScript), false);
        EditorGUI.EndDisabledGroup();

        var nameProp = serializedObject.FindProperty("m_CommandName");
        if (nameProp != null)
            EditorGUILayout.PropertyField(nameProp);

        if (!hasName)
        {
            EditorGUILayout.HelpBox("This command is not supported in the current configuration.", MessageType.Info);
        }

        EditorGUI.BeginDisabledGroup(!hasName);
        DrawPropertiesExcluding(serializedObject, "m_Script", "m_CommandName");
        EditorGUI.EndDisabledGroup();

        serializedObject.ApplyModifiedProperties();
    }
}