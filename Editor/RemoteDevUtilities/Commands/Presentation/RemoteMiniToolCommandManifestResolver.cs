using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation
{
    internal static class RemoteMiniToolCommandManifestResolver
    {
        internal static bool TryCreateBinding(
            RemoteMiniToolDescriptor descriptor,
            out RemoteCommandPresentationBinding binding)
        {
            binding = null;
            if (descriptor == null ||
                string.IsNullOrWhiteSpace(descriptor.Id) ||
                descriptor.Command == null ||
                string.IsNullOrWhiteSpace(descriptor.Command.Name))
                return false;

            try
            {
                binding = new RemoteCommandPresentationBinding(
                    descriptor.Command.Name,
                    descriptor.Id,
                    descriptor.Command.SuggestedRouting);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        internal static bool TryFindBinding(
            IEnumerable<RemoteMiniToolDescriptor> descriptors,
            string commandName,
            out RemoteCommandPresentationBinding binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(commandName))
                return false;

            foreach (RemoteMiniToolDescriptor descriptor in
                     descriptors ??
                     Array.Empty<RemoteMiniToolDescriptor>())
            {
                if (!TryCreateBinding(descriptor, out var candidate) ||
                    !string.Equals(
                        candidate.CommandName,
                        commandName.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                binding = candidate;
                return true;
            }

            return false;
        }
    }
}
