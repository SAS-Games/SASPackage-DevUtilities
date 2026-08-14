using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class RemoteCommandPresentationRegistryTests
    {
        [TestCase("Stats.FPS", "runtime.performance")]
        [TestCase("GameInfo", "runtime.game-info")]
        [TestCase("Animator.ShowStats", "runtime.animators")]
        [TestCase("Particle.ShowStats", "runtime.particles")]
        [TestCase("InputLatencyProfiler.Overlay", "runtime.input-latency")]
        [TestCase("InputVisualizer.Gamepad", "runtime.input-visualizer.gamepad")]
        [TestCase("InputVisualizer.Mouse", "runtime.input-visualizer.mouse")]
        public void BuiltInDefinitionCommands_ControlEditorToolOnly(string commandName, string miniToolId)
        {
            Assert.That(MiniToolRegistry.TryCreateCommandBinding(miniToolId, out RemoteCommandPresentationBinding binding), Is.True);
            Assert.That(binding.CommandName, Is.EqualTo(commandName));
            Assert.That(binding.MiniToolId, Is.EqualTo(miniToolId));
            Assert.That(binding.Routing, Is.EqualTo(RemoteCommandRouting.ControlEditorToolOnly));
        }

        [Test]
        public void GraphicsInfoCommand_ExecutesInBuildAndControlsEditorTool()
        {
            Assert.That(MiniToolRegistry.TryCreateCommandBinding("runtime.graphics-info", out RemoteCommandPresentationBinding binding), Is.True);
            Assert.That(binding.CommandName, Is.EqualTo("GraphicsInfo"));
            Assert.That(binding.Routing, Is.EqualTo(RemoteCommandRouting.ExecuteInBuildAndControlEditorTool));
        }

        [TestCase(null, true, true)]
        [TestCase("On", true, true)]
        [TestCase("off", true, false)]
        [TestCase("1", true, true)]
        [TestCase("0", true, false)]
        [TestCase("yes", true, true)]
        [TestCase("enable", true, true)]
        [TestCase("no", true, false)]
        [TestCase("disable", true, false)]
        [TestCase("invalid", false, false)]
        public void DefaultVisibilityParser_HandlesToggleSyntax(string argument, bool expectedSuccess, bool expectedVisibility)
        {
            string[] arguments = argument == null ? System.Array.Empty<string>() : new[] { argument, "ignored-extra-argument" };

            bool success = RemoteCommandPresentationBinding.TryParseToggle(arguments, out bool visible);

            Assert.That(success, Is.EqualTo(expectedSuccess));
            Assert.That(visible, Is.EqualTo(expectedVisibility));
        }

        [Test]
        public void ProjectEditorAssembly_CanRegisterAndRemoveCustomPresentation()
        {
            const string commandName = "Tests.NetworkOverlay";
            var binding = new RemoteCommandPresentationBinding(commandName, "tests.network", RemoteCommandRouting.ExecuteInBuildAndControlEditorTool);

            try
            {
                Assert.That(RemoteCommandPresentationRegistry.Register(binding, true), Is.True);
                Assert.That(RemoteCommandPresentationRegistry.TryGet(commandName, out var registered), Is.True);
                Assert.That(registered, Is.SameAs(binding));
            }
            finally
            {
                RemoteCommandPresentationRegistry.Unregister(commandName);
            }
        }

        [Test]
        public void BuiltIns_AreStoredInUnifiedDefinitions()
        {
            Assert.That(MiniToolRegistry.TryGet("runtime.game-info", out MiniToolRegistration registration), Is.True);
            Assert.That(registration.Definition, Is.Not.Null);
            Assert.That(RemoteCommandPresentationRegistry.TryGet("GameInfo", out _), Is.True);
        }

        [Test]
        public void RemovingCodeOverride_RevealsDefinitionDefault()
        {
            Assert.That(RemoteCommandPresentationRegistry.TryGet("GameInfo", out var packageDefault), Is.True);
            var projectOverride = new RemoteCommandPresentationBinding("GameInfo", "tests.game-info", RemoteCommandRouting.ExecuteInBuildAndControlEditorTool);

            try
            {
                Assert.That(RemoteCommandPresentationRegistry.Register(projectOverride, replaceExisting: true), Is.True);
                Assert.That(RemoteCommandPresentationRegistry.TryGet("GameInfo", out var registered), Is.True);
                Assert.That(registered, Is.SameAs(projectOverride));
            }
            finally
            {
                RemoteCommandPresentationRegistry.Unregister("GameInfo");
            }

            Assert.That(RemoteCommandPresentationRegistry.TryGet("GameInfo", out var restored), Is.True);
            Assert.That(restored.MiniToolId, Is.EqualTo(packageDefault.MiniToolId));
            Assert.That(restored.Routing, Is.EqualTo(packageDefault.Routing));
        }

        [Test]
        public void PlayerManifest_ProducesCommandBindingWithoutInstalledDefinition()
        {
            var descriptor = new RemoteMiniToolDescriptor
            {
                Id = "custom.network",
                DisplayName = "Network",
                Command = new RemoteMiniToolCommandManifest
                {
                    Name = "Network.Show",
                    SuggestedRouting = RemoteCommandRouting.ExecuteInBuildAndControlEditorTool
                }
            };

            Assert.That(RemoteMiniToolCommandManifestResolver.TryCreateBinding(descriptor, out RemoteCommandPresentationBinding binding), Is.True);
            Assert.That(binding.CommandName, Is.EqualTo("Network.Show"));
            Assert.That(binding.MiniToolId, Is.EqualTo("custom.network"));
            Assert.That(binding.Routing, Is.EqualTo(RemoteCommandRouting.ExecuteInBuildAndControlEditorTool));
        }

        [Test]
        public void PlayerManifest_IgnoresInvalidCommandMetadata()
        {
            var descriptor = new RemoteMiniToolDescriptor
            {
                Id = "custom.invalid",
                Command = new RemoteMiniToolCommandManifest
                {
                    Name = "Invalid Command"
                }
            };

            Assert.That(RemoteMiniToolCommandManifestResolver.TryCreateBinding(descriptor, out _), Is.False);
        }
    }
}
