using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEditor;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration
{
    /// <summary>
    /// Exposes unified definitions without requiring a Player connection.
    /// </summary>
    [InitializeOnLoad]
    internal static class RemoteMiniToolEditorDiscovery
    {
        private static RemoteMiniToolDescriptor[] _descriptors;

        static RemoteMiniToolEditorDiscovery()
        {
            EditorApplication.projectChanged += Invalidate;
            MiniToolRegistry.Changed += Invalidate;
        }

        internal static IReadOnlyList<RemoteMiniToolDescriptor> Descriptors
        {
            get
            {
                EnsureDiscovered();
                return _descriptors;
            }
        }

        internal static void Invalidate()
        {
            _descriptors = null;
        }

        private static void EnsureDiscovered()
        {
            if (_descriptors != null)
                return;

            _descriptors = Discover();
        }

        private static RemoteMiniToolDescriptor[] Discover()
        {
            var descriptors = new Dictionary<string, RemoteMiniToolDescriptor>(StringComparer.OrdinalIgnoreCase);
            foreach (RemoteMiniToolDescriptor descriptor in MiniToolRegistry.GetDescriptors())
            {
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id))
                    continue;
                descriptors[descriptor.Id.Trim()] = Clone(descriptor);
            }

            var discoveredDescriptors = new RemoteMiniToolDescriptor[descriptors.Count];
            descriptors.Values.CopyTo(discoveredDescriptors, 0);
            Array.Sort(discoveredDescriptors, (left, right) => string.Compare(left.DisplayName ?? left.Id, right.DisplayName ?? right.Id, StringComparison.OrdinalIgnoreCase));
            return discoveredDescriptors;
        }

        private static RemoteMiniToolDescriptor Clone(RemoteMiniToolDescriptor descriptor)
        {
            return new RemoteMiniToolDescriptor
            {
                Id = descriptor.Id,
                DisplayName = descriptor.DisplayName,
                Description = descriptor.Description,
                DefaultIntervalSeconds = descriptor.DefaultIntervalSeconds,
                DefaultStreamIntervalSeconds = descriptor.DefaultStreamIntervalSeconds,
                VisibleByDefault = descriptor.VisibleByDefault,
                Capabilities = descriptor.Capabilities,
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
