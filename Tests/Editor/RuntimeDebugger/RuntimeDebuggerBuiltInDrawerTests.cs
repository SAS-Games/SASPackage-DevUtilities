using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SAS.Utilities.RuntimeDebugger.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SAS.Utilities.RuntimeDebugger.Tests
{
    public sealed class RuntimeDebuggerBuiltInDrawerTests
    {
        private static IEnumerable<TestCaseData> SupportedComponentCases()
        {
            yield return SupportedComponent<Rigidbody>(false, "Mass");
            yield return SupportedComponent<Rigidbody2D>(false, "Gravity Scale");
            yield return SupportedComponent<BoxCollider>(true, "Is Trigger", "Size");
            yield return SupportedComponent<BoxCollider2D>(true, "Density", "Size");
            yield return SupportedComponent<Light>(true, "Intensity");
            yield return SupportedComponent<Animator>(true, "Speed");
        }

        private static IEnumerable<TestCaseData> ColliderSubtypeCases()
        {
            yield return ColliderSubtype<BoxCollider>("Center", "Size");
            yield return ColliderSubtype<SphereCollider>("Center", "Radius");
            yield return ColliderSubtype<CapsuleCollider>("Center", "Radius", "Height", "Direction");
            yield return ColliderSubtype<MeshCollider>("Convex", "Shared Mesh");
            yield return ColliderSubtype<BoxCollider2D>("Size", "Edge Radius");
            yield return ColliderSubtype<CircleCollider2D>("Radius");
            yield return ColliderSubtype<CapsuleCollider2D>("Size", "Direction");
            yield return ColliderSubtype<EdgeCollider2D>("Edge Radius", "Point Count");
            yield return ColliderSubtype<PolygonCollider2D>("Path Count");
            yield return ColliderSubtype<CompositeCollider2D>("Geometry Type", "Generation Type", "Path Count",
                "Point Count");
        }

        [TestCaseSource(nameof(SupportedComponentCases))]
        public void Service_ExposesBuiltInMembersAndEnabledState(Type componentType, bool hasEnabledState,
            string[] expectedDisplayNames)
        {
            using var context = new ComponentContext(componentType);

            RuntimeComponentDescriptor descriptor = context.InspectComponent();

            Assert.That(descriptor.Id.IsValid, Is.True);
            Assert.That(descriptor.TypeName, Is.EqualTo(componentType.FullName));
            Assert.That(descriptor.HasEnabledState, Is.EqualTo(hasEnabledState));
            if (hasEnabledState) Assert.That(descriptor.Enabled, Is.True);
            Assert.That(descriptor.StatusMessage, Is.Null);

            foreach (string displayName in expectedDisplayNames)
            {
                RuntimeMemberDescriptor member = GetMember(descriptor, displayName);
                Assert.That(member.Name, Is.Not.Null.And.Not.Empty);
                Assert.That(member.ReadOnly, Is.False);
                Assert.That(member.Error, Is.Null);
            }
        }

        [TestCaseSource(nameof(ColliderSubtypeCases))]
        public void Service_ExposesUniqueMemberIdsAndShapeMembersForColliderSubtype(Type componentType,
            string[] expectedShapeMembers)
        {
            using var context = new ComponentContext(componentType);
            RuntimeComponentDescriptor descriptor = context.InspectComponent();
            string[] memberIds = descriptor.Members.Select(member => member.Name).ToArray();

            Assert.That(memberIds.All(id => !string.IsNullOrEmpty(id)), Is.True,
                $"{componentType.Name} contains an empty runtime member ID.");
            Assert.That(memberIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(memberIds.Length),
                $"{componentType.Name} contains duplicate runtime member IDs.");
            foreach (string displayName in expectedShapeMembers)
                GetMember(descriptor, displayName);
        }

        [TestCase(typeof(Rigidbody), "Mass", "2.5")]
        [TestCase(typeof(Rigidbody2D), "Gravity Scale", "-0.5")]
        [TestCase(typeof(BoxCollider), "Is Trigger", "true")]
        [TestCase(typeof(BoxCollider), "Size", "2, 3, 4")]
        [TestCase(typeof(BoxCollider2D), "Density", "1.75")]
        [TestCase(typeof(BoxCollider2D), "Size", "4, 5")]
        [TestCase(typeof(Light), "Intensity", "3")]
        [TestCase(typeof(Animator), "Speed", "0.5")]
        public void Service_EditsBuiltInMemberAndReportsUpdatedValue(Type componentType, string displayName,
            string input)
        {
            using var context = new ComponentContext(componentType);
            RuntimeComponentDescriptor descriptor = context.InspectComponent();
            RuntimeMemberDescriptor member = GetMember(descriptor, displayName);
            string valueBeforeEdit = member.Value;

            RuntimeCommandResult result = context.Service.Execute(new SetMemberValueCommand
            {
                ComponentId = descriptor.Id,
                MemberName = member.Name,
                Value = input
            });

            Assert.That(result.Success, Is.True, result.Message);
            AssertEditedValue(context.Component, displayName);
            RuntimeMemberDescriptor updatedMember = GetMember(context.InspectComponent(), displayName);
            Assert.That(updatedMember.Value, Is.Not.EqualTo(valueBeforeEdit));
        }

        [Test]
        public void Service_RejectsUnknownBuiltInMemberWithoutChangingComponent()
        {
            using var context = new ComponentContext(typeof(Rigidbody));
            var rigidbody = (Rigidbody)context.Component;
            RuntimeComponentDescriptor descriptor = context.InspectComponent();
            float massBeforeEdit = rigidbody.mass;

            RuntimeCommandResult result = context.Service.Execute(new SetMemberValueCommand
            {
                ComponentId = descriptor.Id,
                MemberName = "__unknown_runtime_debugger_member__",
                Value = "2.5"
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.Not.Null.And.Not.Empty);
            Assert.That(rigidbody.mass, Is.EqualTo(massBeforeEdit));
        }

        [Test]
        public void Service_RejectsInvalidBuiltInValueWithoutChangingComponent()
        {
            using var context = new ComponentContext(typeof(Rigidbody));
            var rigidbody = (Rigidbody)context.Component;
            RuntimeComponentDescriptor descriptor = context.InspectComponent();
            RuntimeMemberDescriptor massMember = GetMember(descriptor, "Mass");
            float massBeforeEdit = rigidbody.mass;

            RuntimeCommandResult result = context.Service.Execute(new SetMemberValueCommand
            {
                ComponentId = descriptor.Id,
                MemberName = massMember.Name,
                Value = "not-a-number"
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.Not.Null.And.Not.Empty);
            Assert.That(rigidbody.mass, Is.EqualTo(massBeforeEdit));
        }

        [Test]
        public void Service_RejectsNonPositiveRigidbodyMassWithoutChangingComponentOrReadback()
        {
            using var context = new ComponentContext(typeof(Rigidbody));
            var rigidbody = (Rigidbody)context.Component;
            RuntimeComponentDescriptor descriptor = context.InspectComponent();
            RuntimeMemberDescriptor massMember = GetMember(descriptor, "Mass");
            float massBeforeEdit = rigidbody.mass;

            RuntimeCommandResult result = context.Service.Execute(new SetMemberValueCommand
            {
                ComponentId = descriptor.Id,
                MemberName = massMember.Name,
                Value = "0"
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("greater than zero"));
            Assert.That(rigidbody.mass, Is.EqualTo(massBeforeEdit));
            Assert.That(GetMember(context.InspectComponent(), "Mass").Value, Is.EqualTo(massMember.Value));
        }

        [Test]
        public void Service_TreatsAnimatorApplyRootMotionAsReadOnlyAndRejectsMutation()
        {
            using var context = new ComponentContext(typeof(Animator));
            var animator = (Animator)context.Component;
            RuntimeComponentDescriptor descriptor = context.InspectComponent();
            RuntimeMemberDescriptor member = GetMember(descriptor, "Apply Root Motion");
            bool valueBeforeEdit = animator.applyRootMotion;

            RuntimeCommandResult result = context.Service.Execute(new SetMemberValueCommand
            {
                ComponentId = descriptor.Id,
                MemberName = member.Name,
                Value = valueBeforeEdit ? "false" : "true"
            });

            Assert.That(member.ReadOnly, Is.True);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("read-only"));
            Assert.That(animator.applyRootMotion, Is.EqualTo(valueBeforeEdit));
        }

        private static TestCaseData SupportedComponent<T>(bool hasEnabledState, params string[] displayNames)
            where T : Component => new TestCaseData(typeof(T), hasEnabledState, displayNames)
            .SetName($"Service_Exposes_{typeof(T).Name}_BuiltInMembers");

        private static TestCaseData ColliderSubtype<T>(params string[] displayNames) where T : Component =>
            new TestCaseData(typeof(T), displayNames)
                .SetName($"Service_Exposes_{typeof(T).Name}_ShapeMembersWithUniqueIds");

        private static RuntimeMemberDescriptor GetMember(RuntimeComponentDescriptor descriptor, string displayName)
        {
            List<RuntimeMemberDescriptor> matches = descriptor.Members
                .Where(member => member.DisplayName == displayName).ToList();
            Assert.That(matches, Has.Count.EqualTo(1),
                $"Expected exactly one '{displayName}' member on {descriptor.TypeName}.");
            return matches[0];
        }

        private static void AssertEditedValue(Component component, string displayName)
        {
            switch (component)
            {
                case Rigidbody rigidbody when displayName == "Mass":
                    Assert.That(rigidbody.mass, Is.EqualTo(2.5f).Within(0.0001f));
                    break;
                case Rigidbody2D rigidbody2D when displayName == "Gravity Scale":
                    Assert.That(rigidbody2D.gravityScale, Is.EqualTo(-0.5f).Within(0.0001f));
                    break;
                case BoxCollider boxCollider when displayName == "Is Trigger":
                    Assert.That(boxCollider.isTrigger, Is.True);
                    break;
                case BoxCollider boxCollider when displayName == "Size":
                    Assert.That(boxCollider.size, Is.EqualTo(new Vector3(2f, 3f, 4f)));
                    break;
                case BoxCollider2D boxCollider2D when displayName == "Density":
                    Assert.That(boxCollider2D.density, Is.EqualTo(1.75f).Within(0.0001f));
                    break;
                case BoxCollider2D boxCollider2D when displayName == "Size":
                    Assert.That(boxCollider2D.size, Is.EqualTo(new Vector2(4f, 5f)));
                    break;
                case Light light when displayName == "Intensity":
                    Assert.That(light.intensity, Is.EqualTo(3f).Within(0.0001f));
                    break;
                case Animator animator when displayName == "Speed":
                    Assert.That(animator.speed, Is.EqualTo(0.5f).Within(0.0001f));
                    break;
                default:
                    Assert.Fail($"No edited-value assertion is defined for {component.GetType().Name}.{displayName}.");
                    break;
            }
        }

        private sealed class ComponentContext : IDisposable
        {
            private bool _disposed;

            public ComponentContext(Type componentType)
            {
                Settings = ScriptableObject.CreateInstance<RuntimeDebuggerSettings>();
                GameObject = new GameObject($"RuntimeDebuggerDrawerTest_{Guid.NewGuid():N}");
                try
                {
                    Component = GameObject.AddComponent(componentType);
                    Service = new RuntimeDebuggerService(Settings);
                    ObjectId = Service.GetHierarchySnapshot().Entries.Single(entry =>
                        entry.Kind == RuntimeHierarchyKind.GameObject && entry.Name == GameObject.name).Id;
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public RuntimeDebuggerSettings Settings { get; }
            public GameObject GameObject { get; }
            public Component Component { get; private set; }
            public RuntimeDebuggerService Service { get; private set; }
            public RuntimeObjectId ObjectId { get; private set; }

            public RuntimeComponentDescriptor InspectComponent()
            {
                RuntimeObjectDetails details = Service.InspectObject(ObjectId);
                Assert.That(details, Is.Not.Null);
                List<RuntimeComponentDescriptor> matches = details.Components
                    .Where(descriptor => descriptor.TypeName == Component.GetType().FullName).ToList();
                Assert.That(matches, Has.Count.EqualTo(1),
                    $"Expected exactly one {Component.GetType().Name} descriptor.");
                return matches[0];
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Service?.Dispose();
                if (GameObject != null) Object.DestroyImmediate(GameObject);
                if (Settings != null) Object.DestroyImmediate(Settings);
                Component = null;
                Service = null;
            }
        }
    }
}
