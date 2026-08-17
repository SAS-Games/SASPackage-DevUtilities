using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.DebugHost
{
    [RemoteDebugHostContribution(100)]
    internal sealed class RemoteCommandDebugHostContribution : IRemoteDebugHostContribution
    {
        private EditorRemoteCommandPresentationGateway _gateway;

        public void Install(RemoteDevUtilitiesClient client)
        {
            Uninstall();
            _gateway = new EditorRemoteCommandPresentationGateway(client);
            DeveloperConsoleBehaviour console = Object.FindFirstObjectByType<DeveloperConsoleBehaviour>(
                FindObjectsInactive.Include);
            if (console != null)
            {
                console.SetCommandGateway(_gateway);
                console.SetConsoleVisible(true);
            }
        }

        public void Uninstall()
        {
            _gateway?.Dispose();
            _gateway = null;
        }
    }
}
