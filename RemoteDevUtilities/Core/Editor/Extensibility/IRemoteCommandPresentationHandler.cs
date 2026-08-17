using System;
using System.Collections.Generic;
using HP.Utilities.RemoteDevUtilities.Editor.Client;
using UnityEditor;

namespace HP.Utilities.RemoteDevUtilities.Editor.Commands.Presentation
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class RemoteCommandPresentationHandlerAttribute : Attribute
    {
        public RemoteCommandPresentationHandlerAttribute(int order) => Order = order;
        public int Order { get; }
    }

    internal readonly struct RemoteCommandPresentationResult
    {
        internal RemoteCommandPresentationResult(bool executeRemotely, bool success, string message)
        {
            ExecuteRemotely = executeRemotely;
            Success = success;
            Message = message;
        }

        internal bool ExecuteRemotely { get; }
        internal bool Success { get; }
        internal string Message { get; }

        internal static RemoteCommandPresentationResult Local(bool success, string message) => new(false, success, message);
        internal static RemoteCommandPresentationResult Remote() => new(true, true, null);
    }

    internal interface IRemoteCommandPresentationHandler
    {
        bool TryExecute(RemoteDevUtilitiesClient client, string commandName, string[] arguments, out RemoteCommandPresentationResult result);
    }

    internal static class RemoteCommandPresentationHandlerRegistry
    {
        internal static IReadOnlyList<IRemoteCommandPresentationHandler> CreateHandlers()
        {
            var registrations = new List<(int Order, Type Type)>();
            foreach (Type type in TypeCache.GetTypesWithAttribute<RemoteCommandPresentationHandlerAttribute>())
            {
                if (type == null || type.IsAbstract || !typeof(IRemoteCommandPresentationHandler).IsAssignableFrom(type))
                    continue;
                var attribute = (RemoteCommandPresentationHandlerAttribute)Attribute.GetCustomAttribute(type, typeof(RemoteCommandPresentationHandlerAttribute));
                if (attribute != null)
                    registrations.Add((attribute.Order, type));
            }

            registrations.Sort((left, right) =>
            {
                int order = left.Order.CompareTo(right.Order);
                return order != 0 ? order : string.Compare(left.Type.FullName, right.Type.FullName, StringComparison.Ordinal);
            });

            var handlers = new List<IRemoteCommandPresentationHandler>(registrations.Count);
            foreach ((int _, Type type) in registrations)
            {
                if (Activator.CreateInstance(type, true) is IRemoteCommandPresentationHandler handler)
                    handlers.Add(handler);
            }
            return handlers;
        }
    }
}
