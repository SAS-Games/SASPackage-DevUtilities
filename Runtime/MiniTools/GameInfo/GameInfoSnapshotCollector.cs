using UnityEngine;

namespace HP.DevUtilities
{
    /// <summary>
    /// Collects the canonical GameInfo snapshot used by local and remote
    /// providers.
    /// </summary>
    internal static class GameInfoSnapshotCollector
    {
        internal static GameInfoSnapshot Capture()
        {
            return new GameInfoSnapshot
            {
                GameVersion = Application.version,
                UnityVersion = Application.unityVersion
            };
        }
    }
}
