using System.IO;
using UnityEditor;
using UnityEngine;

namespace SAS.BuildValidation
{
    public static class BuildValidationSettingsProvider
    {
        private const string RootFolder = "Assets/Settings";
        private const string EditorFolder = RootFolder + "/Editor";
        private const string ValidationFolder = EditorFolder + "/BuildValidation";
        private const string AssetPath = ValidationFolder + "/BuildValidationSettings.asset";
        private static BuildValidationSettings _cachedSettings;

        public static BuildValidationSettings GetOrCreateSettings()
        {
            if (_cachedSettings != null)
                return _cachedSettings;

            _cachedSettings = AssetDatabase.LoadAssetAtPath<BuildValidationSettings>(AssetPath);

            if (_cachedSettings != null) return _cachedSettings;

            EnsureFolderExists(RootFolder);
            EnsureFolderExists(EditorFolder);
            EnsureFolderExists(ValidationFolder);

            _cachedSettings = ScriptableObject.CreateInstance<BuildValidationSettings>();

            AssetDatabase.CreateAsset(_cachedSettings, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return _cachedSettings;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string parentFolder = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string folderName = Path.GetFileName(folderPath);

            AssetDatabase.CreateFolder(parentFolder, folderName);
        }
    }
}