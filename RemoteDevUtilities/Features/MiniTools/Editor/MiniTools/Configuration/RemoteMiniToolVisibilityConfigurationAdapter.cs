using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration
{
    internal static class RemoteMiniToolVisibilityConfigurationAdapter
    {
        internal static bool RegisterCatalog(
            this RemoteMiniToolVisibilityConfiguration configuration,
            IEnumerable<RemoteMiniToolDescriptor> descriptors)
        {
            var stored = new List<RemoteMiniToolKnownDescriptor>();
            foreach (RemoteMiniToolDescriptor descriptor in descriptors ?? Array.Empty<RemoteMiniToolDescriptor>())
            {
                if (descriptor != null)
                    stored.Add(ToStored(descriptor));
            }
            return configuration.RegisterCatalog(stored);
        }

        internal static IReadOnlyList<RemoteMiniToolDescriptor> GetKnownTools(
            this RemoteMiniToolVisibilityConfiguration configuration)
        {
            IReadOnlyList<RemoteMiniToolKnownDescriptor> stored = configuration.KnownTools;
            var descriptors = new List<RemoteMiniToolDescriptor>(stored.Count);
            for (int i = 0; i < stored.Count; i++)
                descriptors.Add(ToDescriptor(stored[i]));
            return descriptors;
        }

        private static RemoteMiniToolKnownDescriptor ToStored(RemoteMiniToolDescriptor descriptor) => new()
        {
            Id = descriptor.Id,
            DisplayName = descriptor.DisplayName,
            Description = descriptor.Description,
            DefaultIntervalSeconds = descriptor.DefaultIntervalSeconds,
            DefaultStreamIntervalSeconds = descriptor.DefaultStreamIntervalSeconds,
            VisibleByDefault = descriptor.VisibleByDefault,
            Capabilities = (int)descriptor.Capabilities,
            Command = descriptor.Command == null
                ? null
                : new RemoteMiniToolKnownCommand
                {
                    Name = descriptor.Command.Name,
                    SuggestedRouting = descriptor.Command.SuggestedRouting
                }
        };

        private static RemoteMiniToolDescriptor ToDescriptor(RemoteMiniToolKnownDescriptor descriptor)
        {
            if (descriptor == null)
                return null;
            return new RemoteMiniToolDescriptor
            {
                Id = descriptor.Id,
                DisplayName = descriptor.DisplayName,
                Description = descriptor.Description,
                DefaultIntervalSeconds = descriptor.DefaultIntervalSeconds,
                DefaultStreamIntervalSeconds = descriptor.DefaultStreamIntervalSeconds,
                VisibleByDefault = descriptor.VisibleByDefault,
                Capabilities = (RemoteMiniToolCapabilities)descriptor.Capabilities,
                Command = descriptor.Command == null
                    ? null
                    : new RemoteMiniToolCommandManifest
                    {
                        Name = descriptor.Command.Name,
                        SuggestedRouting = descriptor.Command.SuggestedRouting
                    }
            };
        }
    }
}
