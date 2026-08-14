using System;
using System.Collections.Generic;
using System.Reflection;

namespace SAS.Utilities.RemoteDevUtilities.Transport
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class RuntimeRemoteTransportProviderAttribute : Attribute
    {
        public RuntimeRemoteTransportProviderAttribute(string id, int order)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("A runtime transport provider id is required.", nameof(id))
                : id;
            Order = order;
        }

        public string Id { get; }
        public int Order { get; }
    }

    internal interface IRuntimeRemoteTransportProvider
    {
        IRuntimeRemoteTransport Create(string runtimeSessionId, RemoteDevUtilitiesRuntimeSettings settings);
    }

    internal static class RuntimeRemoteTransportFactory
    {
        internal static IRuntimeRemoteTransport Create(string runtimeSessionId,
            RemoteDevUtilitiesRuntimeSettings settings, out IReadOnlyList<IRuntimeRemoteTransport> transports)
        {
            var registrations = new List<(RuntimeRemoteTransportProviderAttribute Attribute, Type Type)>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    if (type == null || type.IsAbstract || !typeof(IRuntimeRemoteTransportProvider).IsAssignableFrom(type))
                        continue;

                    RuntimeRemoteTransportProviderAttribute attribute =
                        type.GetCustomAttribute<RuntimeRemoteTransportProviderAttribute>();
                    if (attribute != null)
                        registrations.Add((attribute, type));
                }
            }

            registrations.Sort((left, right) =>
            {
                int order = left.Attribute.Order.CompareTo(right.Attribute.Order);
                return order != 0
                    ? order
                    : string.Compare(left.Type.FullName, right.Type.FullName, StringComparison.Ordinal);
            });

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var created = new List<IRuntimeRemoteTransport>(registrations.Count);
            foreach ((RuntimeRemoteTransportProviderAttribute attribute, Type type) in registrations)
            {
                if (!ids.Add(attribute.Id))
                    throw new InvalidOperationException($"A runtime transport provider with id '{attribute.Id}' is already registered.");
                if (Activator.CreateInstance(type, true) is not IRuntimeRemoteTransportProvider provider)
                    continue;

                IRuntimeRemoteTransport transport = provider.Create(runtimeSessionId, settings);
                if (transport != null)
                    created.Add(transport);
            }

            transports = created;
            if (created.Count == 0)
                return new RuntimeNullTransport();
            if (created.Count == 1)
                return created[0];
            return new RuntimeMultiplexedTransport(created.ToArray());
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types ?? Array.Empty<Type>();
            }
            catch (NotSupportedException)
            {
                return Array.Empty<Type>();
            }
        }

        private sealed class RuntimeNullTransport : IRuntimeRemoteTransport
        {
            public event Action<Protocol.RemoteEnvelope> MessageReceived { add { } remove { } }
            public event Action<int> EditorConnected { add { } remove { } }
            public event Action<int> EditorDisconnected { add { } remove { } }
            public bool RequiresAccessToken => false;
            public void Start() { }
            public void Tick() { }
            public void Send<T>(string messageType, long requestId, T payload) { }
            public void Dispose() { }
        }
    }
}
