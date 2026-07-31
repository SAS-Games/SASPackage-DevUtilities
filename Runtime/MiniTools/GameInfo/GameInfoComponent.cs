using UnityEngine;
using UnityEngine.UI;

namespace SAS.DevUtilities
{
    /// <summary>
    /// View-only component for the GameInfo prefab.
    /// </summary>
    [AddComponentMenu("Dev Utilities/GameInfo/View")]
    public sealed class GameInfoComponent : MonoBehaviour, IMiniToolSnapshotView<GameInfoSnapshot>
    {
        [SerializeField] private Text m_TextInfo;

        public void ApplySnapshot(in GameInfoSnapshot snapshot)
        {
            if (m_TextInfo == null)
                return;

            m_TextInfo.text = $"Game Version: <color=cyan>{snapshot.GameVersion}</color>\n" +
                              $"Unity Version: <color=cyan>{snapshot.UnityVersion}</color>";
        }
    }
}
