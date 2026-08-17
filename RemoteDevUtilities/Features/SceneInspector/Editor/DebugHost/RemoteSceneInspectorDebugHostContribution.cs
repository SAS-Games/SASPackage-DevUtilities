using HP.Utilities.RemoteDevUtilities.DebugHost;
using HP.Utilities.RemoteDevUtilities.Editor.Client;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.DebugHost
{
    [RemoteDebugHostContribution(400)]
    internal sealed class RemoteSceneInspectorDebugHostContribution : IRemoteDebugHostContribution
    {
        private GameObject _hostObject;

        public void Install(RemoteDevUtilitiesClient client)
        {
            RemoteDebugHostSession.Install(new EditorRemoteRuntimeSceneInspectorProxy(client));
            _hostObject = new GameObject("[Remote Runtime Scene Inspector Host]")
            {
                hideFlags = HideFlags.DontSave
            };
            Object.DontDestroyOnLoad(_hostObject);
            _hostObject.AddComponent<RemoteRuntimeSceneInspectorHost>();
        }

        public void Uninstall()
        {
            RemoteDebugHostSession.Clear();
            if (_hostObject != null)
                Object.DestroyImmediate(_hostObject);
            _hostObject = null;
        }
    }
}
