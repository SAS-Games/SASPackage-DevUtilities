using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Presentation;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class BuildDebugUiVisibilityTests
    {
        [TestCase(BuildDebugUiVisibility.ShowWhenEnabled, false, true)]
        [TestCase(BuildDebugUiVisibility.ShowWhenEnabled, true, true)]
        [TestCase(BuildDebugUiVisibility.AlwaysHidden, false, false)]
        [TestCase(BuildDebugUiVisibility.AlwaysHidden, true, false)]
        [TestCase(BuildDebugUiVisibility.HiddenWhileEditorConnected, false, true)]
        [TestCase(BuildDebugUiVisibility.HiddenWhileEditorConnected, true, false)]
        public void Visibility_ControlsOnlyDebugUiInsideBuild(BuildDebugUiVisibility visibility, bool connected, bool expected)
        {
            RemoteDevUtilitiesPresentation.Configure(visibility);
            RemoteDevUtilitiesPresentation.SetRemoteSessionActive(connected);

            Assert.That(RemoteDevUtilitiesPresentation.ShouldAllowBuildDebugUi, Is.EqualTo(expected));
        }

        [Test]
        public void LegacyLocalAndRemoteValue_MigratesToShowWhenEnabled()
        {
            Assert.That(RemoteDevUtilitiesRuntimeSettings.NormalizeBuildUiVisibility((BuildDebugUiVisibility)2), Is.EqualTo(BuildDebugUiVisibility.ShowWhenEnabled));
        }

        [Test]
        public void RuntimeSettings_DefaultToKeepingEnabledBuildUiVisible()
        {
            var settings = ScriptableObject.CreateInstance<RemoteDevUtilitiesRuntimeSettings>();
            try
            {
                Assert.That(settings.BuildUiVisibility, Is.EqualTo(BuildDebugUiVisibility.ShowWhenEnabled));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void PackagedRuntimeSettings_KeepEnabledBuildUiVisible()
        {
            RemoteDevUtilitiesRuntimeSettings settings = Resources.Load<RemoteDevUtilitiesRuntimeSettings>("RemoteDevUtilitiesSettings");

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.BuildUiVisibility, Is.EqualTo(BuildDebugUiVisibility.ShowWhenEnabled));
        }

        [Test]
        public void RuntimeSettings_DefaultTcpPort_Is3000()
        {
            var settings = ScriptableObject.CreateInstance<RemoteDevUtilitiesRuntimeSettings>();
            try
            {
                Assert.That(settings.TcpPort, Is.EqualTo(3000));

                RemoteDevUtilitiesRuntimeSettings packaged =
                    Resources.Load<RemoteDevUtilitiesRuntimeSettings>("RemoteDevUtilitiesSettings");
                Assert.That(packaged, Is.Not.Null);
                Assert.That(packaged.TcpPort, Is.EqualTo(3000));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Connecting_ReappliesBakedBuildUiVisibility()
        {
            var settings = ScriptableObject.CreateInstance<RemoteDevUtilitiesRuntimeSettings>();
            try
            {
                var serializedSettings = new SerializedObject(settings);
                serializedSettings.FindProperty("m_BuildDebugUiVisibility").intValue = (int)BuildDebugUiVisibility.ShowWhenEnabled;
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();

                RemoteDevUtilitiesPresentation.Configure(BuildDebugUiVisibility.HiddenWhileEditorConnected);

                RuntimeDevUtilitiesAgent.ApplyPresentationPolicy(settings, true);

                Assert.That(RemoteDevUtilitiesPresentation.BuildUiVisibility, Is.EqualTo(BuildDebugUiVisibility.ShowWhenEnabled));
                Assert.That(RemoteDevUtilitiesPresentation.ShouldAllowBuildDebugUi, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [TearDown]
        public void ResetPresentationPolicy()
        {
            RemoteDevUtilitiesPresentation.Configure(BuildDebugUiVisibility.ShowWhenEnabled);
            RemoteDevUtilitiesPresentation.SetRemoteSessionActive(false);
        }
    }
}
