namespace SAS.Utilities.DeveloperConsole
{
    public interface IConsoleCommand
    {
        string CommandWord { get; }
        string HelpText { get; }
        bool Process(string[] args, DeveloperConsoleBehaviour developerConsole);
    }
}
