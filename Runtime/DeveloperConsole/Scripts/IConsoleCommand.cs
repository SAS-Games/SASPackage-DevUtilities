namespace SAS.Utilities.DeveloperConsole
{
    public interface IConsoleCommand
    {
        string Name { get; }
        string[] Presets { get; }
        string HelpText { get; }
        bool HelpRequest(string command, string[] args, out string message);
        bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args = null);
        bool Contains(string commandName);
        bool CloseOnCompletion { get; }
    }
}