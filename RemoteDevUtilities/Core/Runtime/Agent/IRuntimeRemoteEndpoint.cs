using System;
using System.Collections.Generic;
using System.Reflection;
using SAS.Utilities.RemoteDevUtilities.Protocol;

namespace SAS.Utilities.RemoteDevUtilities.Agent
{
    internal interface IRuntimeRemoteSender
    {
        bool RequiresAccessToken { get; }
        void Send<T>(string messageType, long requestId, T payload);
    }

    internal sealed class RuntimeRemoteEndpointContext
    {
        public IRuntimeRemoteSender Sender;
        public string RuntimeSessionId;
        public RemoteDevUtilitiesRuntimeSettings Settings;
    }

    internal interface IRuntimeRemoteEndpoint : IDisposable
    {
        IEnumerable<string> MessageTypes { get; }
        void Initialize(RuntimeRemoteEndpointContext context);
        void Handle(RemoteEnvelope envelope);
        void Tick();
    }

    internal interface IRuntimeRemoteSessionListener
    {
        void OnRemoteSessionStateChanged(bool active);
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class RuntimeRemoteEndpointAttribute : Attribute
    {
        public RuntimeRemoteEndpointAttribute(string id, int order)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("A remote endpoint id is required.", nameof(id))
                : id;
            Order = order;
        }

        public string Id { get; }
        public int Order { get; }
    }

    internal static class RuntimeRemoteEndpointRegistry
    {
        internal static IReadOnlyList<IRuntimeRemoteEndpoint> CreateEndpoints()
        {
            var registrations = new List<(RuntimeRemoteEndpointAttribute Attribute, Type Type)>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    if (type == null || type.IsAbstract || !typeof(IRuntimeRemoteEndpoint).IsAssignableFrom(type))
                        continue;

                    RuntimeRemoteEndpointAttribute attribute = type.GetCustomAttribute<RuntimeRemoteEndpointAttribute>();
                    if (attribute != null)
                        registrations.Add((attribute, type));
                }
            }

            registrations.Sort(Compare);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var endpoints = new List<IRuntimeRemoteEndpoint>(registrations.Count);
            foreach ((RuntimeRemoteEndpointAttribute attribute, Type type) in registrations)
            {
                if (!ids.Add(attribute.Id))
                    throw new InvalidOperationException($"A runtime remote endpoint with id '{attribute.Id}' is already registered.");

                if (Activator.CreateInstance(type, true) is not IRuntimeRemoteEndpoint endpoint)
                    throw new InvalidOperationException($"Runtime remote endpoint '{type.FullName}' could not be created.");
                endpoints.Add(endpoint);
            }

            return endpoints;
        }

        private static int Compare((RuntimeRemoteEndpointAttribute Attribute, Type Type) left, (RuntimeRemoteEndpointAttribute Attribute, Type Type) right)
        {
            int order = left.Attribute.Order.CompareTo(right.Attribute.Order);
            return order != 0 ? order : string.Compare(left.Type.FullName, right.Type.FullName, StringComparison.Ordinal);
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
    }
}
