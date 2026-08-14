using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using UnityEditor;

namespace SAS.Utilities.RemoteDevUtilities.Editor.UI.Panels
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class RemoteConnectionPanelContributionAttribute : Attribute
    {
        public RemoteConnectionPanelContributionAttribute(string id, int order)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("A connection panel contribution id is required.", nameof(id))
                : id;
            Order = order;
        }

        public string Id { get; }
        public int Order { get; }
    }

    internal interface IRemoteConnectionPanelContribution : IDisposable
    {
        void Initialize(EditorWindow owner, Action repaint);
        void Draw(RemoteDevUtilitiesClient client);
    }

    internal static class RemoteConnectionPanelContributionRegistry
    {
        internal static IReadOnlyList<IRemoteConnectionPanelContribution> CreateContributions(
            EditorWindow owner, Action repaint)
        {
            var registrations = new List<(RemoteConnectionPanelContributionAttribute Attribute, Type Type)>();
            foreach (Type type in TypeCache.GetTypesWithAttribute<RemoteConnectionPanelContributionAttribute>())
            {
                if (type == null || type.IsAbstract || !typeof(IRemoteConnectionPanelContribution).IsAssignableFrom(type))
                    continue;
                RemoteConnectionPanelContributionAttribute attribute =
                    (RemoteConnectionPanelContributionAttribute)Attribute.GetCustomAttribute(
                        type, typeof(RemoteConnectionPanelContributionAttribute));
                if (attribute != null)
                    registrations.Add((attribute, type));
            }

            registrations.Sort((left, right) =>
            {
                int order = left.Attribute.Order.CompareTo(right.Attribute.Order);
                return order != 0
                    ? order
                    : string.Compare(left.Type.FullName, right.Type.FullName, StringComparison.Ordinal);
            });

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var contributions = new List<IRemoteConnectionPanelContribution>(registrations.Count);
            foreach ((RemoteConnectionPanelContributionAttribute attribute, Type type) in registrations)
            {
                if (!ids.Add(attribute.Id))
                    throw new InvalidOperationException($"A connection panel contribution with id '{attribute.Id}' is already registered.");
                if (Activator.CreateInstance(type, true) is not IRemoteConnectionPanelContribution contribution)
                    continue;
                contribution.Initialize(owner, repaint);
                contributions.Add(contribution);
            }

            return contributions;
        }
    }
}
