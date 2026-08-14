using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using UnityEditor;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Client
{
    internal interface IRemoteEditorSession
    {
        bool IsConnected { get; }
        long Send<T>(string messageType, T payload);
        void NotifyStateChanged();
    }

    internal interface IRemoteEditorFeatureClient
    {
        IEnumerable<string> MessageTypes { get; }
        void OnConnected();
        void Handle(RemoteEnvelope envelope);
        void Reset();
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class RemoteEditorFeatureAttribute : Attribute
    {
        public RemoteEditorFeatureAttribute(string id, int order)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("A remote editor feature id is required.", nameof(id))
                : id;
            Order = order;
        }

        public string Id { get; }
        public int Order { get; }
    }

    internal static class RemoteEditorFeatureRegistry
    {
        internal static IReadOnlyList<IRemoteEditorFeatureClient> CreateFeatures(IRemoteEditorSession session)
        {
            var registrations = new List<(RemoteEditorFeatureAttribute Attribute, Type Type)>();
            foreach (Type type in TypeCache.GetTypesWithAttribute<RemoteEditorFeatureAttribute>())
            {
                if (type == null || type.IsAbstract || !typeof(IRemoteEditorFeatureClient).IsAssignableFrom(type))
                    continue;

                RemoteEditorFeatureAttribute attribute =
                    (RemoteEditorFeatureAttribute)Attribute.GetCustomAttribute(type, typeof(RemoteEditorFeatureAttribute));
                if (attribute != null)
                    registrations.Add((attribute, type));
            }

            registrations.Sort(Compare);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var features = new List<IRemoteEditorFeatureClient>(registrations.Count);
            foreach ((RemoteEditorFeatureAttribute attribute, Type type) in registrations)
            {
                if (!ids.Add(attribute.Id))
                    throw new InvalidOperationException($"A remote editor feature with id '{attribute.Id}' is already registered.");

                if (Activator.CreateInstance(type, session) is not IRemoteEditorFeatureClient feature)
                    throw new InvalidOperationException($"Remote editor feature '{type.FullName}' could not be created.");
                features.Add(feature);
            }

            return features;
        }

        private static int Compare(
            (RemoteEditorFeatureAttribute Attribute, Type Type) left,
            (RemoteEditorFeatureAttribute Attribute, Type Type) right)
        {
            int order = left.Attribute.Order.CompareTo(right.Attribute.Order);
            return order != 0
                ? order
                : string.Compare(left.Type.FullName, right.Type.FullName, StringComparison.Ordinal);
        }
    }
}
