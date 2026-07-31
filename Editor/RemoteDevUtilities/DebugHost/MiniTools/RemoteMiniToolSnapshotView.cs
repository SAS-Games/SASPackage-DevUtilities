using System;
using System.Collections.Generic;
using SAS.DevUtilities;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.DebugHost.MiniTools
{
    /// <summary>
    /// Type-erased bridge between a remote snapshot and the matching view
    /// component on the original mini-tool prefab.
    /// </summary>
    internal interface IRemoteMiniToolSnapshotView
    {
        bool TryApply(RemoteMiniToolSample sample);
    }

    internal static class RemoteMiniToolSnapshotViewFactory
    {
        private static readonly Type SnapshotViewType = typeof(IMiniToolSnapshotView<>);
        private static readonly Type SnapshotProviderType = typeof(IMiniToolSnapshotProvider<>);

        internal static IRemoteMiniToolSnapshotView[] Find(GameObject instance)
        {
            var views = new List<IRemoteMiniToolSnapshotView>();
            if (instance == null)
                return views.ToArray();

            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;

                foreach (Type implementedInterface in behaviour.GetType().GetInterfaces())
                {
                    if (!implementedInterface.IsGenericType || implementedInterface.GetGenericTypeDefinition() != SnapshotViewType)
                    {
                        continue;
                    }

                    Type snapshotType = implementedInterface.GetGenericArguments()[0];
                    Type adapterType = typeof(RemoteMiniToolSnapshotView<>).MakeGenericType(snapshotType);

                    // Keep the argument array explicit. UnityEngine.Object has
                    // an implicit bool conversion, which otherwise selects
                    // Activator.CreateInstance(Type, bool) and incorrectly
                    // requests a default constructor.
                    if (Activator.CreateInstance(adapterType, new object[] { behaviour }) is IRemoteMiniToolSnapshotView view)
                    {
                        views.Add(view);
                    }
                }
            }

            return views.ToArray();
        }

        internal static bool IsSnapshotProvider(MonoBehaviour behaviour)
        {
            if (behaviour == null)
                return false;

            foreach (Type implementedInterface in behaviour.GetType().GetInterfaces())
            {
                if (implementedInterface.IsGenericType && implementedInterface.GetGenericTypeDefinition() == SnapshotProviderType)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class RemoteMiniToolSnapshotView<TSnapshot> : IRemoteMiniToolSnapshotView where TSnapshot : IMiniToolSnapshot
    {
        private readonly IMiniToolSnapshotView<TSnapshot> _view;
        private readonly string _snapshotTypeName;
        private readonly string _snapshotFullName;

        public RemoteMiniToolSnapshotView(IMiniToolSnapshotView<TSnapshot> view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _snapshotTypeName = typeof(TSnapshot).AssemblyQualifiedName ?? typeof(TSnapshot).FullName;
            _snapshotFullName = typeof(TSnapshot).FullName;
        }

        public bool TryApply(RemoteMiniToolSample sample)
        {
            if (sample == null || string.IsNullOrWhiteSpace(sample.SnapshotTypeName) || string.IsNullOrWhiteSpace(sample.SnapshotJson))
            {
                return false;
            }

            if (!string.Equals(sample.SnapshotTypeName, _snapshotTypeName, StringComparison.Ordinal) && !string.Equals(sample.SnapshotTypeName, _snapshotFullName, StringComparison.Ordinal) && !sample.SnapshotTypeName.StartsWith(_snapshotFullName + ",", StringComparison.Ordinal))
            {
                return false;
            }

            TSnapshot snapshot = JsonUtility.FromJson<TSnapshot>(sample.SnapshotJson);
            _view.ApplySnapshot(in snapshot);
            return true;
        }
    }
}
