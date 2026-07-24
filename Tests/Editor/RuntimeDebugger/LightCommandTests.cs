using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SAS.Utilities.DeveloperConsole;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RuntimeDebugger.Tests
{
    public sealed class LightCommandTests
    {
        private readonly List<GameObject> _objects = new();
        private LightCommand _command;

        [SetUp]
        public void SetUp()
        {
            _command = ScriptableObject.CreateInstance<LightCommand>();
            FieldInfo subCommandsField = typeof(CompositeConsoleCommand).GetField(
                "m_SubCommands",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(subCommandsField, Is.Not.Null);
            subCommandsField.SetValue(_command, new List<CompositeConsoleCommand.SubCommand>
            {
                SubCommand("SetAll", "SetAll"),
                SubCommand("Cull", "CullLightsByVisibility"),
                SubCommand("Restore", "Restore"),
                SubCommand("Offset", "OffsetLights"),
                SubCommand("ResetOffset", "ResetOffset"),
                SubCommand("Reset", "Reset")
            });
            _command.Init();
        }

        [TearDown]
        public void TearDown()
        {
            if (_command != null)
            {
                Execute("Reset");
                Object.DestroyImmediate(_command);
            }

            foreach (GameObject gameObject in _objects)
            {
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
            }

            _objects.Clear();
        }

        [Test]
        public void CommandAsset_RegistersEveryConfiguredSubCommand()
        {
            const string assetPath =
                "Packages/com.sas.dev-utilities/Runtime/MiniTools/Light/Assets/Light Command.asset";
            LightCommand commandAsset = AssetDatabase.LoadAssetAtPath<LightCommand>(assetPath);
            Assert.That(commandAsset, Is.Not.Null, $"Could not load {assetPath}");

            commandAsset.Init();
            FieldInfo subCommandsField = typeof(CompositeConsoleCommand).GetField(
                "m_SubCommands",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var subCommands = (List<CompositeConsoleCommand.SubCommand>)subCommandsField?.GetValue(commandAsset);

            Assert.That(subCommands, Is.Not.Null);
            Assert.That(subCommands, Has.Count.EqualTo(7));
            Assert.That(subCommands.All(subCommand => subCommand.Action != null), Is.True,
                "Every Light subcommand asset entry must resolve to a valid method.");
        }

        [Test]
        public void SetAllThenRestore_PreservesEachOriginalEnabledState()
        {
            Light originallyEnabled = CreateLight("Originally Enabled", LightType.Point, true);
            Light originallyDisabled = CreateLight("Originally Disabled", LightType.Point, false);

            Assert.That(Execute("SetAll", "Off", "point"), Is.True);
            Assert.That(originallyEnabled.enabled, Is.False);
            Assert.That(originallyDisabled.enabled, Is.False);

            Assert.That(Execute("SetAll", "On", "point"), Is.True);
            Assert.That(originallyEnabled.enabled, Is.True);
            Assert.That(originallyDisabled.enabled, Is.True);

            Assert.That(Execute("Restore"), Is.True);
            Assert.That(originallyEnabled.enabled, Is.True);
            Assert.That(originallyDisabled.enabled, Is.False);
        }

        [Test]
        public void OffsetWithTypeFilter_OnlyMovesMatchingLightsAndCanReset()
        {
            Light point = CreateLight("Point", LightType.Point, true);
            Light spot = CreateLight("Spot", LightType.Spot, true);
            point.transform.position = new Vector3(1f, 2f, 3f);
            spot.transform.position = new Vector3(4f, 5f, 6f);

            Assert.That(Execute("Offset", "10", "20", "30", "point"), Is.True);
            Assert.That(point.transform.position, Is.EqualTo(new Vector3(11f, 22f, 33f)));
            Assert.That(spot.transform.position, Is.EqualTo(new Vector3(4f, 5f, 6f)));

            Assert.That(Execute("ResetOffset"), Is.True);
            Assert.That(point.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(spot.transform.position, Is.EqualTo(new Vector3(4f, 5f, 6f)));
        }

        [Test]
        public void CullOffscreenThenRestore_OnlyChangesTheOffscreenLight()
        {
            Camera[] existingCameras = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var previousEnabledStates = new bool[existingCameras.Length];
            for (int i = 0; i < existingCameras.Length; i++)
            {
                previousEnabledStates[i] = existingCameras[i].enabled;
                existingCameras[i].enabled = false;
            }

            try
            {
                GameObject cameraObject = CreateObject("Light Command Test Camera");
                cameraObject.tag = "MainCamera";
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                Assert.That(Camera.main, Is.SameAs(camera),
                    "The culling command must use the camera created by this test.");

                Light onscreen = CreateLight("Onscreen", LightType.Point, true);
                onscreen.range = 0.5f;
                onscreen.transform.position = new Vector3(0f, 0f, 5f);

                Light offscreen = CreateLight("Offscreen", LightType.Point, true);
                offscreen.range = 0.5f;
                offscreen.transform.position = new Vector3(10000f, 0f, 5f);

                Assert.That(Execute("Cull", "offscreen", "Off", "point"), Is.True);
                Assert.That(onscreen.enabled, Is.True);
                Assert.That(offscreen.enabled, Is.False);

                Assert.That(Execute("Restore"), Is.True);
                Assert.That(onscreen.enabled, Is.True);
                Assert.That(offscreen.enabled, Is.True);
            }
            finally
            {
                for (int i = 0; i < existingCameras.Length; i++)
                {
                    if (existingCameras[i] != null)
                        existingCameras[i].enabled = previousEnabledStates[i];
                }
            }
        }

        private static CompositeConsoleCommand.SubCommand SubCommand(string name, string methodName) => new()
        {
            Name = name,
            MethodName = methodName
        };

        private bool Execute(string subCommand, params string[] args) =>
            _command.Process(null, $"Light.{subCommand}", args);

        private Light CreateLight(string name, LightType type, bool enabled)
        {
            GameObject gameObject = CreateObject(name);
            Light light = gameObject.AddComponent<Light>();
            light.type = type;
            light.enabled = enabled;
            return light;
        }

        private GameObject CreateObject(string name)
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            return gameObject;
        }
    }
}
