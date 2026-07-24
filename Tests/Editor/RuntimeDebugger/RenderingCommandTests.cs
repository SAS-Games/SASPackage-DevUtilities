using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SAS.Utilities.DeveloperConsole;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SAS.Utilities.RuntimeDebugger.Tests
{
    public sealed class RenderingCommandTests
    {
        private const string PackageRoot = "Packages/com.sas.dev-utilities/Runtime";

        [TestCase("URP/Commands/Assets/URP Quality Console Command.asset", 9)]
        [TestCase("URP/Commands/Assets/URP Shadow Console Command.asset", 11)]
        [TestCase("URP/Commands/Assets/URP Post-processing Console Command.asset", 7)]
        [TestCase("MiniTools/Rendering/Assets/Rendering Command.asset", 10)]
        [TestCase("MiniTools/Rendering/Assets/Camera Command.asset", 8)]
        [TestCase("MiniTools/Commands/Assets/Display Console Command PC.asset", 5)]
        [TestCase("MiniTools/Commands/Assets/Display Console Command PS.asset", 3)]
        public void CommandAsset_RegistersEveryConfiguredSubCommand(string relativePath, int expectedCount)
        {
            CompositeConsoleCommand command = AssetDatabase.LoadAssetAtPath<CompositeConsoleCommand>($"{PackageRoot}/{relativePath}");
            Assert.That(command, Is.Not.Null, $"Could not load {relativePath}");

            command.Init();
            List<CompositeConsoleCommand.SubCommand> subCommands = GetSubCommands(command);

            Assert.That(subCommands, Has.Count.EqualTo(expectedCount));
            Assert.That(subCommands.All(subCommand => subCommand.Action != null), Is.True,
                "Every configured rendering subcommand must resolve to a compatible method.");
        }

        [Test]
        public void UrpQuality_RejectsInvalidValuesWithoutClampingThem()
        {
            CompositeConsoleCommand command = AssetDatabase.LoadAssetAtPath<CompositeConsoleCommand>(
                $"{PackageRoot}/URP/Commands/Assets/URP Quality Console Command.asset");
            command.Init();

            Assert.That(command.Process(null, "Quality.SetMSAA", new[] { "3" }), Is.False);
            Assert.That(command.Process(null, "Quality.SetRenderScale", new[] { "3" }), Is.False);
            Assert.That(command.Process(null, "Quality.TextureQuality", new[] { "4" }), Is.False);
            Assert.That(command.Process(null, "Quality.Restore", Array.Empty<string>()), Is.True);
        }

        [Test]
        public void RenderingReports_AcceptDefaultAndBoundedTopCounts()
        {
            CompositeConsoleCommand command = AssetDatabase.LoadAssetAtPath<CompositeConsoleCommand>(
                $"{PackageRoot}/MiniTools/Rendering/Assets/Rendering Command.asset");
            command.Init();

            Assert.That(command.Process(null, "Rendering.Textures", Array.Empty<string>()), Is.True);
            Assert.That(command.Process(null, "Rendering.Materials", new[] { "5" }), Is.True);
            Assert.That(command.Process(null, "Rendering.RenderTargets", new[] { "0" }), Is.False);
            Assert.That(command.Process(null, "Rendering.Shaders", new[] { "extra" }), Is.False);
        }

        [Test]
        public void DefaultConsolePrefab_RegistersRenderingAndCameraCommands()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PackageRoot}/DeveloperConsole/Assets/Resources/ConsoleCommandsSystem.prefab");
            Assert.That(prefab, Is.Not.Null);
            DeveloperConsoleBehaviour console = prefab.GetComponentInChildren<DeveloperConsoleBehaviour>(true);
            Assert.That(console, Is.Not.Null);

            FieldInfo field = typeof(DeveloperConsoleBehaviour).GetField("m_Commands", BindingFlags.Instance | BindingFlags.NonPublic);
            ConsoleCommand[] commands = (ConsoleCommand[])field?.GetValue(console);
            Assert.That(commands, Is.Not.Null);
            Assert.That(commands.Any(command => command != null && command.Name == "Rendering"), Is.True);
            Assert.That(commands.Any(command => command != null && command.Name == "Camera"), Is.True);
        }

        [Test]
        public void SetCanvasDisplay_RejectsNegativeDisplayIndexWithoutIndexingDisplayArray()
        {
            SetDisplayCommand command = AssetDatabase.LoadAssetAtPath<SetDisplayCommand>(
                $"{PackageRoot}/MiniTools/Commands/Assets/SetDisplay Command.asset");
            Assert.That(command, Is.Not.Null);
            LogAssert.Expect(LogType.Error, new Regex($"Display -1 not available\\. Total displays: {Display.displays.Length}"));
            Assert.That(command.Process(null, "SetCanvasDisplay", new[] { "Debug Canvas", "-1" }), Is.False);
        }

        [Test]
        public void GraphicsInfo_OffBeforeCreation_IsSuccessfulNoOp()
        {
            GraphicsInfoCommand command = AssetDatabase.LoadAssetAtPath<GraphicsInfoCommand>(
                $"{PackageRoot}/MiniTools/GraphicsInfo/Assets/GraphicsInfoCommand.asset");
            Assert.That(command, Is.Not.Null);
            Assert.That(command.Process(null, "GraphicsInfo", new[] { "Off" }), Is.True);
        }

        private static List<CompositeConsoleCommand.SubCommand> GetSubCommands(CompositeConsoleCommand command)
        {
            FieldInfo field = typeof(CompositeConsoleCommand).GetField("m_SubCommands", BindingFlags.Instance | BindingFlags.NonPublic);
            return (List<CompositeConsoleCommand.SubCommand>)field?.GetValue(command);
        }
    }
}
