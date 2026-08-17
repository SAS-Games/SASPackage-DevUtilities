using System;
using System.Collections.Generic;
using HP.Utilities.RemoteDevUtilities.MiniTools;
using UnityEditor;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry
{
    /// <summary>
    /// Resolves a stable provider script GUID to the current namespace and
    /// assembly-qualified runtime type identity.
    /// </summary>
    internal static class MiniToolProviderReferenceResolver
    {
        internal static bool TrySynchronize(MiniToolDefinition definition, string definitionPath, out string error, out string warning)
        {
            error = string.Empty;
            warning = string.Empty;
            if (definition == null)
            {
                error = "Mini Tool Definition is missing.";
                return false;
            }

            var serialized = new SerializedObject(definition);
            SerializedProperty guidProperty = serialized.FindProperty("_providerScriptGuid");
            SerializedProperty typeProperty = serialized.FindProperty("_providerTypeName");
            if (guidProperty == null || typeProperty == null)
            {
                error = "Data Provider reference fields are missing from the definition.";
                return false;
            }

            string scriptGuid = guidProperty.stringValue?.Trim();
            string storedTypeName = typeProperty.stringValue?.Trim();
            MonoScript providerScript;
            if (string.IsNullOrWhiteSpace(scriptGuid))
            {
                if (!TryFindLegacyProviderScript(storedTypeName, out providerScript, out error))
                {
                    if (!TryResolveAssemblyProvider(storedTypeName, out Type assemblyProviderType))
                        return false;

                    string assemblyTypeName = $"{assemblyProviderType.FullName}, " + assemblyProviderType.Assembly.GetName().Name;
                    if (!string.Equals(typeProperty.stringValue, assemblyTypeName, StringComparison.Ordinal))
                    {
                        typeProperty.stringValue = assemblyTypeName;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(definition);
                        if (!string.IsNullOrWhiteSpace(definitionPath) && AssetDatabase.IsOpenForEdit(definitionPath, StatusQueryOptions.UseCachedIfPossible))
                            AssetDatabase.SaveAssetIfDirty(definition);
                    }

                    error = string.Empty;
                    warning = "The data provider is compiled into an assembly and has no MonoScript GUID. " +
                              "Its stored type identity cannot be refreshed automatically after a rename.";
                    return true;
                }

                string providerPath = AssetDatabase.GetAssetPath(providerScript);
                scriptGuid = AssetDatabase.AssetPathToGUID(providerPath);
                if (string.IsNullOrWhiteSpace(scriptGuid))
                {
                    error = "Data Provider script does not have a valid asset GUID.";
                    return false;
                }
            }
            else
            {
                string providerPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
                providerScript = string.IsNullOrWhiteSpace(providerPath) ? null : AssetDatabase.LoadAssetAtPath<MonoScript>(providerPath);
                if (providerScript == null)
                {
                    error = $"Data Provider script GUID '{scriptGuid}' could not be resolved. " + "The script or its .meta file may be missing.";
                    return false;
                }
            }

            Type providerType = providerScript.GetClass();
            if (providerType == null)
            {
                error = $"Data Provider script '{providerScript.name}' does not currently expose a compiled type. " + "Check the first Unity compilation error.";
                return false;
            }

            if (!typeof(IMiniToolDataProvider).IsAssignableFrom(providerType))
            {
                error = $"'{providerType.FullName}' does not implement {nameof(IMiniToolDataProvider)}.";
                return false;
            }

            string currentTypeName = $"{providerType.FullName}, " + providerType.Assembly.GetName().Name;
            bool changed = !string.Equals(guidProperty.stringValue, scriptGuid, StringComparison.Ordinal) || !string.Equals(typeProperty.stringValue, currentTypeName, StringComparison.Ordinal);
            if (!changed)
                return true;

            guidProperty.stringValue = scriptGuid;
            typeProperty.stringValue = currentTypeName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            if (string.IsNullOrWhiteSpace(definitionPath))
                return true;

            if (!AssetDatabase.IsOpenForEdit(definitionPath, StatusQueryOptions.UseCachedIfPossible))
            {
                warning = "Data Provider identity was refreshed in memory, but the definition is read-only. " + "Check out the definition asset to persist the refreshed namespace and assembly.";
                return true;
            }

            AssetDatabase.SaveAssetIfDirty(definition);
            return true;
        }

        private static bool TryResolveAssemblyProvider(string storedTypeName, out Type providerType)
        {
            providerType = null;
            if (string.IsNullOrWhiteSpace(storedTypeName))
                return false;

            try
            {
                providerType = Type.GetType(storedTypeName, false);
            }
            catch (Exception)
            {
                return false;
            }

            return providerType != null && typeof(IMiniToolDataProvider).IsAssignableFrom(providerType);
        }

        private static bool TryFindLegacyProviderScript(string storedTypeName, out MonoScript providerScript, out string error)
        {
            providerScript = null;
            error = string.Empty;
            Type storedType = null;
            if (!string.IsNullOrWhiteSpace(storedTypeName))
            {
                try
                {
                    storedType = Type.GetType(storedTypeName, false);
                }
                catch (Exception)
                {
                    // A malformed or stale legacy identity can still be
                    // migrated safely by a unique provider class name.
                }
            }

            if (storedType != null)
            {
                foreach (MonoScript script in MonoImporter.GetAllRuntimeMonoScripts())
                {
                    if (script != null && script.GetClass() == storedType)
                    {
                        providerScript = script;
                        return true;
                    }
                }
            }

            string className = GetClassName(storedTypeName);
            if (string.IsNullOrWhiteSpace(className))
            {
                error = "Data Provider script is missing. Assign a provider script to the definition.";
                return false;
            }

            var matches = new List<MonoScript>();
            foreach (MonoScript script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                Type candidate = script?.GetClass();
                if (candidate == null || !string.Equals(candidate.Name, className, StringComparison.Ordinal) || !typeof(IMiniToolDataProvider).IsAssignableFrom(candidate))
                {
                    continue;
                }

                matches.Add(script);
            }

            if (matches.Count == 1)
            {
                providerScript = matches[0];
                return true;
            }

            error = matches.Count == 0 ? $"Data Provider '{className}' could not be found. Assign its script to the definition." : $"More than one Data Provider is named '{className}'. Assign the intended script to the definition.";
            return false;
        }

        private static string GetClassName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return string.Empty;

            int assemblySeparator = typeName.IndexOf(',');
            string fullName = (assemblySeparator < 0 ? typeName : typeName.Substring(0, assemblySeparator)).Trim();
            int nestedSeparator = fullName.LastIndexOf('+');
            int namespaceSeparator = fullName.LastIndexOf('.');
            int separator = Math.Max(nestedSeparator, namespaceSeparator);
            return separator < 0 ? fullName : fullName.Substring(separator + 1);
        }
    }
}
