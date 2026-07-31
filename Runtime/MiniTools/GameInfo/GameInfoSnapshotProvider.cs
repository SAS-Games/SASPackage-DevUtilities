using UnityEngine;

namespace SAS.DevUtilities
{
    /// <summary>
    /// Collects and publishes GameInfo snapshots for the local Player prefab.
    /// Remote providers use the same <see cref="GameInfoSnapshotCollector"/>.
    /// </summary>
    [AddComponentMenu("Dev Utilities/GameInfo/Provider")]

    public sealed class GameInfoSnapshotProvider : MiniToolSnapshotProviderBehaviour<GameInfoSnapshot>
    {
        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            GameInfoSnapshot snapshot = GameInfoSnapshotCollector.Capture();
            PublishSnapshot(in snapshot);
        }
    }
}
