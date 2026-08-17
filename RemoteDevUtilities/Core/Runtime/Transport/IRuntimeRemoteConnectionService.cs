using System;
using System.Collections.Generic;
using System.Reflection;

namespace HP.Utilities.RemoteDevUtilities.Transport
{
    internal sealed class RuntimeRemoteConnectionServiceContext
    {
        public string RuntimeSessionId;
        public RemoteDevUtilitiesRuntimeSettings Settings;
        public IReadOnlyList<IRuntimeRemoteTransport> Transports;
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class RuntimeRemoteConnectionServiceAttribute : Attribute
    {
        public RuntimeRemoteConnectionServiceAttribute(string id, int order)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("A runtime connection service id is required.", nameof(id))
                : id;
            Order = order;
        }

        public string Id { get; }
        public int Order { get; }
    }

    internal interface IRuntimeRemoteConnectionService : IDisposable
    {
        void Initialize(RuntimeRemoteConnectionServiceContext context);
        void Tick();
    }

    internal static class RuntimeRemoteConnectionServiceRegistry
    {
        internal static IReadOnlyList<IRuntimeRemoteConnectionService> CreateServices()
        {
            var registrations = new List<(RuntimeRemoteConnectionServiceAttribute Attribute, Type Type)>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    if (type == null || type.IsAbstract || !typeof(IRuntimeRemoteConnectionService).IsAssignableFrom(type))
                        continue;
                    RuntimeRemoteConnectionServiceAttribute attribute =
                        type.GetCustomAttribute<RuntimeRemoteConnectionServiceAttribute>();
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
            var services = new List<IRuntimeRemoteConnectionService>(registrations.Count);
            foreach ((RuntimeRemoteConnectionServiceAttribute attribute, Type type) in registrations)
            {
                if (!ids.Add(attribute.Id))
                    throw new InvalidOperationException($"A runtime connection service with id '{attribute.Id}' is already registered.");
                if (Activator.CreateInstance(type, true) is IRuntimeRemoteConnectionService service)
                    services.Add(service);
            }

            return services;
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { return exception.Types ?? Array.Empty<Type>(); }
            catch (NotSupportedException) { return Array.Empty<Type>(); }
        }
    }
}
