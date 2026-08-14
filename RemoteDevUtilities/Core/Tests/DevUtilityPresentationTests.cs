using NUnit.Framework;
using SAS.Utilities.Presentation;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class DevUtilityPresentationTests
    {
        [Test]
        public void RequestedVisibility_ControlsPresentationRoot()
        {
            var root = new GameObject("Presentation");
            DevUtilityPresentation presentation = root.AddComponent<DevUtilityPresentation>();

            try
            {
                presentation.SetRequestedVisible(false);
                Assert.That(root.activeSelf, Is.False);

                presentation.SetRequestedVisible(true);
                Assert.That(root.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Presentation_PreservesRequestedVisibilityAcrossSuppression()
        {
            const string suppressionSource = "Tests.DevUtilityPresentation";
            DevUtilityPresentationRegistry.SetSuppressed(suppressionSource, false);

            var root = new GameObject("Presentation");
            DevUtilityPresentation presentation = root.AddComponent<DevUtilityPresentation>();

            try
            {
                presentation.SetRequestedVisible(true);
                DevUtilityPresentationRegistry.SetSuppressed(suppressionSource, true);
                Assert.That(root.activeSelf, Is.False);

                presentation.SetRequestedVisible(false);
                DevUtilityPresentationRegistry.SetSuppressed(suppressionSource, false);
                Assert.That(root.activeSelf, Is.False, "A hidden request made during suppression must remain hidden.");

                presentation.SetRequestedVisible(true);
                DevUtilityPresentationRegistry.SetSuppressed(suppressionSource, true);
                DevUtilityPresentationRegistry.SetSuppressed(suppressionSource, false);
                Assert.That(root.activeSelf, Is.True, "A visible request must be restored after suppression ends.");
            }
            finally
            {
                DevUtilityPresentationRegistry.SetSuppressed(suppressionSource, false);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Presentation_RemainsSuppressedUntilEverySourceReleases()
        {
            const string sourceA = "Tests.DevUtilityPresentation.SourceA";
            const string sourceB = "Tests.DevUtilityPresentation.SourceB";
            DevUtilityPresentationRegistry.SetSuppressed(sourceA, false);
            DevUtilityPresentationRegistry.SetSuppressed(sourceB, false);

            var root = new GameObject("Presentation");
            DevUtilityPresentation presentation = root.AddComponent<DevUtilityPresentation>();

            try
            {
                presentation.SetRequestedVisible(true);
                DevUtilityPresentationRegistry.SetSuppressed(sourceA, true);
                DevUtilityPresentationRegistry.SetSuppressed(sourceB, true);
                DevUtilityPresentationRegistry.SetSuppressed(sourceA, false);
                Assert.That(root.activeSelf, Is.False);

                DevUtilityPresentationRegistry.SetSuppressed(sourceB, false);
                Assert.That(root.activeSelf, Is.True);
            }
            finally
            {
                DevUtilityPresentationRegistry.SetSuppressed(sourceA, false);
                DevUtilityPresentationRegistry.SetSuppressed(sourceB, false);
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("Runtime/MiniTools/Animator/Assets/AnimatorStats.prefab")]
        [TestCase("Runtime/MiniTools/ParticleSystem/Assets/ParticleStats.prefab")]
        [TestCase("Runtime/MiniTools/GameInfo/Assets/GameInfo.prefab")]
        [TestCase("Runtime/MiniTools/GraphicsInfo/Assets/GraphicsInfo.prefab")]
        [TestCase("Runtime/MiniTools/FPS/Assets/FPS.prefab")]
        [TestCase("Runtime/MiniTools/InputLatencyProfiler/UI/InputLatencyGraphUI.prefab")]
        [TestCase("Runtime/MiniTools/InputVisualizer/Assets/GamepadVisualizer.prefab")]
        [TestCase("Runtime/MiniTools/InputVisualizer/Assets/MouseVisualizer.prefab")]
        [TestCase("Runtime/LoggingSystem/Objects/OnScreenLoggingUI.prefab")]
        [TestCase("Runtime/MiniTools/FrameStepper/Assets/FrameStepper.prefab")]
        public void BuiltInPresentationPrefab_HasCorePresentationComponent(string relativePath)
        {
            string prefabPath = PackagePath(relativePath);
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            Assert.That(root, Is.Not.Null, $"Missing presentation prefab at '{prefabPath}'.");

            try
            {
                Assert.That(root.GetComponent<DevUtilityPresentation>(), Is.Not.Null, $"{relativePath} must own generic presentation state.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string PackagePath(string relativePath)
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(DevUtilityPresentation).Assembly);
            Assert.That(package?.assetPath, Is.Not.Null.And.Not.Empty);
            string root = package.assetPath.TrimEnd('/');
            return root + "/" + relativePath;
        }
    }
}
