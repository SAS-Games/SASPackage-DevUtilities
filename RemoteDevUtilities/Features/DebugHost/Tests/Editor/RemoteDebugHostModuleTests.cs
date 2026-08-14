using System.Collections;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.DebugHost;
using SAS.Utilities.RemoteDevUtilities.Editor.UI;
using SAS.Utilities.RemoteDevUtilities.Editor.UI.Panels;

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
