using System;
using System.Collections.Generic;
using System.Text;
using SAS.DevUtilities;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine;
using UnityEngine.Scripting;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry
{
    internal static class MiniToolSnapshotContractDiscovery
    {
        private static readonly Type SnapshotViewType = typeof(IMiniToolSnapshotView<>);

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
            string usingDirectives = CreateUsingDirectives(typeof(MiniToolFieldDataProvider), typeof(RemoteMiniToolField), typeof(PreserveAttribute));
            string providerTypeName = GetUnqualifiedCSharpTypeName(typeof(MiniToolFieldDataProvider));
            string fieldTypeName = GetUnqualifiedCSharpTypeName(typeof(RemoteMiniToolField));

            return $@"{usingDirectives}[Preserve]
public sealed class {className} : {providerTypeName}
{{
    public override {fieldTypeName}[] CaptureFields()
    {{
        return new[]
        {{
            CreateField(""status"", ""Status"", ""Running"")
        }};
    }}
}}
";
        }

        internal static string CreateSnapshotProvider(string className, Type snapshotType)
        {
            if (snapshotType == null)
                throw new ArgumentNullException(nameof(snapshotType));

            Type providerType = typeof(MiniToolDataProvider<>).MakeGenericType(snapshotType);
            string usingDirectives = CreateUsingDirectives(providerType, typeof(PreserveAttribute));
            string snapshotTypeName = GetUnqualifiedCSharpTypeName(snapshotType);
            string providerTypeName = GetUnqualifiedCSharpTypeName(providerType);

            return $@"{usingDirectives}[Preserve]
public sealed class {className} : {providerTypeName}
{{
    public override bool TryGetSnapshot(out {snapshotTypeName} snapshot)
    {{
        // TODO: Capture the current state from the running Player.
        snapshot = default;
        return false;
    }}
}}
";
        }

        private static string CreateUsingDirectives(params Type[] types)
        {
            var namespaces = new HashSet<string>(StringComparer.Ordinal);
            foreach (Type type in types)
                CollectNamespaces(type, namespaces);

            var sortedNamespaces = new List<string>(namespaces);
            sortedNamespaces.Sort(StringComparer.Ordinal);

            var builder = new StringBuilder();
            foreach (string typeNamespace in sortedNamespaces)
                builder.Append("using ").Append(typeNamespace).AppendLine(";");

            if (builder.Length > 0)
                builder.AppendLine();

            return builder.ToString();
        }

        private static void CollectNamespaces(Type type, ISet<string> namespaces)
        {
            if (type == null)
                return;

            if (type.HasElementType)
            {
                CollectNamespaces(type.GetElementType(), namespaces);
                return;
            }

            if (!string.IsNullOrWhiteSpace(type.Namespace))
                namespaces.Add(type.Namespace);

            if (!type.IsGenericType)
                return;

            foreach (Type argument in type.GetGenericArguments())
                CollectNamespaces(argument, namespaces);
        }

        private static string GetUnqualifiedCSharpTypeName(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (type.IsGenericParameter)
                return type.Name;
            if (type.IsArray)
                return GetUnqualifiedCSharpTypeName(type.GetElementType()) + "[]";

            string typeName = GetTypeNameWithoutNamespace(type.IsGenericType ? type.GetGenericTypeDefinition() : type);
            if (!type.IsGenericType)
                return typeName;

            var builder = new StringBuilder(RemoveGenericArity(typeName));
            builder.Append('<');
            Type[] arguments = type.GetGenericArguments();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                builder.Append(GetUnqualifiedCSharpTypeName(arguments[i]));
            }

            return builder.Append('>').ToString();
        }

        private static string GetTypeNameWithoutNamespace(Type type)
        {
            string fullName = (type.FullName ?? type.Name).Replace('+', '.');
            string typeNamespace = type.Namespace;
            return string.IsNullOrWhiteSpace(typeNamespace) ? fullName : fullName.Substring(typeNamespace.Length + 1);
        }

        private static string RemoveGenericArity(string typeName)
        {
            var builder = new StringBuilder(typeName.Length);
            for (int i = 0; i < typeName.Length; i++)
            {
                if (typeName[i] != '`')
                {
                    builder.Append(typeName[i]);
                    continue;
                }

                while (i + 1 < typeName.Length && char.IsDigit(typeName[i + 1]))
                    i++;
            }

            return builder.ToString();
        }

    }
}
