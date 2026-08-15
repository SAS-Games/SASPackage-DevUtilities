using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using UnityEditor;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Commands.Sequences
{
    internal enum RemoteCommandSequenceRunState
    {
        Idle,
        Running,
        Completed,
        Failed,
        Cancelled
    }

    internal enum RemoteCommandSequenceStepState
    {
        Pending,
        WaitingForCommand,
        Running,
        Succeeded,
        Failed,
        Skipped
    }

    internal sealed class RemoteCommandSequenceStepResult
    {
        internal RemoteCommandSequenceStepResult(
            int sourceIndex,
            string commandLine,
            bool enabled,
            RemoteCommandUnavailablePolicy whenUnavailable,
            float waitTimeoutSeconds)
        {
            SourceIndex = sourceIndex;
            CommandLine = commandLine ?? string.Empty;
            WhenUnavailable = whenUnavailable;
            WaitTimeoutSeconds = waitTimeoutSeconds;
            State = enabled ? RemoteCommandSequenceStepState.Pending : RemoteCommandSequenceStepState.Skipped;
            Message = enabled ? string.Empty : "Disabled.";
        }

        internal int SourceIndex { get; }
        internal string CommandLine { get; }
        internal RemoteCommandUnavailablePolicy WhenUnavailable { get; }
        internal float WaitTimeoutSeconds { get; }
        internal RemoteCommandSequenceStepState State { get; set; }
        internal string Message { get; set; }
    }

    internal interface IRemoteCommandSequenceDispatcher : IDisposable
    {
        bool IsConnected { get; }
        double TimeSinceStartup { get; }
        event Action<long, RemoteCommandExecuteResponse> ExecutionCompleted;
        event Action CatalogChanged;
        event Action Tick;
        bool IsCommandAvailable(string commandLine);
        void RefreshCatalog();
        long Execute(string commandLine);
    }

    internal sealed class RemoteCommandSequenceDispatcher : IRemoteCommandSequenceDispatcher
    {
        private readonly RemoteDevUtilitiesClient _client;
        private readonly RemoteCommandClient _commands;
        private readonly RemoteCommandPresentationCoordinator _coordinator;

        internal RemoteCommandSequenceDispatcher(RemoteDevUtilitiesClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _commands = client.GetRequiredFeature<RemoteCommandClient>();
            _coordinator = new RemoteCommandPresentationCoordinator(client);
            _commands.ExecutionCompleted += OnExecutionCompleted;
            _commands.CatalogChanged += OnCatalogChanged;
            EditorApplication.update += OnEditorUpdate;
        }

        public bool IsConnected => _client.IsConnected;
        public double TimeSinceStartup => EditorApplication.timeSinceStartup;
        public event Action<long, RemoteCommandExecuteResponse> ExecutionCompleted;
        public event Action CatalogChanged;
        public event Action Tick;

        public bool IsCommandAvailable(string commandLine)
        {
            var step = new RemoteCommandSequenceStep(commandLine);
            return RemoteCommandSequenceValidator.Validate(
                step, IsConnected, _commands.Prefix, _commands.Commands).Availability ==
                   RemoteCommandSequenceStepAvailability.Ready;
        }

        public void RefreshCatalog() => _commands.RequestCatalog();
        public long Execute(string commandLine) => _coordinator.Execute(commandLine);

        public void Dispose()
        {
            _commands.ExecutionCompleted -= OnExecutionCompleted;
            _commands.CatalogChanged -= OnCatalogChanged;
            EditorApplication.update -= OnEditorUpdate;
            ExecutionCompleted = null;
            CatalogChanged = null;
            Tick = null;
        }

        private void OnExecutionCompleted(long requestId, RemoteCommandExecuteResponse response) =>
            ExecutionCompleted?.Invoke(requestId, response);

        private void OnCatalogChanged() => CatalogChanged?.Invoke();
        private void OnEditorUpdate() => Tick?.Invoke();
    }

    internal sealed class RemoteCommandSequenceRunner : IDisposable
    {
        private const double CatalogRefreshIntervalSeconds = 0.5d;
        private readonly IRemoteCommandSequenceDispatcher _dispatcher;
        private readonly List<RemoteCommandSequenceStepResult> _results = new();
        private bool _stopOnFailure;
        private bool _cancelRequested;
        private bool _dispatching;
        private bool _advancing;
        private bool _advanceRequested;
        private bool _waitingForCommand;
        private double _waitDeadline;
        private double _nextCatalogRefresh;
        private long _pendingRequestId = -1;
        private long _deferredRequestId = -1;
        private RemoteCommandExecuteResponse _deferredResponse;
        private int _currentResultIndex = -1;

        internal RemoteCommandSequenceRunner(IRemoteCommandSequenceDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _dispatcher.ExecutionCompleted += OnExecutionCompleted;
            _dispatcher.CatalogChanged += OnCatalogChanged;
            _dispatcher.Tick += OnTick;
        }

        internal event Action Changed;
        internal RemoteCommandSequenceRunState State { get; private set; }
        internal IReadOnlyList<RemoteCommandSequenceStepResult> Results => _results;
        internal bool IsRunning => State == RemoteCommandSequenceRunState.Running;
        internal bool IsCancellationRequested => _cancelRequested && IsRunning;

        internal bool Start(RemoteCommandSequence sequence, out string error)
        {
            error = null;
            if (IsRunning)
            {
                error = "A command sequence is already running.";
                return false;
            }
            if (sequence == null)
            {
                error = "Select a command sequence to run.";
                return false;
            }
            if (!_dispatcher.IsConnected)
            {
                error = "Connect to a runtime Player before running a command sequence.";
                return false;
            }

            _results.Clear();
            IReadOnlyList<RemoteCommandSequenceStep> steps = sequence.Steps;
            int enabledCount = 0;
            for (int i = 0; i < steps.Count; i++)
            {
                RemoteCommandSequenceStep step = steps[i];
                bool enabled = step != null && step.Enabled;
                string commandLine = step?.CommandLine?.Trim() ?? string.Empty;
                if (enabled && string.IsNullOrWhiteSpace(commandLine))
                {
                    error = $"Step {i + 1} does not contain a command.";
                    _results.Clear();
                    return false;
                }

                if (enabled)
                    enabledCount++;
                _results.Add(new RemoteCommandSequenceStepResult(
                    i,
                    commandLine,
                    enabled,
                    step?.WhenUnavailable ?? RemoteCommandUnavailablePolicy.FailImmediately,
                    step?.WaitTimeoutSeconds ?? 5f));
            }

            if (enabledCount == 0)
            {
                error = "The sequence does not contain any enabled commands.";
                _results.Clear();
                return false;
            }

            _stopOnFailure = sequence.StopOnFailure;
            _cancelRequested = false;
            _pendingRequestId = -1;
            _deferredRequestId = -1;
            _deferredResponse = null;
            _currentResultIndex = -1;
            _waitingForCommand = false;
            _waitDeadline = 0d;
            _nextCatalogRefresh = 0d;
            State = RemoteCommandSequenceRunState.Running;
            Changed?.Invoke();
            RequestAdvance();
            return true;
        }

        internal void Cancel()
        {
            if (!IsRunning || _cancelRequested)
                return;

            _cancelRequested = true;
            Changed?.Invoke();
            if ((_pendingRequestId < 0 || _waitingForCommand) && !_dispatching)
                FinishCancelled();
        }

        internal void Abort(string reason)
        {
            if (!IsRunning)
                return;

            string message = string.IsNullOrWhiteSpace(reason) ? "Sequence aborted." : reason.Trim();
            if (_currentResultIndex >= 0 && _currentResultIndex < _results.Count &&
                (_results[_currentResultIndex].State == RemoteCommandSequenceStepState.Running ||
                 _results[_currentResultIndex].State == RemoteCommandSequenceStepState.WaitingForCommand))
            {
                _results[_currentResultIndex].State = RemoteCommandSequenceStepState.Failed;
                _results[_currentResultIndex].Message = message;
            }

            SkipPending(message);
            _pendingRequestId = -1;
            _waitingForCommand = false;
            State = RemoteCommandSequenceRunState.Failed;
            Changed?.Invoke();
        }

        public void Dispose()
        {
            _dispatcher.ExecutionCompleted -= OnExecutionCompleted;
            _dispatcher.CatalogChanged -= OnCatalogChanged;
            _dispatcher.Tick -= OnTick;
            if (IsRunning)
                Abort("Sequence runner was closed.");
            _dispatcher.Dispose();
            Changed = null;
        }

        private void RequestAdvance()
        {
            if (!IsRunning)
                return;
            if (_advancing)
            {
                _advanceRequested = true;
                return;
            }

            _advancing = true;
            try
            {
                do
                {
                    _advanceRequested = false;
                    AdvanceOnce();
                }
                while (_advanceRequested && IsRunning);
            }
            finally
            {
                _advancing = false;
            }
        }

        private void AdvanceOnce()
        {
            if (_pendingRequestId >= 0 || _dispatching || _waitingForCommand)
                return;
            if (_cancelRequested)
            {
                FinishCancelled();
                return;
            }

            _currentResultIndex = FindNextPendingResult();
            if (_currentResultIndex < 0)
            {
                State = HasFailedStep() ? RemoteCommandSequenceRunState.Failed : RemoteCommandSequenceRunState.Completed;
                Changed?.Invoke();
                return;
            }

            ExecuteCurrentStep();
        }

        private void ExecuteCurrentStep()
        {
            RemoteCommandSequenceStepResult result = _results[_currentResultIndex];
            if (!_dispatcher.IsCommandAvailable(result.CommandLine))
            {
                if (result.WhenUnavailable == RemoteCommandUnavailablePolicy.WaitUntilAvailable)
                {
                    _waitingForCommand = true;
                    _waitDeadline = _dispatcher.TimeSinceStartup + result.WaitTimeoutSeconds;
                    _nextCatalogRefresh = _dispatcher.TimeSinceStartup + CatalogRefreshIntervalSeconds;
                    result.State = RemoteCommandSequenceStepState.WaitingForCommand;
                    result.Message = $"Waiting up to {result.WaitTimeoutSeconds:0.##}s for the command to become available.";
                    Changed?.Invoke();
                    _dispatcher.RefreshCatalog();
                }
                else
                {
                    FailCurrent("Command is not available in the current Player catalog.");
                }
                return;
            }

            result.State = RemoteCommandSequenceStepState.Running;
            result.Message = string.Empty;
            _deferredRequestId = -1;
            _deferredResponse = null;
            _dispatching = true;
            Changed?.Invoke();

            long requestId;
            try
            {
                requestId = _dispatcher.Execute(result.CommandLine);
            }
            catch (Exception exception)
            {
                _dispatching = false;
                CompleteCurrent(new RemoteCommandExecuteResponse
                {
                    Success = false,
                    Message = exception.GetType().Name + ": " + exception.Message
                });
                return;
            }

            _dispatching = false;
            _pendingRequestId = requestId;
            if (_deferredResponse != null && _deferredRequestId == requestId)
            {
                RemoteCommandExecuteResponse deferred = _deferredResponse;
                _deferredRequestId = -1;
                _deferredResponse = null;
                CompleteCurrent(deferred);
            }
        }

        private void OnExecutionCompleted(long requestId, RemoteCommandExecuteResponse response)
        {
            if (!IsRunning)
                return;
            if (_dispatching)
            {
                _deferredRequestId = requestId;
                _deferredResponse = response;
                return;
            }
            if (requestId != _pendingRequestId)
                return;

            CompleteCurrent(response);
        }

        private void OnCatalogChanged()
        {
            if (IsRunning && _waitingForCommand)
                TryResumeWaitingStep();
        }

        private void OnTick()
        {
            if (!IsRunning || !_waitingForCommand)
                return;
            if (!_dispatcher.IsConnected)
            {
                Abort("The Player disconnected while waiting for a command.");
                return;
            }
            if (TryResumeWaitingStep())
                return;
            if (_dispatcher.TimeSinceStartup < _waitDeadline)
            {
                if (_dispatcher.TimeSinceStartup >= _nextCatalogRefresh)
                {
                    _nextCatalogRefresh = _dispatcher.TimeSinceStartup + CatalogRefreshIntervalSeconds;
                    _dispatcher.RefreshCatalog();
                }
                return;
            }

            _waitingForCommand = false;
            FailCurrent("Timed out waiting for the command to become available.");
        }

        private bool TryResumeWaitingStep()
        {
            if (!_waitingForCommand || _currentResultIndex < 0 ||
                !_dispatcher.IsCommandAvailable(_results[_currentResultIndex].CommandLine))
                return false;

            _waitingForCommand = false;
            ExecuteCurrentStep();
            return true;
        }

        private void CompleteCurrent(RemoteCommandExecuteResponse response)
        {
            if (_currentResultIndex < 0 || _currentResultIndex >= _results.Count)
                return;

            _pendingRequestId = -1;
            RemoteCommandSequenceStepResult result = _results[_currentResultIndex];
            bool success = response?.Success == true;
            result.State = success ? RemoteCommandSequenceStepState.Succeeded : RemoteCommandSequenceStepState.Failed;
            result.Message = response?.Message ?? (success ? "Command completed." : "Command failed.");
            Changed?.Invoke();

            AfterStepCompleted(success);
        }

        private void FailCurrent(string message)
        {
            if (_currentResultIndex < 0 || _currentResultIndex >= _results.Count)
                return;

            RemoteCommandSequenceStepResult result = _results[_currentResultIndex];
            result.State = RemoteCommandSequenceStepState.Failed;
            result.Message = message ?? "Command failed.";
            Changed?.Invoke();
            AfterStepCompleted(false);
        }

        private void AfterStepCompleted(bool success)
        {
            if (_cancelRequested)
            {
                FinishCancelled();
                return;
            }
            if (!success && _stopOnFailure)
            {
                SkipPending("Skipped after a previous command failed.");
                State = RemoteCommandSequenceRunState.Failed;
                Changed?.Invoke();
                return;
            }

            RequestAdvance();
        }

        private int FindNextPendingResult()
        {
            for (int i = _currentResultIndex + 1; i < _results.Count; i++)
            {
                if (_results[i].State == RemoteCommandSequenceStepState.Pending)
                    return i;
            }
            return -1;
        }

        private bool HasFailedStep()
        {
            for (int i = 0; i < _results.Count; i++)
            {
                if (_results[i].State == RemoteCommandSequenceStepState.Failed)
                    return true;
            }
            return false;
        }

        private void FinishCancelled()
        {
            if (_waitingForCommand && _currentResultIndex >= 0 && _currentResultIndex < _results.Count)
            {
                RemoteCommandSequenceStepResult waiting = _results[_currentResultIndex];
                waiting.State = RemoteCommandSequenceStepState.Skipped;
                waiting.Message = "Skipped because the sequence was cancelled.";
            }
            SkipPending("Skipped because the sequence was cancelled.");
            State = RemoteCommandSequenceRunState.Cancelled;
            _pendingRequestId = -1;
            _waitingForCommand = false;
            Changed?.Invoke();
        }

        private void SkipPending(string message)
        {
            for (int i = 0; i < _results.Count; i++)
            {
                if (_results[i].State != RemoteCommandSequenceStepState.Pending)
                    continue;
                _results[i].State = RemoteCommandSequenceStepState.Skipped;
                _results[i].Message = message;
            }
        }
    }
}
