using System;
using System.Linq;
using System.Reflection;
using SAS.Utilities.DeveloperConsole;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CompositeConsoleCommand.SubCommand), true)]
public class SubCommandDrawer : PropertyDrawer
{
    private const BindingFlags FLAGS =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 5 + 6;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var line = EditorGUIUtility.singleLineHeight;
        var y = position.y;

        var nameProp = property.FindPropertyRelative("Name");
        var helpProp = property.FindPropertyRelative("HelpText");
        var presetsProp = property.FindPropertyRelative("Presets");
        var methodProp = property.FindPropertyRelative("MethodName");

        Draw(ref y, position, nameProp, "Name");
        DrawMethodPopup(ref y, position, property, methodProp);
        Draw(ref y, position, helpProp, "Help");
        Draw(ref y, position, presetsProp, "Presets");


        EditorGUI.EndProperty();
    }

    private void Draw(ref float y, Rect pos, SerializedProperty prop, string label)
    {
        EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, EditorGUIUtility.singleLineHeight), prop, new GUIContent(label));
        y += EditorGUIUtility.singleLineHeight + 2;
    }

    private void DrawMethodPopup(ref float y, Rect pos, SerializedProperty property, SerializedProperty methodProp)
    {
        var target = property.serializedObject.targetObject;
        var methods = GetValidMethods(target.GetType());

        var names = methods.Select(m => m.Name).ToArray();
        var currentIndex = Mathf.Max(0, Array.IndexOf(names, methodProp.stringValue));

        EditorGUI.BeginChangeCheck();

        var newIndex = EditorGUI.Popup(new Rect(pos.x, y, pos.width, EditorGUIUtility.singleLineHeight), "Method", currentIndex, names);

        if (EditorGUI.EndChangeCheck())
        {
            methodProp.stringValue = names.Length > 0 ? names[newIndex] : string.Empty;
        }

        y += EditorGUIUtility.singleLineHeight + 2;
    }

    private static MethodInfo[] GetValidMethods(Type type)
    {
        return type.GetMethods(FLAGS)
            .Where(m =>
                m.ReturnType == typeof(bool) &&
                m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType == typeof(string[]) &&
                !m.IsGenericMethod &&
                !m.IsAbstract)
            .OrderBy(m => m.Name)
            .ToArray();
    }
}
