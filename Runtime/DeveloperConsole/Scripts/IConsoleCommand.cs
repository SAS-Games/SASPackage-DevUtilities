namespace SAS.Utilities.DeveloperConsole
{
    public interface IConsoleCommand
    {
        string CommandWord { get; }
        bool Process(string[] args, DeveloperConsoleBehaviour developerConsole);
    }
}
