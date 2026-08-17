using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using UnityEditor;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Connection
{
    internal static class RemoteEditorTransportIds
    {
        public const string PlayerConnection = "player-connection";
        public const string Tcp = "tcp";
    }

    internal sealed class RemoteEditorTransportConnectRequest
    {
        public string TargetName;
        public int PlayerId = -1;
        public string Host;
        public int Port;
        public string AccessToken;
    }

    internal interface IRemoteEditorTransport : IDisposable
    {
        string Id { get; }
        RemoteEditorConnectionKind Kind { get; }
        bool IsReady { get; }
        event Action<RemoteEnvelope> MessageReceived;
        event Action Ready;
        event Action<string> Disconnected;
        event Action<string> ConnectionFailed;
        event Action TargetsChanged;
        void Start();
        void Tick();
        void Connect(RemoteEditorTransportConnectRequest request);
        void Disconnect();
        void Send<T>(string messageType, long requestId, string editorSessionId, T payload);
    }

    internal interface IRemoteEditorPlayerTransport
    {
        IReadOnlyList<RemoteEditorPlayerDescriptor> GetConnectedPlayers();
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class RemoteEditorTransportProviderAttribute : Attribute
    {
        public RemoteEditorTransportProviderAttribute(string id, int order)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("An editor transport provider id is required.", nameof(id)) : id;
            Order = order;
        }

        public string Id { get; }
        public int Order { get; }
    }

    internal interface IRemoteEditorTransportProvider
    {
        IRemoteEditorTransport Create();
    }

    internal static class RemoteEditorTransportRegistry
    {
        internal static IReadOnlyList<IRemoteEditorTransport> CreateTransports()
        {
            var registrations = new List<(RemoteEditorTransportProviderAttribute Attribute, Type Type)>();
            foreach (Type type in TypeCache.GetTypesWithAttribute<RemoteEditorTransportProviderAttribute>())
            {
                if (type == null || type.IsAbstract || !typeof(IRemoteEditorTransportProvider).IsAssignableFrom(type))
                    continue;
                RemoteEditorTransportProviderAttribute attribute =
                    (RemoteEditorTransportProviderAttribute)Attribute.GetCustomAttribute(
                        type, typeof(RemoteEditorTransportProviderAttribute));
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
            var transports = new List<IRemoteEditorTransport>(registrations.Count);
            foreach ((RemoteEditorTransportProviderAttribute attribute, Type type) in registrations)
            {
                if (!ids.Add(attribute.Id))
                    throw new InvalidOperationException($"An editor transport provider with id '{attribute.Id}' is already registered.");
                if (Activator.CreateInstance(type, true) is not IRemoteEditorTransportProvider provider)
                    continue;
                IRemoteEditorTransport transport = provider.Create();
                if (transport != null)
                    transports.Add(transport);
            }

            return transports;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class RemoteEditorConnectionServiceAttribute : Attribute
    {
        public RemoteEditorConnectionServiceAttribute(string id, int order)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("An editor connection service id is required.", nameof(id))
                : id;
            Order = order;
        }

        public string Id { get; }
        public int Order { get; }
    }

    internal interface IRemoteEditorConnectionService : IDisposable
    {
        void Start(RemoteDevUtilitiesClient client);
        bool Tick(double now);
    }

    internal interface IRemoteLanDiscoveryService : IRemoteEditorConnectionService
    {
        IReadOnlyList<RemoteLanPlayerDescriptor> Players { get; }
        string Error { get; }
        bool Clear();
    }

    internal static class RemoteEditorConnectionServiceRegistry
    {
        internal static IReadOnlyList<IRemoteEditorConnectionService> CreateServices()
        {
            var registrations = new List<(RemoteEditorConnectionServiceAttribute Attribute, Type Type)>();
            foreach (Type type in TypeCache.GetTypesWithAttribute<RemoteEditorConnectionServiceAttribute>())
            {
                if (type == null || type.IsAbstract || !typeof(IRemoteEditorConnectionService).IsAssignableFrom(type))
                    continue;
                RemoteEditorConnectionServiceAttribute attribute =
                    (RemoteEditorConnectionServiceAttribute)Attribute.GetCustomAttribute(
                        type, typeof(RemoteEditorConnectionServiceAttribute));
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
            var services = new List<IRemoteEditorConnectionService>(registrations.Count);
            foreach ((RemoteEditorConnectionServiceAttribute attribute, Type type) in registrations)
            {
                if (!ids.Add(attribute.Id))
                    throw new InvalidOperationException($"An editor connection service with id '{attribute.Id}' is already registered.");
                if (Activator.CreateInstance(type, true) is IRemoteEditorConnectionService service)
                    services.Add(service);
            }

            return services;
        }
    }
}
