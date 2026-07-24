using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SAS.Utilities.DeveloperConsole;
using UnityEditor;

namespace SAS.Utilities.RuntimeDebugger.Tests
{
    public sealed class StatsCommandTests
    {
        private static readonly string[] AssetPaths =
        {
            "Packages/com.sas.dev-utilities/Runtime/MiniTools/FPS/Assets/Show FPS Command.asset",
            "Packages/com.sas.dev-utilities/Runtime/MiniTools/FPS/Assets/Show FPS Command PS.asset"
        };

        [TestCaseSource(nameof(AssetPaths))]
        public void CommandAsset_RegistersEveryConfiguredSubCommand(string assetPath)
        {
            ShowFPSCommand command = AssetDatabase.LoadAssetAtPath<ShowFPSCommand>(assetPath);
            Assert.That(command, Is.Not.Null, $"Could not load {assetPath}");

            command.Init();
            List<CompositeConsoleCommand.SubCommand> subCommands = GetSubCommands(command);

            Assert.That(subCommands, Has.Count.EqualTo(7));
            Assert.That(subCommands.All(subCommand => subCommand.Action != null), Is.True,
                "Every Stats subcommand asset entry must resolve to a valid method.");
        }

        [Test]
        public void OneShotReports_ExecuteWithoutArguments()
        {
            ShowFPSCommand command = AssetDatabase.LoadAssetAtPath<ShowFPSCommand>(AssetPaths[0]);
            Assert.That(command, Is.Not.Null);
            command.Init();

            Assert.That(command.Process(null, "Stats.Memory", Array.Empty<string>()), Is.True);
            Assert.That(command.Process(null, "Stats.SceneRendering", Array.Empty<string>()), Is.True);
            Assert.That(command.Process(null, "Stats.Levels", Array.Empty<string>()), Is.True);
        }

        private static List<CompositeConsoleCommand.SubCommand> GetSubCommands(ShowFPSCommand command)
        {
            FieldInfo field = typeof(CompositeConsoleCommand).GetField(
                "m_SubCommands",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return (List<CompositeConsoleCommand.SubCommand>)field?.GetValue(command);
        }
    }
}
