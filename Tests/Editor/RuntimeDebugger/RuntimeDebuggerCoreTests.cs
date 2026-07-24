using System.Linq;
using NUnit.Framework;
using SAS.Utilities.RuntimeDebugger.Core;
using UnityEngine;

namespace SAS.Utilities.RuntimeDebugger.Tests
{
    public sealed class RuntimeDebuggerCoreTests
    {
        [Test]
        public void Registry_DoesNotResolveDestroyedObjectOrReuseId()
        {
            var registry = new RuntimeObjectRegistry();
            var first = new GameObject("First");
            registry.BeginReconciliation(); RuntimeObjectId firstId = registry.GetOrCreate(first); registry.EndReconciliation();
            Object.DestroyImmediate(first);
            registry.BeginReconciliation(); registry.EndReconciliation();
            Assert.That(registry.TryResolve(firstId, out GameObject _), Is.False);
            var second = new GameObject("Second");
            registry.BeginReconciliation(); RuntimeObjectId secondId = registry.GetOrCreate(second); registry.EndReconciliation();
            Assert.That(secondId.Value, Is.GreaterThan(firstId.Value));
            Object.DestroyImmediate(second);
        }

        [Test]
        public void Service_BuildsParentedHierarchyAndValidatesStaleCommands()
        {
            RuntimeDebuggerSettings settings = ScriptableObject.CreateInstance<RuntimeDebuggerSettings>();
            var parent = new GameObject("DebuggerParent"); var child = new GameObject("DebuggerChild"); child.transform.SetParent(parent.transform);
            using var service = new RuntimeDebuggerService(settings);
            RuntimeHierarchySnapshot snapshot = service.GetHierarchySnapshot();
            RuntimeHierarchyEntry parentEntry = snapshot.Entries.Single(item => item.Name == parent.name);
            RuntimeHierarchyEntry childEntry = snapshot.Entries.Single(item => item.Name == child.name);
            Assert.That(childEntry.ParentId, Is.EqualTo(parentEntry.Id));
            RuntimeCommandResult result = service.Execute(new SetGameObjectActiveCommand { ObjectId = childEntry.Id, Active = false });
            Assert.That(result.Success, Is.True); Assert.That(child.activeSelf, Is.False);
            Object.DestroyImmediate(parent); service.RefreshHierarchy();
            Assert.That(service.Execute(new SetGameObjectActiveCommand { ObjectId = childEntry.Id, Active = true }).Success, Is.False);
            Object.DestroyImmediate(settings);
        }

        [TestCase("42", typeof(int), 42)]
        [TestCase("true", typeof(bool), true)]
        public void DrawerRegistry_ParsesSupportedScalar(string text, System.Type type, object expected)
        {
            var registry = new RuntimeValueDrawerRegistry();
            Assert.That(registry.Resolve(type).TryParse(text, type, out object value, out string error), Is.True, error);
            Assert.That(value, Is.EqualTo(expected));
        }
    }
}
