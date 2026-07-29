using SAS.DevUtilities;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimeGameInfoMiniToolProvider :
        MiniToolDataProvider<GameInfoSnapshot>,
        IMiniToolFieldProvider
    {
        public override bool TryGetSnapshot(out GameInfoSnapshot snapshot)
        {
            snapshot = GameInfoSnapshotCollector.Capture();
            return true;
        }

        public RemoteMiniToolField[] CaptureFields()
        {
            Scene scene = SceneManager.GetActiveScene();
            return new[]
            {
                Field("product", "Product", Application.productName),
                Field("version", "Version", Application.version),
                Field("unity", "Unity", Application.unityVersion),
                Field("platform", "Platform", Application.platform.ToString()),
                Field("scene", "Active Scene", scene.IsValid() ? scene.name : "<none>"),
                Field("scenes", "Loaded Scenes", SceneManager.sceneCount.ToString()),
                Field("resolution", "Resolution", $"{Screen.width} x {Screen.height}"),
                Field("fullscreen", "Full Screen", Screen.fullScreen.ToString())
            };
        }

        private static RemoteMiniToolField Field(string name, string displayName, string value) => new()
        {
            Name = name,
            DisplayName = displayName,
            Value = value,
            Unit = string.Empty
        };
    }
}
