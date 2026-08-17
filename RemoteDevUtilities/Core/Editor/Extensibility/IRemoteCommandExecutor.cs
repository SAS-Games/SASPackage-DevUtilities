using SAS.Utilities.RemoteDevUtilities.Editor.Client;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Commands
{
    internal sealed class RemoteCommandExecutionResult
    {
        internal RemoteCommandExecutionResult(bool success, bool closeRequested, string message)
        {
            Success = success;
            CloseRequested = closeRequested;
            Message = message;
        }

        internal bool Success { get; }
        internal bool CloseRequested { get; }
        internal string Message { get; }
    }

    /// <summary>
    /// Optional command capability used by features such as Logging without
    /// creating an assembly dependency on the Commands implementation.
    /// </summary>
    internal interface IRemoteCommandExecutor : IRemoteEditorFeatureClient
    {
        string Error { get; }
        RemoteCommandExecutionResult ExecutionResult { get; }
        long ExecutionResultRequestId { get; }
        bool HasCommand(string commandName);
        void RequestCatalog();
        long Execute(string commandLine);
    }
}
