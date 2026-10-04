using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Pointer drag routing for a backpack slot: reorder another slot or drop on an enemy.</summary>
public sealed class ConsumableSlotDragUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    ActionButtonsUI _owner;
    int _slotIndex;
    CanvasGroup _canvasGroup;
    bool _dragging;
    bool _suppressNextClick;

    public void Configure(ActionButtonsUI owner, int slotIndex)
    {
        _owner = owner;
        _slotIndex = slotIndex;
        if (!_canvasGroup)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (!_canvasGroup) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public bool ConsumeClickSuppression()
    {
        bool suppress = _suppressNextClick;
        _suppressNextClick = false;
        return suppress;
    }

    void OnDisable()
    {
        if (_canvasGroup) _canvasGroup.blocksRaycasts = true;
        _dragging = false;
        _suppressNextClick = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || _owner == null ||
            CardManager.Instance?.GetConsumableAtSlot(_slotIndex) == null || GameplayInputGate.IsBlocked)
            return;
        _dragging = true;
        _suppressNextClick = true;
        if (_canvasGroup) _canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging = false;
        if (_canvasGroup) _canvasGroup.blocksRaycasts = true;
        _owner?.HandleConsumableSlotDrop(_slotIndex, eventData);
        StartCoroutine(ClearClickSuppressionNextFrame());
    }

    System.Collections.IEnumerator ClearClickSuppressionNextFrame()
    {
        yield return null;
        _suppressNextClick = false;
    }
}
