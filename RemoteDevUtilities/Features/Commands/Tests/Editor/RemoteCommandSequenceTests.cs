using System;
using System.Collections.Generic;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands.Sequences;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class RemoteCommandSequenceTests
    {
        [Test]
        public void Runner_ExecutesCommandsInOrderAndWaitsForEachResult()
        {
            RemoteCommandSequence sequence = CreateSequence(true, "First", "Second");
            var dispatcher = new StubDispatcher();
            using var runner = new RemoteCommandSequenceRunner(dispatcher);

            Assert.That(runner.Start(sequence, out string error), Is.True, error);
            Assert.That(dispatcher.Executed, Is.EqualTo(new[] { "First" }));
            Assert.That(runner.Results[0].State, Is.EqualTo(RemoteCommandSequenceStepState.Running));
            Assert.That(runner.Results[1].State, Is.EqualTo(RemoteCommandSequenceStepState.Pending));

            dispatcher.Complete(1, true, "First complete");

            Assert.That(dispatcher.Executed, Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(runner.Results[0].State, Is.EqualTo(RemoteCommandSequenceStepState.Succeeded));
            Assert.That(runner.Results[1].State, Is.EqualTo(RemoteCommandSequenceStepState.Running));

            dispatcher.Complete(2, true, "Second complete");

            Assert.That(runner.State, Is.EqualTo(RemoteCommandSequenceRunState.Completed));
            Assert.That(runner.Results[1].State, Is.EqualTo(RemoteCommandSequenceStepState.Succeeded));
            UnityEngine.Object.DestroyImmediate(sequence);
        }

        [Test]
        public void Runner_StopOnFailure_SkipsRemainingCommands()
        {
            RemoteCommandSequence sequence = CreateSequence(true, "First", "Second");
            var dispatcher = new StubDispatcher();
            using var runner = new RemoteCommandSequenceRunner(dispatcher);

            Assert.That(runner.Start(sequence, out string error), Is.True, error);
            dispatcher.Complete(1, false, "Rejected");

            Assert.That(runner.State, Is.EqualTo(RemoteCommandSequenceRunState.Failed));
            Assert.That(dispatcher.Executed, Is.EqualTo(new[] { "First" }));
            Assert.That(runner.Results[0].State, Is.EqualTo(RemoteCommandSequenceStepState.Failed));
            Assert.That(runner.Results[1].State, Is.EqualTo(RemoteCommandSequenceStepState.Skipped));
            UnityEngine.Object.DestroyImmediate(sequence);
        }

        [Test]
        public void Runner_ContinueOnFailure_RunsRemainingCommandsAndReportsFailure()
        {
            RemoteCommandSequence sequence = CreateSequence(false, "First", "Second");
            var dispatcher = new StubDispatcher();
            using var runner = new RemoteCommandSequenceRunner(dispatcher);

            Assert.That(runner.Start(sequence, out string error), Is.True, error);
            dispatcher.Complete(1, false, "Rejected");
            dispatcher.Complete(2, true, "Recovered");

            Assert.That(dispatcher.Executed, Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(runner.State, Is.EqualTo(RemoteCommandSequenceRunState.Failed));
            Assert.That(runner.Results[0].State, Is.EqualTo(RemoteCommandSequenceStepState.Failed));
            Assert.That(runner.Results[1].State, Is.EqualTo(RemoteCommandSequenceStepState.Succeeded));
            UnityEngine.Object.DestroyImmediate(sequence);
        }

        [Test]
        public void Runner_Cancel_WaitsForCurrentCommandAndSkipsTheRest()
        {
            RemoteCommandSequence sequence = CreateSequence(true, "First", "Second");
            var dispatcher = new StubDispatcher();
            using var runner = new RemoteCommandSequenceRunner(dispatcher);

            Assert.That(runner.Start(sequence, out string error), Is.True, error);
            runner.Cancel();

            Assert.That(runner.IsCancellationRequested, Is.True);
            Assert.That(runner.Results[0].State, Is.EqualTo(RemoteCommandSequenceStepState.Running));
            dispatcher.Complete(1, true, "First complete");

            Assert.That(runner.State, Is.EqualTo(RemoteCommandSequenceRunState.Cancelled));
            Assert.That(dispatcher.Executed, Is.EqualTo(new[] { "First" }));
            Assert.That(runner.Results[1].State, Is.EqualTo(RemoteCommandSequenceStepState.Skipped));
            UnityEngine.Object.DestroyImmediate(sequence);
        }

        [Test]
        public void Runner_HandlesSynchronousEditorOnlyCommandCompletions()
        {
            RemoteCommandSequence sequence = CreateSequence(true, "First", "Second");
            var dispatcher = new StubDispatcher { CompleteSynchronously = true };
            using var runner = new RemoteCommandSequenceRunner(dispatcher);

            Assert.That(runner.Start(sequence, out string error), Is.True, error);

            Assert.That(dispatcher.Executed, Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(runner.State, Is.EqualTo(RemoteCommandSequenceRunState.Completed));
            UnityEngine.Object.DestroyImmediate(sequence);
        }

        [Test]
        public void Runner_WaitsForSceneCommandAndResumesWhenCatalogChanges()
        {
            RemoteCommandSequence sequence = CreateSequence(
                true,
                new RemoteCommandSequenceStep("First"),
                new RemoteCommandSequenceStep(
                    "Boss.Spawn",
                    true,
                    RemoteCommandUnavailablePolicy.WaitUntilAvailable,
                    5f));
            var dispatcher = new StubDispatcher { AllCommandsAvailable = false };
            dispatcher.MakeAvailable("First", false);
            using var runner = new RemoteCommandSequenceRunner(dispatcher);

            Assert.That(runner.Start(sequence, out string error), Is.True, error);
            dispatcher.Complete(1, true, "First complete");

            Assert.That(dispatcher.Executed, Is.EqualTo(new[] { "First" }));
            Assert.That(runner.Results[1].State, Is.EqualTo(RemoteCommandSequenceStepState.WaitingForCommand));
            Assert.That(dispatcher.CatalogRefreshCount, Is.EqualTo(1));

            dispatcher.MakeAvailable("Boss.Spawn");

            Assert.That(dispatcher.Executed, Is.EqualTo(new[] { "First", "Boss.Spawn" }));
            Assert.That(runner.Results[1].State, Is.EqualTo(RemoteCommandSequenceStepState.Running));
            dispatcher.Complete(2, true, "Boss spawned");
            Assert.That(runner.State, Is.EqualTo(RemoteCommandSequenceRunState.Completed));
            UnityEngine.Object.DestroyImmediate(sequence);
        }

        [Test]
        public void Runner_FailsWhenSceneCommandDoesNotAppearBeforeTimeout()
        {
            RemoteCommandSequence sequence = CreateSequence(
                true,
                new RemoteCommandSequenceStep(
                    "Boss.Spawn",
                    true,
                    RemoteCommandUnavailablePolicy.WaitUntilAvailable,
                    2f));
            var dispatcher = new StubDispatcher { AllCommandsAvailable = false };
            using var runner = new RemoteCommandSequenceRunner(dispatcher);

            Assert.That(runner.Start(sequence, out string error), Is.True, error);
            Assert.That(runner.Results[0].State, Is.EqualTo(RemoteCommandSequenceStepState.WaitingForCommand));
            dispatcher.AdvanceTime(2.1d);

            Assert.That(runner.State, Is.EqualTo(RemoteCommandSequenceRunState.Failed));
            Assert.That(runner.Results[0].State, Is.EqualTo(RemoteCommandSequenceStepState.Failed));
            Assert.That(runner.Results[0].Message, Does.Contain("Timed out"));
            Assert.That(dispatcher.Executed, Is.Empty);
            UnityEngine.Object.DestroyImmediate(sequence);
        }

        [Test]
        public void Runner_FailsImmediatelyWhenUnavailablePolicyDoesNotWait()
        {
            RemoteCommandSequence sequence = CreateSequence(true, "Scene.Reload");
            var dispatcher = new StubDispatcher { AllCommandsAvailable = false };
            using var runner = new RemoteCommandSequenceRunner(dispatcher);

            Assert.That(runner.Start(sequence, out string error), Is.True, error);

            Assert.That(runner.State, Is.EqualTo(RemoteCommandSequenceRunState.Failed));
            Assert.That(runner.Results[0].State, Is.EqualTo(RemoteCommandSequenceStepState.Failed));
            Assert.That(dispatcher.Executed, Is.Empty);
            UnityEngine.Object.DestroyImmediate(sequence);
        }

        [Test]
        public void Validator_AcceptsPrefixAndRejectsCommandsMissingFromPlayerCatalog()
        {
            var catalog = new[]
            {
                new RemoteCommandDescriptor { Name = "Stats" },
                new RemoteCommandDescriptor { Name = "Logging" }
            };

            RemoteCommandSequenceStepValidation ready = RemoteCommandSequenceValidator.Validate(
                new RemoteCommandSequenceStep("/Stats.FPS On"), true, "/", catalog);
            RemoteCommandSequenceStepValidation missing = RemoteCommandSequenceValidator.Validate(
                new RemoteCommandSequenceStep("Quality.SetHDR Off"), true, "/", catalog);

            Assert.That(ready.Availability, Is.EqualTo(RemoteCommandSequenceStepAvailability.Ready));
            Assert.That(ready.BlocksExecution, Is.False);
            Assert.That(missing.Availability, Is.EqualTo(RemoteCommandSequenceStepAvailability.MissingCommand));
            Assert.That(missing.BlocksExecution, Is.False);
        }

        private static RemoteCommandSequence CreateSequence(bool stopOnFailure, params string[] commandLines)
        {
            var sequence = ScriptableObject.CreateInstance<RemoteCommandSequence>();
            var steps = new RemoteCommandSequenceStep[commandLines.Length];
            for (int i = 0; i < commandLines.Length; i++)
                steps[i] = new RemoteCommandSequenceStep(commandLines[i]);
            sequence.Configure(stopOnFailure, steps);
            return sequence;
        }

        private static RemoteCommandSequence CreateSequence(
            bool stopOnFailure,
            params RemoteCommandSequenceStep[] steps)
        {
            var sequence = ScriptableObject.CreateInstance<RemoteCommandSequence>();
            sequence.Configure(stopOnFailure, steps);
            return sequence;
        }

        private sealed class StubDispatcher : IRemoteCommandSequenceDispatcher
        {
            private long _nextRequestId;

            internal readonly List<string> Executed = new();
            private readonly HashSet<string> _availableCommands = new(StringComparer.OrdinalIgnoreCase);
            internal bool CompleteSynchronously;
            internal bool AllCommandsAvailable = true;
            internal int CatalogRefreshCount;
            public bool IsConnected { get; set; } = true;
            public double TimeSinceStartup { get; private set; }
            public event Action<long, RemoteCommandExecuteResponse> ExecutionCompleted;
            public event Action CatalogChanged;
            public event Action Tick;

            public bool IsCommandAvailable(string commandLine) =>
                AllCommandsAvailable || _availableCommands.Contains(commandLine);

            public void RefreshCatalog() => CatalogRefreshCount++;

            public long Execute(string commandLine)
            {
                long requestId = ++_nextRequestId;
                Executed.Add(commandLine);
                if (CompleteSynchronously)
                {
                    ExecutionCompleted?.Invoke(requestId, new RemoteCommandExecuteResponse
                    {
                        Success = true,
                        Message = "Command completed."
                    });
                }
                return requestId;
            }

            internal void Complete(long requestId, bool success, string message)
            {
                ExecutionCompleted?.Invoke(requestId, new RemoteCommandExecuteResponse
                {
                    Success = success,
                    Message = message
                });
            }

            internal void MakeAvailable(string commandLine, bool notify = true)
            {
                _availableCommands.Add(commandLine);
                if (notify)
                    CatalogChanged?.Invoke();
            }

            internal void AdvanceTime(double seconds)
            {
                TimeSinceStartup += seconds;
                Tick?.Invoke();
            }

            public void Dispose()
            {
            }
        }
    }
}
