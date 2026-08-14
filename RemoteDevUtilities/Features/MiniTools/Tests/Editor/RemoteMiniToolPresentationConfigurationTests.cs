using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class RemoteMiniToolPresentationConfigurationTests
    {
        [Test]
        public void SetPrefabGuid_AddsAndUpdatesProjectOverride()
        {
            var configuration = new RemoteMiniToolPresentationConfiguration();

            Assert.That(configuration.SetPrefabGuid("custom.tool", "first-guid"), Is.True);
            Assert.That(configuration.TryGetPrefabGuid("CUSTOM.TOOL", out string first), Is.True);
            Assert.That(first, Is.EqualTo("first-guid"));

            Assert.That(configuration.SetPrefabGuid("custom.tool", "second-guid"), Is.True);
            Assert.That(configuration.TryGetPrefabGuid("custom.tool", out string second), Is.True);
            Assert.That(second, Is.EqualTo("second-guid"));
        }

        [Test]
        public void Clear_RemovesOnlyRequestedOverride()
        {
            var configuration = new RemoteMiniToolPresentationConfiguration();
            configuration.SetPrefabGuid("first", "first-guid");
            configuration.SetPrefabGuid("second", "second-guid");

            Assert.That(configuration.Clear("FIRST"), Is.True);
            Assert.That(configuration.TryGetPrefabGuid("first", out _), Is.False);
            Assert.That(configuration.TryGetPrefabGuid("second", out string remaining), Is.True);
            Assert.That(remaining, Is.EqualTo("second-guid"));
        }
    }
}
