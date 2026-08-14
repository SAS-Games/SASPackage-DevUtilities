using System;
using System.Collections.Generic;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands;
using SAS.Utilities.RemoteDevUtilities.Editor.Logging;
using SAS.Utilities.RemoteDevUtilities.Editor.Logging.Settings;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Logging;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class RemoteLoggingCommandBuilderTests
    {
        [TestCase((int)RemoteLoggingLevel.Info, true, "Logging.LogLevel Info On")]
        [TestCase((int)RemoteLoggingLevel.Warning, false, "Logging.LogLevel Warning Off")]
        [TestCase((int)RemoteLoggingLevel.Error, true, "Logging.LogLevel Error On")]
        public void SetLogLevel_UsesLoggingPresetSyntax(int level, bool enabled, string expected)
        {
            Assert.That(RemoteLoggingCommandBuilder.SetLogLevel((RemoteLoggingLevel)level, enabled), Is.EqualTo(expected));
        }

        [TestCase((int)RemoteStackTraceTarget.All, StackTraceLogType.None, "Logging.SetStackTrace All 0")]
        [TestCase((int)RemoteStackTraceTarget.Log, StackTraceLogType.ScriptOnly, "Logging.SetStackTrace Log 1")]
        [TestCase((int)RemoteStackTraceTarget.Exception, StackTraceLogType.Full, "Logging.SetStackTrace Exception 2")]
        public void SetStackTrace_UsesNumericPresetMode(int target, StackTraceLogType mode, string expected)
        {
            Assert.That(RemoteLoggingCommandBuilder.SetStackTrace((RemoteStackTraceTarget)target, mode), Is.EqualTo(expected));
        }

        [Test]
        public void SetTags_TrimsAndDeduplicatesTags()
        {
            bool success = RemoteLoggingCommandBuilder.TrySetTags(new[] { " Gameplay ", "UI", "gameplay", string.Empty }, out string command, out string[] normalizedTags, out string error);

            Assert.That(success, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(normalizedTags, Is.EqualTo(new[] { "Gameplay", "UI" }));
            Assert.That(command, Is.EqualTo("Logging.SetTags Gameplay|UI"));
        }

        [Test]
        public void SetTags_UsesClearCommandForEmptyList()
        {
            Assert.That(RemoteLoggingCommandBuilder.TrySetTags(new string[0], out string command, out string[] normalizedTags, out string error), Is.True);
            Assert.That(command, Is.EqualTo(RemoteLoggingCommandBuilder.ClearTagsCommand));
            Assert.That(normalizedTags, Is.Empty);
            Assert.That(error, Is.Null);
        }

        [TestCase("Player State")]
        [TestCase("Gameplay|UI")]
        public void SetTags_RejectsValuesThatBreakCommandArguments(string tag)
        {
            Assert.That(RemoteLoggingCommandBuilder.TrySetTags(new[] { tag }, out string command, out string[] normalizedTags, out string error), Is.False);
            Assert.That(command, Is.Null);
            Assert.That(normalizedTags, Is.Empty);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void Availability_RequiresLoggingCommandInTargetCatalog()
        {
            Assert.That(RemoteLoggingCommandBuilder.IsAvailable(new StubCommands("GameInfo", "Logging")), Is.True);
            Assert.That(RemoteLoggingCommandBuilder.IsAvailable(new StubCommands("GameInfo")), Is.False);
            Assert.That(RemoteLoggingCommandBuilder.IsAvailable(null), Is.False);
        }

        [Test]
        public void TargetSettingsResponse_UpdatesLogLevelStatus()
        {
            var session = new StubSession();
            var client = new RemoteLogClient(session);

            client.RequestSettings();

            Assert.That(session.LastMessageType, Is.EqualTo(RemoteLoggingMessageTypes.SettingsRequest));

            client.Handle(new RemoteEnvelope
            {
                MessageType = RemoteLoggingMessageTypes.SettingsResponse,
                PayloadJson = JsonUtility.ToJson(new RemoteLogSettingsResponse
                {
                    InfoEnabled = true,
                    WarningEnabled = false,
                    ErrorEnabled = true
                })
            });

            Assert.That(client.HasTargetSettings, Is.True);
            Assert.That(client.InfoEnabled, Is.True);
            Assert.That(client.WarningEnabled, Is.False);
            Assert.That(client.ErrorEnabled, Is.True);
            Assert.That(session.StateChangeCount, Is.EqualTo(1));
        }

        private sealed class StubSession : IRemoteEditorSession
        {
            internal string LastMessageType;
            internal int StateChangeCount;

            public bool IsConnected => true;

            public long Send<T>(string messageType, T payload)
            {
                LastMessageType = messageType;
                return 1;
            }

            public void NotifyStateChanged() => StateChangeCount++;
        }

        private sealed class StubCommands : IRemoteCommandExecutor
        {
            private readonly HashSet<string> _commands;

            internal StubCommands(params string[] commands) =>
                _commands = new HashSet<string>(commands ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            public IEnumerable<string> MessageTypes => Array.Empty<string>();
            public string Error => null;
            public RemoteCommandExecutionResult ExecutionResult => null;
            public long ExecutionResultRequestId => 0;
            public bool HasCommand(string commandName) => _commands.Contains(commandName);
            public void RequestCatalog() { }
            public long Execute(string commandLine) => 0;
            public void OnConnected() { }
            public void Handle(RemoteEnvelope envelope) { }
            public void Reset() { }
        }
    }
}
