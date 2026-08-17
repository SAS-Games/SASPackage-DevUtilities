using System;
using System.Reflection;
using HP.DevUtilities;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.MiniTools
{
    /// <summary>
    /// Type-erased snapshot bridge between a registered provider and the remote
    /// protocol. Reflection is used only while the registration is created.
    /// </summary>
    internal interface IRemoteMiniToolSnapshotCapture
    {
        bool TryCapture(out string snapshotTypeName, out string snapshotJson);
    }

    internal static class RemoteMiniToolSnapshotSerializer
    {
        internal static bool TrySerialize<TSnapshot>(in TSnapshot snapshot, out string snapshotTypeName, out string snapshotJson) where TSnapshot : IMiniToolSnapshot
        {
            snapshotTypeName = string.Empty;
            snapshotJson = string.Empty;

            try
            {
                snapshotTypeName = typeof(TSnapshot).AssemblyQualifiedName ?? typeof(TSnapshot).FullName;
                snapshotJson = JsonUtility.ToJson(snapshot);
                return !string.IsNullOrWhiteSpace(snapshotJson);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                snapshotTypeName = string.Empty;
                snapshotJson = string.Empty;
                return false;
            }
        }
    }

    internal static class RemoteMiniToolSnapshotCaptureFactory
    {
        private static readonly Type SnapshotProviderType = typeof(IMiniToolSnapshotProvider<>);

        internal static IRemoteMiniToolSnapshotCapture Create(IMiniToolDataProvider provider, out string error)
        {
            error = string.Empty;
            if (provider == null)
                return null;

            // Providers deriving from MiniToolDataProvider<TSnapshot> use this
            // direct path, which is safe for AOT/IL2CPP Player builds.
            if (provider is IRemoteMiniToolSnapshotCapture directCapture)
                return directCapture;

            foreach (Type implementedInterface in provider.GetType().GetInterfaces())
            {
                if (!implementedInterface.IsGenericType || implementedInterface.GetGenericTypeDefinition() != SnapshotProviderType)
                {
                    continue;
                }

                Type snapshotType = implementedInterface.GetGenericArguments()[0];
                Type captureType = typeof(RemoteMiniToolSnapshotCapture<>).MakeGenericType(snapshotType);

                try
                {
                    return Activator.CreateInstance(captureType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { provider }, null) as IRemoteMiniToolSnapshotCapture;
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

    internal sealed class RemoteMiniToolSnapshotCapture<TSnapshot> : IRemoteMiniToolSnapshotCapture where TSnapshot : IMiniToolSnapshot
    {
        private readonly IMiniToolSnapshotProvider<TSnapshot> _provider;

        internal RemoteMiniToolSnapshotCapture(IMiniToolSnapshotProvider<TSnapshot> provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public bool TryCapture(out string snapshotTypeName, out string snapshotJson)
        {
            snapshotTypeName = string.Empty;
            snapshotJson = string.Empty;

            if (!_provider.TryGetSnapshot(out TSnapshot snapshot))
                return false;

            return RemoteMiniToolSnapshotSerializer.TrySerialize(in snapshot, out snapshotTypeName, out snapshotJson);
        }
    }
}
