using System;
using System.Collections.Generic;
using System.Text;
using SAS.DevUtilities;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry
{
    internal static class MiniToolSnapshotContractDiscovery
    {
        private static readonly Type SnapshotViewType =
            typeof(IMiniToolSnapshotView<>);

        internal static Type[] Find(GameObject prefab)
        {
            if (prefab == null)
                return Array.Empty<Type>();

            var snapshotTypes = new List<Type>();
            foreach (MonoBehaviour behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
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
                    if (!snapshotTypes.Contains(snapshotType))
                        snapshotTypes.Add(snapshotType);
                }
            }

            snapshotTypes.Sort((left, right) => string.Compare(left.FullName, right.FullName, StringComparison.Ordinal));
            return snapshotTypes.ToArray();
        }

        internal static bool HasCompatibleSnapshot(Type providerType, IEnumerable<Type> viewSnapshotTypes)
        {
            if (providerType == null || viewSnapshotTypes == null)
                return false;

            Type[] providerSnapshotTypes = MiniToolProviderCapabilities.GetSnapshotTypes(providerType);
            foreach (Type viewSnapshotType in viewSnapshotTypes)
            {
                if (Array.IndexOf(providerSnapshotTypes, viewSnapshotType) >= 0)
                    return true;
            }

            return false;
        }
    }

    internal static class MiniToolProviderTemplateGenerator
    {
        internal static string CreateFieldProvider(string className)
        {
            return
$@"using SAS.Utilities.RemoteDevUtilities.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;

[UnityEngine.Scripting.Preserve]
public sealed class {className} : MiniToolFieldDataProvider
{{
    public override RemoteMiniToolField[] CaptureFields()
    {{
        return new[]
        {{
            CreateField(""status"", ""Status"", ""Running"")
        }};
    }}
}}
";
        }

        internal static string CreateSnapshotProvider(
            string className,
            Type snapshotType)
        {
            if (snapshotType == null)
                throw new ArgumentNullException(nameof(snapshotType));

            string snapshotTypeName = GetCSharpTypeName(snapshotType);
            return
$@"using SAS.Utilities.RemoteDevUtilities.MiniTools;

[UnityEngine.Scripting.Preserve]
public sealed class {className} :
    MiniToolDataProvider<{snapshotTypeName}>
{{
    public override bool TryGetSnapshot(
        out {snapshotTypeName} snapshot)
    {{
        // TODO: Capture the current state from the running Player.
        snapshot = default;
        return false;
    }}
}}
";
        }

        internal static string GetCSharpTypeName(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (type.IsArray)
                return GetCSharpTypeName(type.GetElementType()) + "[]";

            if (!type.IsGenericType)
            {
                return "global::" + (type.FullName ?? type.Name).Replace('+', '.');
            }

            Type definition = type.GetGenericTypeDefinition();
            string definitionName = (definition.FullName ?? definition.Name).Replace('+', '.');
            int arityMarker = definitionName.IndexOf('`');
            if (arityMarker >= 0)
                definitionName = definitionName.Substring(0, arityMarker);

            var builder = new StringBuilder("global::");
            builder.Append(definitionName).Append('<');
            Type[] arguments = type.GetGenericArguments();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                builder.Append(GetCSharpTypeName(arguments[i]));
            }

            return builder.Append('>').ToString();
        }
    }
}
