using System.Linq;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.DebugHost.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using SAS.Utilities.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SAS.Utilities.RemoteDevUtilities.DebugHost.Tests
{
    public sealed class RemoteMiniToolPrefabDiscoveryTests
    {
        [TestCase("runtime.performance")]
        [TestCase("runtime.game-info")]
        [TestCase("runtime.graphics-info")]
        [TestCase("runtime.animators")]
        [TestCase("runtime.particles")]
        [TestCase("runtime.input-visualizer.gamepad")]
        [TestCase("runtime.input-visualizer.mouse")]
        public void PackageDefaults_ResolveBuiltInPrefab(string toolId)
        {
            Assert.That(MiniToolRegistry.TryGetDebugHostPrefab(toolId, out GameObject prefab), Is.True);
            Assert.That(prefab, Is.Not.Null);
        }

        [Test]
        public void DefinitionWithoutPrefab_UsesGenericDebugHostView()
        {
            RemoteMiniToolPrefabDefinition definition = RemoteMiniToolPrefabDefinitions.Discover().Single(candidate => candidate.ToolId == "runtime.rendering");

            Assert.That(definition.AssetPath, Is.Empty);
        }

        [Test]
        public void Discover_UsesDefinitionWithoutPrefabToolId()
        {
            const string prefabPath = "Assets/RemoteMiniToolDiscoveryTest.prefab";
            const string definitionPath = "Assets/RemoteMiniToolDiscoveryTest.asset";
            const string toolId = "custom.discovery.test";

            try
            {
                AssetDatabase.DeleteAsset(prefabPath);
                AssetDatabase.DeleteAsset(definitionPath);
                var parent = new GameObject("RemoteMiniToolDiscoveryParent");
                parent.AddComponent<DevUtilityPresentation>();
                var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textObject.transform.SetParent(parent.transform, false);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(parent, prefabPath);
                Assert.That(prefab, Is.Not.Null);
                Object.DestroyImmediate(parent);

                var definition = ScriptableObject.CreateInstance<MiniToolDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("_toolId").stringValue = toolId;
                serialized.FindProperty("_displayName").stringValue = "Discovery Test";
                serialized.FindProperty("_providerTypeName").stringValue = $"{typeof(DefinitionTestProvider).FullName}, " + typeof(DefinitionTestProvider).Assembly.GetName().Name;
                serialized.FindProperty("_debugHostPrefabGuid").stringValue = AssetDatabase.AssetPathToGUID(prefabPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                MiniToolRegistry.Invalidate();

                RemoteMiniToolPrefabDefinition[] definitions = RemoteMiniToolPrefabDefinitions.Discover();
                Assert.That(definitions.Any(d => d.ToolId == toolId), Is.True);
                Assert.That(definitions.First(d => d.ToolId == toolId).AssetPath, Is.EqualTo(prefabPath));
            }
            finally
            {
                AssetDatabase.DeleteAsset(definitionPath);
                AssetDatabase.DeleteAsset(prefabPath);
                MiniToolRegistry.Invalidate();
            }
        }

        private sealed class DefinitionTestProvider : MiniToolFieldDataProvider
        {
            public override RemoteMiniToolField[] CaptureFields() => System.Array.Empty<RemoteMiniToolField>();
        }
    }
}
