using SAS.DevUtilities;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimeGameInfoMiniToolProvider : MiniToolDataProvider<GameInfoSnapshot>, IMiniToolFieldProvider
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
                CreateField("product", "Product", Application.productName),
                CreateField("version", "Version", Application.version),
                CreateField("unity", "Unity", Application.unityVersion),
                CreateField("platform", "Platform", Application.platform.ToString()),
                CreateField("scene", "Active Scene", scene.IsValid() ? scene.name : "<none>"),
                CreateField("scenes", "Loaded Scenes", SceneManager.sceneCount.ToString()),
                CreateField("resolution", "Resolution", $"{Screen.width} x {Screen.height}"),
                CreateField("fullscreen", "Full Screen", Screen.fullScreen.ToString())
            };
        }
    }
}
