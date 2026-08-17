using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.DebugHost.MiniTools;

namespace SAS.Utilities.RemoteDevUtilities.Editor.DebugHost
{
    [RemoteDebugHostContribution(200)]
    internal sealed class RemoteMiniToolDebugHostContribution : IRemoteDebugHostContribution
    {
        private RemoteMiniToolPrefabPresenter _presenter;

        public void Install(RemoteDevUtilitiesClient client)
        {
            Uninstall();
            _presenter = new RemoteMiniToolPrefabPresenter(client);
        }

        public void Uninstall()
        {
            _presenter?.Dispose();
            _presenter = null;
        }
    }
}
