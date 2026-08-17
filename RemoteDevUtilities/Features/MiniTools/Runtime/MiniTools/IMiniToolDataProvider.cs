using System;
using HP.DevUtilities;
using HP.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine.Scripting;

namespace HP.Utilities.RemoteDevUtilities.MiniTools
{
    /// <summary>
    /// Collects runtime data for a registered mini-tool. Identity, command routing,
    /// sampling, and Editor presentation belong to <see cref="MiniToolDefinition"/>.
    /// </summary>
    [RequireImplementors]
    public interface IMiniToolDataProvider
    {
        void Start();
        void Stop();
        void Tick();
    }

    /// <summary>
    /// Convenience lifecycle base. Native Workspace fields and typed Debug Host
    /// snapshot are independent optional capabilities.
    /// </summary>
    public abstract class MiniToolDataProvider : IMiniToolDataProvider
    {
        /// <summary>
        /// Creates a consistently initialized Native Workspace field.
        /// Providers remain responsible for formatting values and units.
        /// </summary>
        protected static RemoteMiniToolField CreateField(string name, string displayName, string value, string unit = "")
        {
            return new RemoteMiniToolField
            {
                Name = name,
                DisplayName = displayName,
                Value = value ?? string.Empty,
                Unit = unit ?? string.Empty
            };
        }

        /// <summary>
        /// Describes optional controls that Native Workspace and a shared
        /// Debug Host prefab can send to this provider.
        /// </summary>
        public virtual RemoteMiniToolActionDescriptor[] GetActions()
        {
            return Array.Empty<RemoteMiniToolActionDescriptor>();
        }

        /// <summary>
        /// Executes one of the actions returned by <see cref="GetActions"/>.
        /// Data-only providers do not need to override this method.
        /// </summary>
        public virtual bool TryExecuteAction(string actionId, out string error)
        {
            error = "This mini-tool does not expose remote actions.";
            return false;
        }

        public virtual void Start()
        {
        }

        public virtual void Stop()
        {
        }

        public virtual void Tick()
        {
        }
    }

    /// <summary>
    /// Base class for providers that supply strongly typed snapshot to a prefab
    /// hosted in the Editor Debug Host. Native Workspace fields are optional.
    /// </summary>
    /// <typeparam name="TSnapshot">
    /// The same snapshot consumed by the mini-tool prefab's
    /// <see cref="IMiniToolSnapshotView{TSnapshot}"/>.
    /// </typeparam>
    public abstract class MiniToolDataProvider<TSnapshot> : MiniToolDataProvider, IMiniToolSnapshotProvider<TSnapshot>, IRemoteMiniToolSnapshotCapture where TSnapshot : IMiniToolSnapshot
    {
        public abstract bool TryGetSnapshot(out TSnapshot snapshot);

        bool IRemoteMiniToolSnapshotCapture.TryCapture(out string snapshotTypeName, out string snapshotJson)
        {
            snapshotTypeName = string.Empty;
            snapshotJson = string.Empty;
            if (!TryGetSnapshot(out TSnapshot snapshot))
                return false;

            return RemoteMiniToolSnapshotSerializer.TrySerialize(in snapshot, out snapshotTypeName, out snapshotJson);
        }
    }

    /// <summary>
    /// Convenience base for a recoverable snapshot plus incremental
    /// event batches. Existing snapshot-only providers remain unchanged.
    /// </summary>
    public abstract class MiniToolStreamingDataProvider<TSnapshot, TEvent> : MiniToolDataProvider<TSnapshot>, IMiniToolStreamProvider<TEvent>, IRemoteMiniToolStreamCapture where TSnapshot : IMiniToolSnapshot where TEvent : IMiniToolStreamEvent
    {
        public abstract bool TryGetEvents(out TEvent[] events, out int droppedEventCount);

        bool IRemoteMiniToolStreamCapture.TryCapture(out string eventTypeName, out string eventsJson, out int droppedEventCount)
        {
            eventTypeName = string.Empty;
            eventsJson = string.Empty;
            droppedEventCount = 0;
            if (!TryGetEvents(out TEvent[] events, out droppedEventCount))
            {
                return false;
            }

            return RemoteMiniToolStreamSerializer.TrySerialize(events, out eventTypeName, out eventsJson);
        }
    }
}
