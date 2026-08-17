namespace HP.Utilities.DeveloperConsole
{
    public sealed class DeveloperConsoleCommandResult
    {
        public DeveloperConsoleCommandResult(bool success, bool closeRequested, string message)
        {
            Success = success;
            CloseRequested = closeRequested;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public bool CloseRequested { get; }
        public string Message { get; }
    }
}
