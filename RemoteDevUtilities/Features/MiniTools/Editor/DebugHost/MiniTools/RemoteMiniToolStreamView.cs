using System;
using System.Collections.Generic;
using HP.DevUtilities;
using HP.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.DebugHost.MiniTools
{
    internal interface IRemoteMiniToolStreamView
    {
        bool TryApply(RemoteMiniToolStreamBatch batch);
    }

    internal static class RemoteMiniToolStreamViewFactory
    {
        private static readonly Type StreamViewType = typeof(IMiniToolStreamView<>);

        internal static IRemoteMiniToolStreamView[] Find(GameObject instance)
        {
            var views = new List<IRemoteMiniToolStreamView>();
            if (instance == null)
                return views.ToArray();

            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;

                foreach (Type implementedInterface in behaviour.GetType().GetInterfaces())
                {
                    if (!implementedInterface.IsGenericType || implementedInterface.GetGenericTypeDefinition() != StreamViewType)
                    {
                        continue;
                    }

                    Type eventType = implementedInterface.GetGenericArguments()[0];
                    Type adapterType = typeof(RemoteMiniToolStreamView<>).MakeGenericType(eventType);
                    if (Activator.CreateInstance(adapterType, new object[] { behaviour }) is IRemoteMiniToolStreamView view)
                    {
                        views.Add(view);
                    }
                }
            }

            return views.ToArray();
        }
    }

    internal sealed class RemoteMiniToolStreamView<TEvent> : IRemoteMiniToolStreamView where TEvent : IMiniToolStreamEvent
    {
        private readonly IMiniToolStreamView<TEvent> _view;
        private readonly string _eventTypeName;
        private readonly string _eventFullName;

        public RemoteMiniToolStreamView(IMiniToolStreamView<TEvent> view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _eventTypeName = typeof(TEvent).AssemblyQualifiedName ?? typeof(TEvent).FullName;
            _eventFullName = typeof(TEvent).FullName;
        }

        public bool TryApply(RemoteMiniToolStreamBatch batch)
        {
            if (batch == null || string.IsNullOrWhiteSpace(batch.EventTypeName) || string.IsNullOrWhiteSpace(batch.EventsJson) || !MatchesEventType(batch.EventTypeName))
            {
                return false;
            }

            RemoteMiniToolStreamPayload<TEvent> payload = JsonUtility.FromJson<RemoteMiniToolStreamPayload<TEvent>>(batch.EventsJson);
            _view.ApplyEvents(payload?.Events ?? Array.Empty<TEvent>(), batch.DroppedEventCount);
            return true;
        }

        private bool MatchesEventType(string eventTypeName)
        {
            return string.Equals(eventTypeName, _eventTypeName, StringComparison.Ordinal) || string.Equals(eventTypeName, _eventFullName, StringComparison.Ordinal) || eventTypeName.StartsWith(_eventFullName + ",", StringComparison.Ordinal);
        }
    }
}
