using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using UnityEditor;

namespace SAS.Utilities.RemoteDevUtilities.Editor.DebugHost
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class RemoteDebugHostContributionAttribute : Attribute
    {
        public RemoteDebugHostContributionAttribute(int order) => Order = order;
        public int Order { get; }
    }

    internal interface IRemoteDebugHostContribution
    {
        void Install(RemoteDevUtilitiesClient client);
        void Uninstall();
    }

    internal static class RemoteDebugHostContributionRegistry
    {
        internal static IReadOnlyList<IRemoteDebugHostContribution> CreateContributions()
        {
            var registrations = new List<(int Order, Type Type)>();
            foreach (Type type in TypeCache.GetTypesWithAttribute<RemoteDebugHostContributionAttribute>())
            {
                if (type == null || type.IsAbstract || !typeof(IRemoteDebugHostContribution).IsAssignableFrom(type))
                    continue;

                var attribute = (RemoteDebugHostContributionAttribute)Attribute.GetCustomAttribute(type, typeof(RemoteDebugHostContributionAttribute));
                if (attribute != null)
                    registrations.Add((attribute.Order, type));
            }

            registrations.Sort((left, right) =>
            {
                int order = left.Order.CompareTo(right.Order);
                return order != 0 ? order : string.Compare(left.Type.FullName, right.Type.FullName, StringComparison.Ordinal);
            });

            var contributions = new List<IRemoteDebugHostContribution>(registrations.Count);
            foreach ((int _, Type type) in registrations)
            {
                if (Activator.CreateInstance(type, true) is IRemoteDebugHostContribution contribution)
                    contributions.Add(contribution);
            }

            return contributions;
        }
    }
}
