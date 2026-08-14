using System.Collections.Generic;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class RemoteMiniToolCommandConfigurationTests
    {
        [Test]
        public void ProjectOverride_ReplacesPackageDefaultForSameMiniTool()
        {
            var bindings = new Dictionary<string, RemoteCommandPresentationBinding>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["GameInfo"] = new("GameInfo", "runtime.game-info", RemoteCommandRouting.ControlEditorToolOnly),
                ["Stats.FPS"] = new("Stats.FPS", "runtime.performance", RemoteCommandRouting.ControlEditorToolOnly)
            };
            var configuration = new RemoteMiniToolCommandConfiguration();
            configuration.Set("runtime.game-info", "Project.GameInfo", RemoteCommandRouting.ExecuteInBuildAndControlEditorTool);

            RemoteCommandPresentationRegistry.ApplyProjectOverrides(bindings, configuration.Overrides);

            Assert.That(bindings.ContainsKey("GameInfo"), Is.False);
            Assert.That(bindings.ContainsKey("Stats.FPS"), Is.True);
            Assert.That(bindings.TryGetValue("Project.GameInfo", out RemoteCommandPresentationBinding projectBinding), Is.True);
            Assert.That(projectBinding.MiniToolId, Is.EqualTo("runtime.game-info"));
            Assert.That(projectBinding.Routing, Is.EqualTo(RemoteCommandRouting.ExecuteInBuildAndControlEditorTool));
        }

        [Test]
        public void EmptyProjectCommand_DisablesPackageDefault()
        {
            var bindings = new Dictionary<string, RemoteCommandPresentationBinding>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["GameInfo"] = new("GameInfo", "runtime.game-info", RemoteCommandRouting.ControlEditorToolOnly)
            };
            var configuration = new RemoteMiniToolCommandConfiguration();
            configuration.Set("runtime.game-info", string.Empty, RemoteCommandRouting.ControlEditorToolOnly);

            RemoteCommandPresentationRegistry.ApplyProjectOverrides(bindings, configuration.Overrides);

            Assert.That(bindings, Is.Empty);
        }

        [Test]
        public void ClearingOverride_RestoresConfigurationToDefaultState()
        {
            var configuration = new RemoteMiniToolCommandConfiguration();
            Assert.That(configuration.Set("runtime.game-info", "Project.GameInfo", RemoteCommandRouting.ControlEditorToolOnly), Is.True);
            Assert.That(configuration.TryGet("RUNTIME.GAME-INFO", out RemoteMiniToolCommandOverride commandOverride), Is.True);
            Assert.That(commandOverride.CommandName, Is.EqualTo("Project.GameInfo"));

            Assert.That(configuration.Clear("runtime.game-info"), Is.True);
            Assert.That(configuration.TryGet("runtime.game-info", out _), Is.False);
        }
    }
}
