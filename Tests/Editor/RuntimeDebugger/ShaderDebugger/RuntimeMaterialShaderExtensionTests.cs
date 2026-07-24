using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SAS.Utilities.RuntimeDebugger.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SAS.Utilities.RuntimeDebugger.Tests
{
    public sealed class RuntimeMaterialShaderExtensionTests
    {
        [Test]
        public void Service_EditsAndRestoresShaderValueWithRendererPropertyBlock()
        {
            using var context = new MaterialContext();
            RuntimeShaderPropertyView property = context.GetEditableNumericProperty();
            string originalValue = property.Value;

            RuntimeCommandResult setResult = context.Service.Execute(new SetRuntimeShaderPropertyCommand
            {
                RendererId = context.RendererDescriptor.RendererId,
                MaterialIndex = 0,
                PropertyId = property.Property.PropertyId,
                Scope = RuntimeMaterialEditScope.RendererPropertyBlock,
                Value = ChangedValue(property)
            });

            Assert.That(setResult.Success, Is.True, setResult.Message);
            Assert.That(context.Renderer.sharedMaterials[0], Is.SameAs(context.Material),
                "A property-block edit must not instantiate or replace the material.");

            RuntimeShaderPropertyView changed = context.GetProperty(property.Property.PropertyId);
            Assert.That(changed.HasDebuggerOverride, Is.True);
            Assert.That(changed.ValueSource, Does.Contain("Debugger"));
            Assert.That(changed.Value, Is.Not.EqualTo(originalValue));

            RuntimeCommandResult restoreResult = context.Service.Execute(new RestoreRuntimeShaderPropertyCommand
            {
                RendererId = context.RendererDescriptor.RendererId,
                MaterialIndex = 0,
                PropertyId = property.Property.PropertyId,
                Scope = RuntimeMaterialEditScope.RendererPropertyBlock
            });

            Assert.That(restoreResult.Success, Is.True, restoreResult.Message);
            RuntimeShaderPropertyView restored = context.GetProperty(property.Property.PropertyId);
            Assert.That(restored.HasDebuggerOverride, Is.False);
            Assert.That(restored.Value, Is.EqualTo(originalValue));
        }

        [Test]
        public void Service_CreatesOneOwnedMaterialInstanceAndRestoresOriginalSlot()
        {
            using var context = new MaterialContext();
            RuntimeShaderPropertyView property = context.GetEditableNumericProperty();

            RuntimeCommandResult firstSet = context.Service.Execute(new SetRuntimeShaderPropertyCommand
            {
                RendererId = context.RendererDescriptor.RendererId,
                MaterialIndex = 0,
                PropertyId = property.Property.PropertyId,
                Scope = RuntimeMaterialEditScope.MaterialInstance,
                Value = ChangedValue(property)
            });
            Material instance = context.Renderer.sharedMaterials[0];

            Assert.That(firstSet.Success, Is.True, firstSet.Message);
            Assert.That(instance, Is.Not.SameAs(context.Material));
            Assert.That(instance.name, Does.Contain("Runtime Debugger"));

            RuntimeCommandResult secondSet = context.Service.Execute(new SetRuntimeShaderPropertyCommand
            {
                RendererId = context.RendererDescriptor.RendererId,
                MaterialIndex = 0,
                PropertyId = property.Property.PropertyId,
                Scope = RuntimeMaterialEditScope.MaterialInstance,
                Value = ChangedValue(property)
            });

            Assert.That(secondSet.Success, Is.True, secondSet.Message);
            Assert.That(context.Renderer.sharedMaterials[0], Is.SameAs(instance),
                "Subsequent edits must reuse the debugger-owned instance.");

            RuntimeCommandResult restore = context.Service.Execute(new RestoreRuntimeMaterialCommand
            {
                RendererId = context.RendererDescriptor.RendererId,
                MaterialIndex = 0,
                Scope = RuntimeMaterialEditScope.MaterialInstance
            });

            Assert.That(restore.Success, Is.True, restore.Message);
            Assert.That(context.Renderer.sharedMaterials[0], Is.SameAs(context.Material));
        }

        [Test]
        public void Service_RejectsSharedMaterialChangesByDefault()
        {
            using var context = new MaterialContext();
            RuntimeShaderPropertyView property = context.GetEditableNumericProperty();
            string before = property.Value;

            RuntimeCommandResult result = context.Service.Execute(new SetRuntimeShaderPropertyCommand
            {
                RendererId = context.RendererDescriptor.RendererId,
                MaterialIndex = 0,
                PropertyId = property.Property.PropertyId,
                Scope = RuntimeMaterialEditScope.SharedMaterial,
                Value = ChangedValue(property)
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("disabled"));
            Assert.That(context.GetProperty(property.Property.PropertyId).Value, Is.EqualTo(before));
        }

        [Test]
        public void InspectorNavigation_TraversesFromComponentsIntoShaderProperties()
        {
            using var context = new MaterialContext();
            context.GetEditableNumericProperty();

            Assembly assembly = typeof(RuntimeDebuggerService).Assembly;
            Type controllerType = assembly.GetType(
                "SAS.Utilities.RuntimeDebugger.RuntimeDebuggerInspectorController", true);
            Type navigationType = assembly.GetType(
                "SAS.Utilities.RuntimeDebugger.RuntimeDebuggerNavigationCommand", true);
            object controller = Activator.CreateInstance(controllerType,
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { context.Service, context.Settings }, null);

            MethodInfo select = controllerType.GetMethod("Select",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo navigate = controllerType.GetMethod("Navigate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo getRows = controllerType.GetMethod("GetRows",
                BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo cursorProperty = controllerType.GetProperty("Cursor",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(select, Is.Not.Null);
            Assert.That(navigate, Is.Not.Null);
            Assert.That(getRows, Is.Not.Null);
            Assert.That(cursorProperty, Is.Not.Null);

            select.Invoke(controller, new object[] { context.ObjectId });
            var rows = (IList)getRows.Invoke(controller, null);
            FieldInfo kindField = rows[0].GetType().GetField("Kind",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(kindField, Is.Not.Null);

            int shaderPropertyIndex = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(kindField.GetValue(rows[i]).ToString(), "ShaderProperty",
                        StringComparison.Ordinal))
                {
                    shaderPropertyIndex = i;
                    break;
                }
            }

            Assert.That(shaderPropertyIndex, Is.GreaterThan(0),
                "Shader properties must be appended to the keyboard-navigable inspector rows.");

            object down = Enum.Parse(navigationType, "Down");
            for (int i = 0; i < shaderPropertyIndex; i++)
                navigate.Invoke(controller, new[] { down });
            Assert.That((int)cursorProperty.GetValue(controller), Is.EqualTo(shaderPropertyIndex));

            object left = Enum.Parse(navigationType, "Left");
            navigate.Invoke(controller, new[] { left });
            int slotIndex = (int)cursorProperty.GetValue(controller);
            Assert.That(kindField.GetValue(rows[slotIndex]).ToString(), Is.EqualTo("MaterialSlot"));

            object right = Enum.Parse(navigationType, "Right");
            navigate.Invoke(controller, new[] { right });
            Assert.That((int)cursorProperty.GetValue(controller), Is.EqualTo(shaderPropertyIndex),
                "Right on an expanded material slot should enter its first shader property.");
        }

        private static string ChangedValue(RuntimeShaderPropertyView view)
        {
            switch (view.Property.Type)
            {
                case RuntimeShaderPropertyType.Float:
                case RuntimeShaderPropertyType.Range:
                    float scalar = float.Parse(view.Value, CultureInfo.InvariantCulture);
                    return (scalar + 0.375f).ToString("R", CultureInfo.InvariantCulture);
                case RuntimeShaderPropertyType.Integer:
                    int integer = int.Parse(view.Value, CultureInfo.InvariantCulture);
                    return (integer + 1).ToString(CultureInfo.InvariantCulture);
                case RuntimeShaderPropertyType.Color:
                    return "0.137, 0.271, 0.419, 0.863";
                case RuntimeShaderPropertyType.Vector:
                    return "0.137, 0.271, 0.419, 0.863";
                default:
                    throw new AssertionException("The selected property is not numeric.");
            }
        }

        private sealed class MaterialContext : IDisposable
        {
            private bool _disposed;

            internal MaterialContext()
            {
                Settings = ScriptableObject.CreateInstance<RuntimeDebuggerSettings>();
                GameObject = new GameObject("RuntimeMaterialShaderExtensionTest_" + Guid.NewGuid().ToString("N"));
                Renderer = GameObject.AddComponent<MeshRenderer>();
                Shader shader = FindTestShader();
                if (shader == null)
                {
                    Dispose();
                    Assert.Ignore("No supported runtime shader was found in this project.");
                }

                Material = new Material(shader);
                Renderer.sharedMaterial = Material;
                Service = new RuntimeDebuggerService(Settings);
                RuntimeHierarchyEntry entry = Service.GetHierarchySnapshot().Entries.Single(item =>
                    item.Kind == RuntimeHierarchyKind.GameObject && item.Name == GameObject.name);
                ObjectId = entry.Id;
                RendererDescriptor = InspectRenderer();
            }

            internal RuntimeDebuggerSettings Settings { get; }
            internal GameObject GameObject { get; }
            internal MeshRenderer Renderer { get; }
            internal Material Material { get; private set; }
            internal RuntimeDebuggerService Service { get; private set; }
            internal RuntimeObjectId ObjectId { get; }
            internal RuntimeRendererMaterialDescriptor RendererDescriptor { get; private set; }

            internal RuntimeShaderPropertyView GetEditableNumericProperty()
            {
                RuntimeShaderPropertyView property = RendererDescriptor.MaterialSlots[0].Properties.FirstOrDefault(
                    item => !item.ReadOnly && item.Property.Type != RuntimeShaderPropertyType.Texture &&
                            item.Property.Type != RuntimeShaderPropertyType.Unsupported);
                if (property == null)
                    Assert.Ignore("The selected shader exposes no editable numeric properties.");
                return property;
            }

            internal RuntimeShaderPropertyView GetProperty(int propertyId)
            {
                RendererDescriptor = InspectRenderer();
                return RendererDescriptor.MaterialSlots[0].Properties.Single(item =>
                    item.Property.PropertyId == propertyId);
            }

            private RuntimeRendererMaterialDescriptor InspectRenderer()
            {
                RuntimeObjectDetails details = Service.InspectObject(ObjectId);
                Assert.That(details, Is.Not.Null);
                Assert.That(details.MaterialsAndShaders, Is.Not.Null);
                return details.MaterialsAndShaders.Renderers.Single();
            }

            private static Shader FindTestShader()
            {
                string[] candidates =
                {
                    "Universal Render Pipeline/Lit",
                    "Standard",
                    "Unlit/Color",
                    "Sprites/Default"
                };
                foreach (string candidate in candidates)
                {
                    Shader shader = Shader.Find(candidate);
                    if (shader != null && shader.GetPropertyCount() > 0)
                        return shader;
                }

                return null;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                Service?.Dispose();
                if (GameObject != null)
                    Object.DestroyImmediate(GameObject);
                if (Material != null)
                    Object.DestroyImmediate(Material);
                if (Settings != null)
                    Object.DestroyImmediate(Settings);
                Service = null;
                Material = null;
            }
        }
    }
}
