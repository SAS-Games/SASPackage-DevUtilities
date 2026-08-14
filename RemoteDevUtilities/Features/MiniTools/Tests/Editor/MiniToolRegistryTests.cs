using System;
using System.Linq;
using NUnit.Framework;
using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using SAS.Utilities.RemoteDevUtilities.MiniTools.Providers;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEditor;
using UnityEngine;
using RuntimeConsole = SAS.Utilities.DeveloperConsole.DeveloperConsole;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class MiniToolRegistryTests
    {
        [Test]
        public void BuiltIns_UseValidUnifiedDefinitions()
        {
            MiniToolRegistry.Invalidate();

            Assert.That(MiniToolRegistry.ValidationErrors, Is.Empty);
            Assert.That(MiniToolRegistry.ValidationWarnings, Is.Empty);
            Assert.That(MiniToolRegistry.Registrations.Select(registration => registration.Descriptor.Id), Does.Contain("runtime.performance"));
            Assert.That(MiniToolRegistry.Registrations.Select(registration => registration.Descriptor.Id), Does.Contain("runtime.game-info"));
            Assert.That(MiniToolRegistry.Registrations.Select(registration => registration.Descriptor.Id), Does.Contain("runtime.rendering"));
        }

        [Test]
        public void Definition_OwnsCommandAndDebugHostPresentation()
        {
            Assert.That(MiniToolRegistry.TryGet("runtime.game-info", out MiniToolRegistration registration), Is.True);
            Assert.That(registration.Descriptor.Command.Name, Is.EqualTo("GameInfo"));
            Assert.That(registration.LoadDebugHostPrefab(), Is.Not.Null);
        }

        [Test]
        public void BuiltIns_StoreResolvableProviderScriptGuids()
        {
            string[] toolIds =
            {
                "runtime.animators",
                "runtime.frame-stepper",
                "runtime.game-info",
                "runtime.graphics-info",
                "runtime.input-latency",
                "runtime.input-visualizer.gamepad",
                "runtime.input-visualizer.mouse",
                "runtime.particles",
                "runtime.performance",
                "runtime.rendering"
            };

            foreach (string toolId in toolIds)
            {
                Assert.That(MiniToolRegistry.TryGet(toolId, out MiniToolRegistration registration), Is.True, toolId);

                var serialized = new SerializedObject(registration.Definition);
                string providerScriptGuid = serialized.FindProperty("_providerScriptGuid").stringValue;
                Assert.That(providerScriptGuid, Is.Not.Empty, toolId);

                string providerScriptPath = AssetDatabase.GUIDToAssetPath(providerScriptGuid);
                MonoScript providerScript = AssetDatabase.LoadAssetAtPath<MonoScript>(providerScriptPath);
                Assert.That(providerScript, Is.Not.Null, toolId);
                Assert.That(registration.Definition.TryGetProviderType(out Type providerType), Is.True, toolId);
                Assert.That(providerScript.GetClass(), Is.EqualTo(providerType), toolId);
            }
        }

        [TestCase("runtime.input-visualizer.gamepad", "InputVisualizer.Gamepad")]
        [TestCase("runtime.input-visualizer.mouse", "InputVisualizer.Mouse")]
        public void InputVisualizers_AreDebugHostOnlyStreamingTools(string toolId, string commandName)
        {
            Assert.That(MiniToolRegistry.TryGet(toolId, out MiniToolRegistration registration), Is.True);
            Assert.That(registration.Descriptor.Command.Name, Is.EqualTo(commandName));
            Assert.That(registration.Descriptor.Capabilities, Is.EqualTo(RemoteMiniToolCapabilities.TypedDebugHostSnapshot | RemoteMiniToolCapabilities.EventStream));
            Assert.That(registration.LoadDebugHostPrefab(), Is.Not.Null);
        }

        [Test]
        public void RuntimeRegistry_CreatesDefinitionProviders()
        {
            MiniToolRuntimeRegistry.SetEditorDefinitions(MiniToolRegistry.GetDefinitions());
            var registrations = MiniToolRuntimeRegistry.CreateRegistrations();
            try
            {
                Assert.That(registrations.Any(registration => registration.Descriptor.Id == "runtime.game-info"), Is.True);
            }
            finally
            {
                foreach (MiniToolProviderRegistration registration in registrations)
                    registration.Dispose();
            }
        }

        [Test]
        public void RuntimeRegistry_RegistersDefinitionCommandsOnce()
        {
            MiniToolRuntimeRegistry.SetEditorDefinitions(MiniToolRegistry.GetDefinitions());
            var console = new RuntimeConsole("/", Array.Empty<IConsoleCommand>());

            MiniToolRuntimeRegistry.RegisterCommands(console);
            int firstCount = console.ConsoleCommands.Count;
            MiniToolRuntimeRegistry.RegisterCommands(console);

            Assert.That(console.ConsoleCommands.Select(command => command.Name), Does.Contain("GameInfo"));
            Assert.That(console.ConsoleCommands.Count, Is.EqualTo(firstCount));
        }

        [Test]
        public void Definition_RefreshesProviderIdentityFromScriptGuid()
        {
            var definition = ScriptableObject.CreateInstance<MiniToolDefinition>();
            try
            {
                MonoScript providerScript = FindScript(typeof(RuntimeRenderingMiniToolProvider));
                Assert.That(providerScript, Is.Not.Null);

                var serialized = new SerializedObject(definition);
                serialized.FindProperty("_providerScriptGuid").stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(providerScript));
                serialized.FindProperty("_providerTypeName").stringValue = "Previous.Namespace.RuntimeRenderingMiniToolProvider, " + "Previous.Project.Assembly";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(MiniToolProviderReferenceResolver.TrySynchronize(definition, string.Empty, out string error, out string warning), Is.True);
                Assert.That(error, Is.Empty);
                Assert.That(warning, Is.Empty);
                Assert.That(definition.TryGetProviderType(out Type providerType), Is.True);
                Assert.That(providerType, Is.EqualTo(typeof(RuntimeRenderingMiniToolProvider)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void LegacyDefinition_MigratesGuidAfterNamespaceChange()
        {
            var definition = ScriptableObject.CreateInstance<MiniToolDefinition>();
            try
            {
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("_providerTypeName").stringValue = "Previous.Namespace.RuntimeRenderingMiniToolProvider, " + "Previous.Project.Assembly";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(MiniToolProviderReferenceResolver.TrySynchronize(definition, string.Empty, out string error, out string warning), Is.True);
                Assert.That(error, Is.Empty);
                Assert.That(warning, Is.Empty);

                serialized.Update();
                Assert.That(serialized.FindProperty("_providerScriptGuid").stringValue, Is.Not.Empty);
                Assert.That(definition.TryGetProviderType(out Type providerType), Is.True);
                Assert.That(providerType, Is.EqualTo(typeof(RuntimeRenderingMiniToolProvider)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

#if ENABLE_DEBUG
        [Test]
        public void BuildProcessor_BakesProjectDefinitionAndRestoresSettings()
        {
            const string definitionPath =
                "Assets/RemoteMiniToolBuildIntegrationTest.asset";
            UnityEngine.Object[] previous =
                PlayerSettings.GetPreloadedAssets();
            var processor = new RemoteDevUtilitiesBuildProcessor();

            try
            {
                AssetDatabase.DeleteAsset(definitionPath);
                var definition =
                    ScriptableObject.CreateInstance<MiniToolDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("_toolId").stringValue =
                    "tests.project-build-integration";
                serialized.FindProperty("_displayName").stringValue =
                    "Project Build Integration";
                serialized.FindProperty("_providerTypeName").stringValue =
                    $"{typeof(RuntimeRenderingMiniToolProvider).FullName}, " +
                    typeof(RuntimeRenderingMiniToolProvider)
                        .Assembly.GetName().Name;
                MonoScript providerScript =
                    FindScript(
                        typeof(RuntimeRenderingMiniToolProvider));
                serialized.FindProperty("_providerScriptGuid")
                    .stringValue =
                    AssetDatabase.AssetPathToGUID(
                        AssetDatabase.GetAssetPath(providerScript));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                MiniToolRegistry.Invalidate();

                Assert.That(
                    MiniToolRegistry.TryGet(
                        "tests.project-build-integration",
                        out MiniToolRegistration registration),
                    Is.True);
                Assert.That(registration.IsProjectOwned, Is.True);

                processor.OnPreprocessBuild(null);

                Assert.That(
                    PlayerSettings.GetPreloadedAssets(),
                    Does.Contain(definition));
                Assert.That(
                    PlayerSettings.GetPreloadedAssets(),
                    Has.Some.TypeOf<
                        RemoteDevUtilitiesRuntimeSettings>());
            }
            finally
            {
                try
                {
                    processor.OnPostprocessBuild(null);
                }
                finally
                {
                    AssetDatabase.DeleteAsset(definitionPath);
                    MiniToolRegistry.Invalidate();
                }
            }

            Assert.That(
                PlayerSettings.GetPreloadedAssets(),
                Is.EqualTo(previous));
        }
#endif

        private static MonoScript FindScript(Type providerType)
        {
            foreach (MonoScript script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                if (script != null && script.GetClass() == providerType)
                {
                    return script;
                }
            }

            return null;
        }
    }
}
