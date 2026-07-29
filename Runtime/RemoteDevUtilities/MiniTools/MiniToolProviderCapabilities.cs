using System;
using System.Collections.Generic;
using SAS.DevUtilities;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools
{
    /// <summary>
    /// Describes the independently optional outputs exposed by a provider.
    /// </summary>
    internal static class MiniToolProviderCapabilities
    {
        private static readonly Type SnapshotProviderType =
            typeof(IMiniToolSnapshotProvider<>);
        private static readonly Type StreamProviderType =
            typeof(IMiniToolStreamProvider<>);

        internal static bool ProvidesFields(Type providerType)
        {
            return providerType != null &&
                   typeof(IMiniToolFieldProvider)
                       .IsAssignableFrom(providerType);
        }

        internal static bool ProvidesTypedSnapshot(Type providerType)
        {
            return GetSnapshotTypes(providerType).Length > 0 ||
                   (providerType != null &&
                    typeof(IRemoteMiniToolSnapshotCapture)
                        .IsAssignableFrom(providerType));
        }

        internal static Type[] GetSnapshotTypes(Type providerType)
        {
            if (providerType == null)
                return Array.Empty<Type>();

            var snapshotTypes = new List<Type>();
            foreach (Type implementedInterface in
                     providerType.GetInterfaces())
            {
                if (!implementedInterface.IsGenericType ||
                    implementedInterface.GetGenericTypeDefinition() !=
                    SnapshotProviderType)
                {
                    continue;
                }

                Type snapshotType =
                    implementedInterface.GetGenericArguments()[0];
                if (!snapshotTypes.Contains(snapshotType))
                    snapshotTypes.Add(snapshotType);
            }

            snapshotTypes.Sort(
                (left, right) => string.Compare(
                    left.FullName,
                    right.FullName,
                    StringComparison.Ordinal));
            return snapshotTypes.ToArray();
        }

        internal static bool ProvidesEventStream(
            Type providerType)
        {
            return GetStreamEventTypes(providerType).Length > 0 ||
                   (providerType != null &&
                    typeof(IRemoteMiniToolStreamCapture)
                        .IsAssignableFrom(providerType));
        }

        internal static Type[] GetStreamEventTypes(
            Type providerType)
        {
            if (providerType == null)
                return Array.Empty<Type>();

            var eventTypes = new List<Type>();
            foreach (Type implementedInterface in
                     providerType.GetInterfaces())
            {
                if (!implementedInterface.IsGenericType ||
                    implementedInterface.GetGenericTypeDefinition() !=
                    StreamProviderType)
                {
                    continue;
                }

                Type eventType =
                    implementedInterface.GetGenericArguments()[0];
                if (!eventTypes.Contains(eventType))
                    eventTypes.Add(eventType);
            }

            eventTypes.Sort(
                (left, right) => string.Compare(
                    left.FullName,
                    right.FullName,
                    StringComparison.Ordinal));
            return eventTypes.ToArray();
        }

    }
}
