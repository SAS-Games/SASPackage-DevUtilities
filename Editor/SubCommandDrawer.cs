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
        float height = 0f;
        float line = EditorGUIUtility.singleLineHeight;
        float spacing = 2f;

        var nameProp = property.FindPropertyRelative("Name");
        var helpProp = property.FindPropertyRelative("HelpText");
        var presetsProp = property.FindPropertyRelative("Presets");
        var methodProp = property.FindPropertyRelative("MethodName");

        height += line + spacing; 
        height += line + spacing; 
        height += line + spacing; 

        height += EditorGUI.GetPropertyHeight(presetsProp, true) + spacing;

        return height;
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
        float h = EditorGUI.GetPropertyHeight(prop, true);

        EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, h), prop, new GUIContent(label), true);

        y += h + 2;
    }


    private void DrawMethodPopup(ref float y, Rect pos, SerializedProperty property, SerializedProperty methodProp)
    {
        var target = property.serializedObject.targetObject;
        var type = target.GetType();

        var methods = GetValidMethods(type);
        var validNames = methods.Select(m => m.Name).ToArray();

        bool isValid = IsMethodValid(type, methodProp.stringValue);

        int currentIndex = Mathf.Max(-1, Array.IndexOf(validNames, methodProp.stringValue));

        var prevColor = GUI.color;
        if (!isValid && !string.IsNullOrEmpty(methodProp.stringValue))
            GUI.color = Color.red;

        EditorGUI.BeginChangeCheck();

        int newIndex = EditorGUI.Popup(new Rect(pos.x, y, pos.width, EditorGUIUtility.singleLineHeight), "Method", currentIndex, validNames);

        if (EditorGUI.EndChangeCheck() && newIndex != -1)
        {
            methodProp.stringValue = validNames[newIndex];
        }

        GUI.color = prevColor;
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

    private static bool IsMethodValid(Type type, string methodName)
    {
        if (string.IsNullOrEmpty(methodName))
            return false;

        return type.GetMethods(FLAGS).Any(m =>
            m.Name == methodName &&
            m.ReturnType == typeof(bool) &&
            m.GetParameters().Length == 1 &&
            m.GetParameters()[0].ParameterType == typeof(string[]) &&
            !m.IsGenericMethod &&
            !m.IsAbstract);
    }

}
