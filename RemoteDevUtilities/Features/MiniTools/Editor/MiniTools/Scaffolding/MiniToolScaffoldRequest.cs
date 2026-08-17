using System;
using System.Collections.Generic;
using System.IO;
using HP.Utilities.RemoteDevUtilities.Protocol.Commands;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.MiniTools.Scaffolding
{
    [Serializable]
    internal sealed class MiniToolScaffoldRequest
    {
        public string ToolName = "New Mini Tool";
        public string Namespace = "Game.MiniTools";
        public string Description = string.Empty;
        public string OutputFolder = "Assets";
        public bool CreateSubfolder = true;
        public bool CreateCommand = true;
        public float UpdateInterval = 0.5f;
        public bool VisibleByDefault = true;
        public RemoteCommandRouting CommandRouting = RemoteCommandRouting.ControlEditorToolOnly;

        internal bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(ToolName))
            {
                error = "Tool Name is required.";
                return false;
            }

            if (!MiniToolScaffoldNaming.IsValidNamespace(Namespace))
            {
                error = "Namespace must contain valid dot-separated C# identifiers.";
                return false;
            }

            string folder = MiniToolScaffoldNaming.NormalizeAssetPath(OutputFolder);
            if (!folder.Equals("Assets", StringComparison.OrdinalIgnoreCase) && !folder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                error = "Output Folder must be inside the project's Assets folder.";
                return false;
            }

            if (MiniToolScaffoldNaming.IsEditorOnlyPath(folder))
            {
                error = "Mini-tool runtime scripts cannot be generated inside an Editor folder.";
                return false;
            }

            if (UpdateInterval < 0.1f || float.IsNaN(UpdateInterval) || float.IsInfinity(UpdateInterval))
            {
                error = "Update Interval must be at least 0.1 seconds.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    internal sealed class MiniToolScaffoldState
    {
        public MiniToolScaffoldRequest Request;
        public string ClassName;
        public string TargetFolder;
        public string SnapshotScriptPath;
        public string CollectorScriptPath;
        public string SnapshotProviderScriptPath;
        public string DataProviderScriptPath;
        public string ViewScriptPath;
        public string LocalControllerScriptPath;
        public string CommandScriptPath;
        public string PrefabPath;
        public string CommandAssetPath;
        public string DefinitionPath;
    }

    internal static class MiniToolScaffoldNaming
    {
        private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        };

        internal static string ToIdentifier(string value)
        {
            var builder = new System.Text.StringBuilder();
            bool capitalize = true;
            foreach (char character in value ?? string.Empty)
            {
                if (!char.IsLetterOrDigit(character) && character != '_')
                {
                    capitalize = true;
                    continue;
                }

                char output = capitalize ? char.ToUpperInvariant(character) : character;
                if (builder.Length == 0 && char.IsDigit(output))
                    builder.Append('_');
                builder.Append(output);
                capitalize = false;
            }

            string identifier = builder.Length == 0 ? "NewMiniTool" : builder.ToString();
            return Keywords.Contains(identifier) ? "_" + identifier : identifier;
        }

        internal static string ToSlug(string value)
        {
            var builder = new System.Text.StringBuilder();
            foreach (char character in value?.Trim().ToLowerInvariant() ?? string.Empty)
            {
                if (char.IsLetterOrDigit(character))
                    builder.Append(character);
                else if (builder.Length > 0 && builder[builder.Length - 1] != '-')
                    builder.Append('-');
            }

            return builder.ToString().Trim('-');
        }

        internal static bool IsValidNamespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string[] segments = value.Trim().Split('.');
            foreach (string segment in segments)
            {
                if (!IsValidIdentifier(segment))
                    return false;
            }

            return true;
        }

        internal static bool IsEditorOnlyPath(string assetPath)
        {
            string[] segments = NormalizeAssetPath(assetPath).Split('/');
            foreach (string segment in segments)
            {
                if (string.Equals(segment, "Editor", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        internal static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        internal static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, NormalizeAssetPath(assetPath)));
        }

        private static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || Keywords.Contains(value))
                return false;
            if (!char.IsLetter(value[0]) && value[0] != '_')
                return false;

            for (int i = 1; i < value.Length; i++)
            {
                if (!char.IsLetterOrDigit(value[i]) && value[i] != '_')
                    return false;
            }

            return true;
        }
    }
}
