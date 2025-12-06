using System.Collections.Generic;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    public class DeveloperConsoleCommandRegistrar : MonoBehaviour
    {
        [Header("Commands")] [SerializeField] private ConsoleCommand[] m_Commands;
        [SerializeField] private DeveloperConsoleBehaviour.PlatformCommand[] m_PlatformCommands;
        [SerializeField] private string[] m_CommandsToExecuteOnLoad;

        private readonly List<ConsoleCommand> _registeredCommands = new();
        private DeveloperConsoleBehaviour _developerConsoleBehaviour;

        private void Awake()
        {
            _developerConsoleBehaviour = DeveloperConsoleBehaviour.Instance;
            if (_developerConsoleBehaviour == null)
                return;

            CollectCommandsForScene();
            RunStartUpExecutions();
        }

        private void OnDestroy()
        {
            if (_developerConsoleBehaviour == null)
                return;

            foreach (var consoleCommand in this._registeredCommands)
                _developerConsoleBehaviour.DeveloperConsole.RemoveCommand(consoleCommand);
            _registeredCommands.Clear();
        }

        void CollectCommandsForScene()
        {
            _registeredCommands.Clear();

            if (m_Commands != null)
            {
                foreach (var cmd in m_Commands)
                {
                    if (cmd != null)
                        _registeredCommands.Add(cmd);
                }
            }

            if (m_PlatformCommands != null)
            {
                foreach (var platformCmd in m_PlatformCommands)
                {
                    if (platformCmd == null || platformCmd.commands == null)
                        continue;

                    if (DeveloperConsoleBehaviour.IsCurrentPlatform(platformCmd.platform))
                    {
                        foreach (var cmd in platformCmd.commands)
                        {
                            if (cmd != null)
                                _registeredCommands.Add(cmd);
                        }
                    }
                }
            }

            foreach (var consoleCommand in this._registeredCommands)
                _developerConsoleBehaviour.DeveloperConsole.AddCommand(consoleCommand);
        }


        void RunStartUpExecutions()
        {
            if (m_CommandsToExecuteOnLoad == null || _developerConsoleBehaviour == null)
                return;

            foreach (var cmd in m_CommandsToExecuteOnLoad)
                _developerConsoleBehaviour.DeveloperConsole.ProcessCommand(cmd, _developerConsoleBehaviour, out _);
        }
    }
}