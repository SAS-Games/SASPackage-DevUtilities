using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SAS.Utilities.DeveloperConsole.Editor
{
    public class RoslynDebugUpdater
    {
        private const string LoggingSystem = "SAS";
        [MenuItem("Tools/DevUtilities/Roslyn/Use " + LoggingSystem + " Debug")]
        public static void Run()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "Switch to SAS Debug",
                "This will add 'using Debug = SAS.Debug;' to scripts using Debug.Log.\n\n" +
                "After this:\n" +
                "- Debug.Log will use SAS.Debug instead of UnityEngine.Debug\n" +
                "- Logging behavior may change (custom filters, formatting, etc.)\n" +
                "- Mixed usage with UnityEngine.Debug may still exist\n\n" +
                "Do you want to continue?",
                "Yes, Use SAS Debug",
                "Cancel"
            );

            if (!confirm)
            {
                UnityEngine.Debug.Log("Operation cancelled by user.");
                return;
            }
            
            string[] ignoredFolders = { "Plugins", "ThirdParty" };

            var files = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var segments = path.Split(Path.DirectorySeparatorChar);
                    return !segments.Any(folder => ignoredFolders.Contains(folder));
                })
                .ToArray();

            int updated = 0;

            foreach (var file in files)
            {
                string code = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetCompilationUnitRoot();

                bool usesDebug = root.DescendantNodes()
                    .OfType<MemberAccessExpressionSyntax>()
                    .Any(ma =>
                    {
                        string expr = ma.Expression.ToString();
                        string method = ma.Name.Identifier.Text;

                        return expr == "Debug" && method.StartsWith("Log");
                    });

                if (!usesDebug) 
                    continue;

                bool usesUnityDebug = root.DescendantNodes()
                    .OfType<MemberAccessExpressionSyntax>()
                    .Any(ma =>
                        ma.Expression.ToString() == "UnityEngine.Debug" &&
                        ma.Name.Identifier.Text.StartsWith("Log"));

                if (usesUnityDebug)
                {
                    UnityEngine.Debug.LogWarning($"Mixed Debug usage in: {file}");
                }

                var existingAlias = root.Usings.FirstOrDefault(u =>
                    u.Alias?.Name.ToString() == "Debug");

                if (existingAlias != null)
                {
                    // Already correct → skip
                    if (existingAlias.Name.ToString() == $"{LoggingSystem}.Debug")
                        continue;

                    // Replace incorrect alias
                    var newAlias = SyntaxFactory.ParseCompilationUnit($"using Debug = {LoggingSystem}.Debug;\n")
                        .Usings[0];

                    var newRootReplace = root.ReplaceNode(existingAlias, newAlias);

                    File.WriteAllText(file, newRootReplace.ToFullString());
                    updated++;
                    continue;
                }

                var alias = SyntaxFactory.ParseCompilationUnit($"using Debug = {LoggingSystem}.Debug;\n")
                    .Usings[0];

                var lastUsing = root.Usings.LastOrDefault();
                CompilationUnitSyntax newRoot;

                if (lastUsing != null)
                    newRoot = root.InsertNodesAfter(lastUsing, new[] { alias });
                else
                    newRoot = root.AddUsings(alias);

                File.WriteAllText(file, newRoot.ToFullString());
                updated++;
            }

            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"<color=green>Roslyn updated {updated} files.</color>");
        }
        
        
        [MenuItem("Tools/DevUtilities/Roslyn/Use Unity Debug")]
        public static void UseUnityDebug()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "Switch to Unity Debug",
                $"This will remove 'using Debug = {LoggingSystem}.Debug;' from all scripts.\n\n" +
                "After this:\n" +
                "- Debug.Log will resolve to UnityEngine.Debug\n" +
                "- Any SAS.Debug specific behavior will be lost\n" +
                "- Logging behavior may change across the project\n\n" +
                "Do you want to continue?",
                "Yes, Remove Alias",
                "Cancel"
            );

            if (!confirm)
            {
                UnityEngine.Debug.Log("Operation cancelled by user.");
                return;
            }

            string[] ignoredFolders = { "Plugins", "ThirdParty" };

            var files = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var segments = path.Split(Path.DirectorySeparatorChar);
                    return !segments.Any(folder => ignoredFolders.Contains(folder));
                })
                .ToArray();

            int updated = 0;

            foreach (var file in files)
            {
                string code = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetCompilationUnitRoot();

                var alias = root.Usings.FirstOrDefault(u =>
                    u.Alias?.Name.ToString() == "Debug" &&
                    u.Name.ToString() == $"{LoggingSystem}.Debug");

                if (alias == null)
                    continue;

                var newRoot = root.RemoveNode(alias, SyntaxRemoveOptions.KeepNoTrivia);

                var newText = newRoot.ToFullString();

                if (newText != code)
                {
                    File.WriteAllText(file, newText);
                    updated++;
                }
            }

            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"<color=yellow>Removed Debug alias from {updated} files.</color>");
        }
    }
}