using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
[DisallowMultipleComponent]
public class ScrollSnapper : MonoBehaviour
{
    [SerializeField] private float m_Offset = 15;
    private ScrollRect _scrollRect;

    void Awake()
    {
        if (_scrollRect == null)
            _scrollRect = GetComponent<ScrollRect>();
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
    }

    public void FocusOn(Transform target)
    {
        if (target == null)
            return;
        RectTransform targetRect = target.GetComponent<RectTransform>();
        SnapTo(targetRect, _scrollRect.content);
    }

    private void SnapTo(RectTransform target, RectTransform content)
    {
        Canvas.ForceUpdateCanvases();
        Vector2 currentPos = content.anchoredPosition;
        float newY = ((Vector2)_scrollRect.transform.InverseTransformPoint(content.position)
                      - (Vector2)_scrollRect.transform.InverseTransformPoint(target.position)).y;
        content.anchoredPosition = new Vector2(currentPos.x, newY + m_Offset);
    }
}