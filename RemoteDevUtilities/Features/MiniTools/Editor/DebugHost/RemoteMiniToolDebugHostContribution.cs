using HP.Utilities.RemoteDevUtilities.Editor.Client;
using HP.Utilities.RemoteDevUtilities.Editor.DebugHost.MiniTools;

namespace HP.Utilities.RemoteDevUtilities.Editor.DebugHost
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
