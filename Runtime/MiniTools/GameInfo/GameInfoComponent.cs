using UnityEngine;
using UnityEngine.UI;

public class GameInfoComponent : MonoBehaviour
{
    [SerializeField] private Text m_TextInfo;

    private void Start()
    {
        DisplayInfo();
    }

    private void DisplayInfo()
    {
        m_TextInfo.text = $"Game Version: <color=cyan>{Application.version}</color>\n" +
            $"Unity Version: <color=cyan>{Application.unityVersion}</color>";
    }
}
