using UnityEngine;
using UnityEngine.Serialization;

namespace SAS.Utilities.DeveloperConsole.InputVisualizers
{
    public enum ScreenPosition
    {
        None,
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft
    }

    public class InputVisualizerHandler : MonoBehaviour
    {
        [SerializeField] private InputVisualizer m_rootVisualizer;

        [SerializeField, FormerlySerializedAs("_height")] private float m_height;

        [SerializeField, FormerlySerializedAs("_width")] private float m_width;

        [SerializeField, FormerlySerializedAs("_widthPadding")] private float m_widthPadding;

        [SerializeField, FormerlySerializedAs("_heightPadding")] private float m_heightPadding;

        private Vector2 _topLeft, _topRight, _bottomLeft, _bottomRight;

        public void AnchorToScreenEdge(ScreenPosition screenPosition)
        {
            _topLeft = new Vector2(m_widthPadding, m_heightPadding);
            _topRight = new Vector2(Screen.width - m_width - m_widthPadding, m_heightPadding);
            _bottomRight = new Vector2(Screen.width - m_width - m_widthPadding, Screen.height - m_height - m_heightPadding);
            _bottomLeft = new Vector2(m_widthPadding, Screen.height - m_height - m_heightPadding);

            m_rootVisualizer.m_Rect.position = screenPosition switch
            {
                ScreenPosition.TopLeft => _topLeft,
                ScreenPosition.TopRight => _topRight,
                ScreenPosition.BottomLeft => _bottomLeft,
                ScreenPosition.BottomRight => _bottomRight,
                _ => Vector2.zero
            };
        }

#if UNITY_EDITOR
        private void Reset()
        {
            m_rootVisualizer ??= GetComponent<InputVisualizer>();
        }

        [ContextMenu("Anchor Top Left")] private void AnchorTopLeft() => AnchorToScreenEdge(ScreenPosition.TopLeft);
        [ContextMenu("Anchor Top Right")] private void AnchorTopRight() => AnchorToScreenEdge(ScreenPosition.TopRight);
        [ContextMenu("Anchor Bottom Right")] private void AnchorBottomRight() => AnchorToScreenEdge(ScreenPosition.BottomRight);
        [ContextMenu("Anchor Bottom Left")] private void AnchorBottomLeft() => AnchorToScreenEdge(ScreenPosition.BottomLeft);

#endif
    }
}
