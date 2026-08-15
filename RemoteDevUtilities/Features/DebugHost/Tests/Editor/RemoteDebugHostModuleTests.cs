using System.Collections;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.DebugHost;
using SAS.Utilities.RemoteDevUtilities.Editor.UI;
using SAS.Utilities.RemoteDevUtilities.Editor.UI.Panels;
using UnityEditor;

namespace SAS.Utilities.RemoteDevUtilities.DebugHost.Tests
{
    public sealed class RemoteDebugHostModuleTests
    {
        [Test]
        public void Module_ContributesWorkspaceHeader()
        {
            Assert.That(ContainsType<EditorDebugWorkspacePanel>(
                RemoteWorkspaceHeaderRegistry.CreateHeaders()), Is.True);
        }

        [Test]
        public void LauncherAndSceneLoader_AreOwnedByModuleAssembly()
        {
            Assert.That(typeof(RemoteDebugHostLauncher).Assembly,
                Is.EqualTo(typeof(RemoteDebugHostSceneLoader).Assembly));
            Assert.That(typeof(RemoteDebugHostLauncher).Assembly.GetName().Name,
                Is.EqualTo("DevUtilities.RemoteDevUtilities.DebugHost.Editor"));
        }

        [Test]
        public void HostWorkflowSettings_AreOwnedByTheEditorOnlyModule()
        {
            Assert.That(typeof(RemoteDebugHostSettings).Assembly,
                Is.EqualTo(typeof(RemoteDebugHostLauncher).Assembly));
            Assert.That(typeof(ScriptableSingleton<RemoteDebugHostSettings>)
                .IsAssignableFrom(typeof(RemoteDebugHostSettings)), Is.True);
        }

        [TestCase(false, true, true, false, false, false, true)]
        [TestCase(true, true, true, false, false, false, false)]
        [TestCase(false, true, false, false, false, false, false)]
        [TestCase(false, true, true, true, false, false, false)]
        [TestCase(false, true, true, false, true, false, false)]
        [TestCase(false, true, true, false, false, true, false)]
        [TestCase(false, false, true, false, false, false, false)]
        public void AutoLaunch_OnlyOccursOnAnEligibleConnectionEdge(
            bool wasConnected,
            bool connected,
            bool enabled,
            bool hostActive,
            bool editorPlayingOrChangingPlayMode,
            bool suppressed,
            bool expected)
        {
            Assert.That(RemoteDebugHostLauncher.ShouldAutoLaunch(
                wasConnected,
                connected,
                enabled,
                hostActive,
                editorPlayingOrChangingPlayMode,
                suppressed), Is.EqualTo(expected));
        }

        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(false, false, false)]
        public void ConsoleAutoSpawn_IsSuppressedOnlyForAConsolelessHost(
            bool hostRequested,
            bool includeDeveloperConsoleUi,
            bool expected)
        {
            Assert.That(RemoteDebugHostLauncher.ShouldSuppressConsoleAutoSpawn(
                hostRequested,
                includeDeveloperConsoleUi), Is.EqualTo(expected));
        }

        private static bool ContainsType<T>(IEnumerable values)
        {
            foreach (object value in values)
            {
                if (value is T)
                    return true;
            }

            return false;
        }
    }
}
