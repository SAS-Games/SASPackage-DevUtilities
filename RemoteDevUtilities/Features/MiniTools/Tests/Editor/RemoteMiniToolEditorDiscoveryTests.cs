using System.Linq;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry;
using SAS.Utilities.RemoteDevUtilities.MiniTools;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class RemoteMiniToolEditorDiscoveryTests
    {
        [Test]
        public void EditorDiscovery_UsesUnifiedDefinitionsWithoutPlayerConnection()
        {
            RemoteMiniToolEditorDiscovery.Invalidate();

            var descriptor = RemoteMiniToolEditorDiscovery.Descriptors.Single(tool => tool.Id == "runtime.game-info");

            Assert.That(descriptor.Command.Name, Is.EqualTo("GameInfo"));
            Assert.That(RemoteMiniToolEditorDiscovery.Descriptors.Select(tool => tool.Id), Is.EquivalentTo(MiniToolRegistry.GetDescriptors().Select(tool => tool.Id)));
        }
    }
}
