using NUnit.Framework;
using SAS.DevUtilities;
using SAS.Utilities.DeveloperConsole.InputVisualizers;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class InputVisualizerRemoteIntegrationTests
    {
        [TestCase("runtime.input-visualizer.gamepad")]
        [TestCase("runtime.input-visualizer.mouse")]
        public void Definition_UsesTypedDebugHostDataWithoutNativeWorkspaceFields(string toolId)
        {
            Assert.That(MiniToolRegistry.TryGet(toolId, out MiniToolRegistration registration), Is.True);
            Assert.That(registration.Descriptor.Capabilities, Is.EqualTo(RemoteMiniToolCapabilities.TypedDebugHostSnapshot | RemoteMiniToolCapabilities.EventStream));
            Assert.That(registration.Definition.TryGetProviderType(out System.Type providerType), Is.True);
            Assert.That(typeof(IMiniToolFieldProvider).IsAssignableFrom(providerType), Is.False);
        }

        [TestCase("runtime.input-visualizer.gamepad")]
        [TestCase("runtime.input-visualizer.mouse")]
        public void DebugHostPrefab_ReusesInputVisualizerHandler(string toolId)
        {
            Assert.That(MiniToolRegistry.TryGet(toolId, out MiniToolRegistration registration), Is.True);
            InputVisualizerHandler handler = registration.LoadDebugHostPrefab().GetComponent<InputVisualizerHandler>();

            Assert.That(handler, Is.Not.Null);
            Assert.That(handler, Is.AssignableTo<IMiniToolSnapshotView<InputVisualizerSnapshot>>());
            Assert.That(handler, Is.AssignableTo<IMiniToolStreamView<InputVisualizerSampleEvent>>());
            Assert.That(handler, Is.AssignableTo<IMiniToolLocalController>());
        }
    }
}
