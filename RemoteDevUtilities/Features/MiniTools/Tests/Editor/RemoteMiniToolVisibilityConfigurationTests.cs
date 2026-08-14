using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class RemoteMiniToolVisibilityConfigurationTests
    {
        [Test]
        public void UnknownTools_FollowDefaultVisibility()
        {
            var configuration = new RemoteMiniToolVisibilityConfiguration();

            Assert.That(configuration.IsVisible("custom.tool"), Is.True);
            Assert.That(configuration.SetShowNewToolsByDefault(false), Is.True);
            Assert.That(configuration.IsVisible("custom.tool"), Is.False);
        }

        [Test]
        public void ExplicitVisibility_SurvivesDefaultChanges()
        {
            var configuration = new RemoteMiniToolVisibilityConfiguration();

            Assert.That(configuration.SetVisible("runtime.performance", false), Is.True);
            Assert.That(configuration.SetShowNewToolsByDefault(false), Is.True);
            Assert.That(configuration.SetShowNewToolsByDefault(true), Is.True);
            Assert.That(configuration.IsVisible("runtime.performance"), Is.False);

            Assert.That(configuration.SetVisible("runtime.performance", true), Is.True);
            Assert.That(configuration.SetShowNewToolsByDefault(false), Is.True);
            Assert.That(configuration.IsVisible("runtime.performance"), Is.True);
        }

        [Test]
        public void DefinitionDefault_CanStartHidden()
        {
            var configuration = new RemoteMiniToolVisibilityConfiguration();
            var descriptor = Descriptor("custom.hidden", "Hidden", string.Empty);
            descriptor.VisibleByDefault = false;
            configuration.RegisterCatalog(new[] { descriptor });

            Assert.That(configuration.IsVisible(descriptor.Id), Is.False);
            Assert.That(configuration.SetVisible(descriptor.Id, true), Is.True);
            Assert.That(configuration.IsVisible(descriptor.Id), Is.True);
        }

        [Test]
        public void CatalogRegistration_UpdatesDescriptorWithoutDuplicates()
        {
            var configuration = new RemoteMiniToolVisibilityConfiguration();
            configuration.RegisterCatalog(new[]
            {
                Descriptor("custom.network", "Network", "First")
            });

            bool changed = configuration.RegisterCatalog(new[]
            {
                Descriptor("CUSTOM.NETWORK", "Network Traffic", "Updated")
            });

            Assert.That(changed, Is.True);
            var knownTools = configuration.GetKnownTools();
            Assert.That(knownTools, Has.Count.EqualTo(1));
            Assert.That(knownTools[0].DisplayName, Is.EqualTo("Network Traffic"));
            Assert.That(knownTools[0].Description, Is.EqualTo("Updated"));
        }

        [Test]
        public void CatalogRegistration_PreservesPortableCommandMetadata()
        {
            var configuration = new RemoteMiniToolVisibilityConfiguration();
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

            configuration.RegisterCatalog(new[] { descriptor });

            RemoteMiniToolDescriptor knownTool = configuration.GetKnownTools()[0];
            Assert.That(knownTool.Command, Is.Not.SameAs(descriptor.Command));
            Assert.That(knownTool.Command.Name, Is.EqualTo("Network.Show"));
            Assert.That(knownTool.Command.SuggestedRouting, Is.EqualTo(RemoteCommandRouting.ExecuteInBuildAndControlEditorTool));
        }

        [Test]
        public void Forget_RemovesRememberedToolAndVisibilityOverride()
        {
            var configuration = new RemoteMiniToolVisibilityConfiguration();
            RemoteMiniToolDescriptor descriptor = Descriptor("custom.removed", "Removed Tool", string.Empty);
            configuration.RegisterCatalog(new[] { descriptor });
            configuration.SetVisible(descriptor.Id, false);

            Assert.That(configuration.Forget(descriptor.Id), Is.True);
            Assert.That(configuration.GetKnownTools(), Is.Empty);
            Assert.That(configuration.Forget(descriptor.Id), Is.False);

            configuration.RegisterCatalog(new[] { descriptor });
            Assert.That(configuration.IsVisible(descriptor.Id), Is.True, "Forgetting the tool must clear its hidden override.");
        }

        [Test]
        public void ShowAllAndHideAll_ClearPerToolOverrides()
        {
            var configuration = new RemoteMiniToolVisibilityConfiguration();
            configuration.SetVisible("tool.a", false);

            Assert.That(configuration.HideAll(), Is.True);
            Assert.That(configuration.IsVisible("tool.a"), Is.False);
            Assert.That(configuration.IsVisible("tool.b"), Is.False);

            Assert.That(configuration.ShowAll(), Is.True);
            Assert.That(configuration.IsVisible("tool.a"), Is.True);
            Assert.That(configuration.IsVisible("tool.b"), Is.True);
        }

        private static RemoteMiniToolDescriptor Descriptor(string id, string displayName, string description)
        {
            return new RemoteMiniToolDescriptor
            {
                Id = id,
                DisplayName = displayName,
                Description = description,
                DefaultIntervalSeconds = 0.5f
            };
        }
    }
}
