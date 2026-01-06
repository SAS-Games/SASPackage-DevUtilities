using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class Draggable : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private RectTransform _rect;
    private RectTransform _parent;
    private Vector2 _pointerOffset;

    private void Awake()
    {
        _rect = transform as RectTransform;
        _parent = _rect.parent as RectTransform;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parent,
            eventData.position,
            eventData.pressEventCamera,
            out var localPointerPos);

        _pointerOffset = localPointerPos - _rect.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parent,
            eventData.position,
            eventData.pressEventCamera,
            out var localPointerPos);

        _rect.anchoredPosition = localPointerPos - _pointerOffset;
    }
}
