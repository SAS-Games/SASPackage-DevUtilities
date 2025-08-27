using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class ScrollToSelection : MonoBehaviour
{
    private ScrollRect _scrollRect;

    void Awake()
    {
        if (_scrollRect == null)
            _scrollRect = GetComponent<ScrollRect>();
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
        content.anchoredPosition = new Vector2(currentPos.x, newY);
    }
}