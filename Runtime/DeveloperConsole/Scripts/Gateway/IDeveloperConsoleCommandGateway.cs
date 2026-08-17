using System;

namespace SAS.Utilities.DeveloperConsole
{
    /// <summary>
    /// Supplies a Developer Console with commands from an optional external
    /// source without exposing that source's transport or protocol types.
    /// </summary>
    public interface IDeveloperConsoleCommandGateway
    {
        bool IsConnected { get; }
        string Prefix { get; }
        DeveloperConsoleCommandDescriptor[] Commands { get; }

        event Action CatalogChanged;
        event Action<DeveloperConsoleCommandResult> CommandCompleted;

        void Execute(string commandLine);
    }
}
