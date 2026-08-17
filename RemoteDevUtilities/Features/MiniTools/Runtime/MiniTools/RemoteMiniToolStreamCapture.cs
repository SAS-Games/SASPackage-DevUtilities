using System;
using System.Reflection;
using HP.DevUtilities;
using HP.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.MiniTools
{
    /// <summary>
    /// Type-erased bridge between an optional incremental provider and the
    /// remote mini-tool protocol.
    /// </summary>
    internal interface IRemoteMiniToolStreamCapture
    {
        bool TryCapture(out string eventTypeName, out string eventsJson, out int droppedEventCount);
    }

    internal static class RemoteMiniToolStreamSerializer
    {
        internal static bool TrySerialize<TEvent>(TEvent[] events, out string eventTypeName, out string eventsJson) where TEvent : IMiniToolStreamEvent
        {
            eventTypeName = string.Empty;
            eventsJson = string.Empty;
            if (events == null || events.Length == 0)
                return false;

            try
            {
                eventTypeName = typeof(TEvent).AssemblyQualifiedName ?? typeof(TEvent).FullName;
                eventsJson = JsonUtility.ToJson(new RemoteMiniToolStreamPayload<TEvent>
                {
                    Events = events
                });
                return !string.IsNullOrWhiteSpace(eventsJson);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                eventTypeName = string.Empty;
                eventsJson = string.Empty;
                return false;
            }
        }
    }

    internal static class RemoteMiniToolStreamCaptureFactory
    {
        private static readonly Type StreamProviderType = typeof(IMiniToolStreamProvider<>);

        internal static IRemoteMiniToolStreamCapture Create(IMiniToolDataProvider provider, out string error)
        {
            error = string.Empty;
            if (provider == null)
                return null;

            if (provider is IRemoteMiniToolStreamCapture directCapture)
                return directCapture;

            foreach (Type implementedInterface in provider.GetType().GetInterfaces())
            {
                if (!implementedInterface.IsGenericType || implementedInterface.GetGenericTypeDefinition() != StreamProviderType)
                {
                    continue;
                }

                Type eventType = implementedInterface.GetGenericArguments()[0];
                Type captureType = typeof(RemoteMiniToolStreamCapture<>).MakeGenericType(eventType);

                try
                {
                    return Activator.CreateInstance(captureType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { provider }, null) as IRemoteMiniToolStreamCapture;
                }
                catch (Exception exception)
                {
                    error = exception.GetBaseException().Message;
                    return null;
                }
            }

            return null;
        }
    }

    internal sealed class RemoteMiniToolStreamCapture<TEvent> : IRemoteMiniToolStreamCapture where TEvent : IMiniToolStreamEvent
    {
        private readonly IMiniToolStreamProvider<TEvent> _provider;

        internal RemoteMiniToolStreamCapture(IMiniToolStreamProvider<TEvent> provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public bool TryCapture(out string eventTypeName, out string eventsJson, out int droppedEventCount)
        {
            eventTypeName = string.Empty;
            eventsJson = string.Empty;
            droppedEventCount = 0;
            if (!_provider.TryGetEvents(out TEvent[] events, out droppedEventCount))
            {
                return false;
            }

            return RemoteMiniToolStreamSerializer.TrySerialize(events, out eventTypeName, out eventsJson);
        }
    }
}
