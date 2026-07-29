using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration
{
    [FilePath("ProjectSettings/RemoteDevUtilitiesMiniTools.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class RemoteMiniToolSettings :
        ScriptableSingleton<RemoteMiniToolSettings>
    {
        [SerializeField]
        private RemoteMiniToolVisibilityConfiguration _visibility = new();

        [SerializeField]
        private RemoteMiniToolPresentationConfiguration _presentations = new();

        [SerializeField]
        private RemoteMiniToolCommandConfiguration _commands = new();

        internal RemoteMiniToolVisibilityConfiguration Visibility =>
            _visibility ??= new RemoteMiniToolVisibilityConfiguration();

        internal RemoteMiniToolPresentationConfiguration Presentations =>
            _presentations ??=
                new RemoteMiniToolPresentationConfiguration();

        internal RemoteMiniToolCommandConfiguration Commands =>
            _commands ??= new RemoteMiniToolCommandConfiguration();

        internal void Persist()
        {
            Save(true);
        }
    }
}
