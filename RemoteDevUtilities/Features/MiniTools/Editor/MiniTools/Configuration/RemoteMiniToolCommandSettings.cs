using System;
using HP.Utilities.RemoteDevUtilities.Editor.Commands.Presentation;
using HP.Utilities.RemoteDevUtilities.Editor.Configuration;
using HP.Utilities.RemoteDevUtilities.Protocol.Commands;
using UnityEditor;

namespace HP.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration
{
    /// <summary>
    /// Focused facade for the command section of the unified mini-tool project
    /// settings.
    /// </summary>
    internal sealed class RemoteMiniToolCommandSettings : ScriptableSingleton<RemoteMiniToolCommandSettings>
    {
        internal static event Action Changed;

        internal RemoteMiniToolCommandConfiguration Configuration => RemoteDevUtilitiesProjectSettings.instance.Commands;

        internal bool TryGetOverride(string toolId, out RemoteMiniToolCommandOverride commandOverride)
        {
            return Configuration.TryGet(toolId, out commandOverride);
        }

        internal bool SetOverride(string toolId, string commandName, RemoteCommandRouting routing, out string error)
        {
            error = string.Empty;
            try
            {
                if (!string.IsNullOrWhiteSpace(commandName))
                {
                    _ = new RemoteCommandPresentationBinding(commandName, toolId, routing);
                }
                else if (!Enum.IsDefined(typeof(RemoteCommandRouting), routing))
                {
                    throw new ArgumentOutOfRangeException(nameof(routing));
                }
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }

            if (Configuration.Set(toolId, commandName, routing))
                Persist();
            return true;
        }

        internal void ClearOverride(string toolId)
        {
            if (Configuration.Clear(toolId))
                Persist();
        }

        private void Persist()
        {
            RemoteDevUtilitiesProjectSettings.instance.Persist();
            Changed?.Invoke();
        }
    }
}
