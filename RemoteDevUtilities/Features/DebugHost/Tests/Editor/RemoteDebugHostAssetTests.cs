using System.Linq;
using NUnit.Framework;
using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.RemoteDevUtilities.DebugHost.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SAS.Utilities.RemoteDevUtilities.DebugHost.Tests
{
    public sealed class RemoteDebugHostAssetTests
    {
        private const string EnvironmentGuid = "37a03349eaf30bc4cafc546d68d1bef3";
        private const string ConsoleGuid = "6590d4eca4ab3de42a2372b05d8cc2e2";

        [Test]
        public void DebugHostPrefabs_AreResolvable()
        {
            string environmentPath = AssetPath(EnvironmentGuid);
            string consolePath = AssetPath(ConsoleGuid);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(environmentPath), Is.Not.Null, $"Missing Debug Host environment at '{environmentPath}'.");
            GameObject console = AssetDatabase.LoadAssetAtPath<GameObject>(consolePath);
            Assert.That(console, Is.Not.Null, $"Missing Developer Console at '{consolePath}'.");
            Assert.That(console.GetComponentInChildren<DeveloperConsoleBehaviour>(true), Is.Not.Null);
        }

        [Test]
        public void EnvironmentPrefab_ContainsRequiredHostInfrastructure()
        {
            string prefabPath = AssetPath(EnvironmentGuid);
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            Assert.That(root, Is.Not.Null, $"Missing environment prefab at '{prefabPath}'.");

            try
            {
                Assert.That(root.GetComponent<RemoteDebugHostEnvironmentView>(), Is.Not.Null);
                Assert.That(root.GetComponentInChildren<Camera>(true), Is.Not.Null);
                Assert.That(root.GetComponentInChildren<EventSystem>(true), Is.Not.Null);
                Assert.That(root.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component != null && component.GetType().Name == "InputSystemUIInputModule"), Is.True, "The Debug Host EventSystem must use the Input System UI module.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string AssetPath(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Assert.That(path, Is.Not.Empty, $"Asset GUID '{guid}' could not be resolved.");
            return path;
        }
    }
}
