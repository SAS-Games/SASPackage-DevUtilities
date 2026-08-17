using System;
using System.Collections.Generic;
using RuntimeConsole = SAS.Utilities.DeveloperConsole.DeveloperConsole;

namespace SAS.Utilities.RemoteDevUtilities.Commands
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class RuntimeDeveloperConsoleContributionAttribute : Attribute
    {
        public RuntimeDeveloperConsoleContributionAttribute(int order) => Order = order;
        public int Order { get; }
    }

    internal interface IRuntimeDeveloperConsoleContribution
    {
        void Configure(DeveloperConsole.DeveloperConsole console);
    }

    internal static class RuntimeDeveloperConsoleContributionRegistry
    {
        internal static void Configure(DeveloperConsole.DeveloperConsole console)
        {
            if (console == null)
                return;

            var registrations = new List<(int Order, Type Type)>();
            foreach (Type type in AppDomain.CurrentDomain.GetAssemblies()
                         .SelectManySafely(assembly => assembly.GetTypes()))
            {
                if (type == null || type.IsAbstract ||
                    !typeof(IRuntimeDeveloperConsoleContribution).IsAssignableFrom(type))
                    continue;
                var attribute = (RuntimeDeveloperConsoleContributionAttribute)Attribute.GetCustomAttribute(
                    type, typeof(RuntimeDeveloperConsoleContributionAttribute));
                if (attribute != null)
                    registrations.Add((attribute.Order, type));
            }

            registrations.Sort((left, right) =>
            {
                int order = left.Order.CompareTo(right.Order);
                return order != 0
                    ? order
                    : string.Compare(left.Type.FullName, right.Type.FullName, StringComparison.Ordinal);
            });

            foreach ((int _, Type type) in registrations)
            {
                if (Activator.CreateInstance(type, true) is IRuntimeDeveloperConsoleContribution contribution)
                    contribution.Configure(console);
            }
        }

        private static IEnumerable<Type> SelectManySafely(
            this IEnumerable<System.Reflection.Assembly> assemblies,
            Func<System.Reflection.Assembly, Type[]> selector)
        {
            foreach (System.Reflection.Assembly assembly in assemblies)
            {
                Type[] types;
                try
                {
                    types = selector(assembly);
                }
                catch (System.Reflection.ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types ?? Array.Empty<Type>())
                    yield return type;
            }
        }
    }
}
