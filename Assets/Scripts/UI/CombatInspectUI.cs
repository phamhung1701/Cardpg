using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Inspect/focus adapter; all commit input still routes through the existing handlers.</summary>
public sealed class CombatInspectUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler, ISubmitHandler
{
    bool _pointer, _focus;
    public bool IsInspected => _pointer || _focus;
    public void OnPointerEnter(PointerEventData _) { _pointer = true; CombatDetailPanelUI.Instance?.Inspect(this); }
    public void OnPointerExit(PointerEventData _) { _pointer = false; if (!_focus) CombatDetailPanelUI.Instance?.Leave(this); }
    public void OnSelect(BaseEventData _)
    {
        _focus = true;
        if (TryGetComponent<CardView>(out var card) && !GameplayInputGate.IsBlocked) card.SetInspectFocus(true);
        CombatDetailPanelUI.Instance?.Inspect(this);
    }
    public void OnDeselect(BaseEventData _)
    {
        _focus = false;
        if (TryGetComponent<CardView>(out var card)) card.SetInspectFocus(false);
        if (!_pointer) CombatDetailPanelUI.Instance?.Leave(this);
    }
    void OnDisable()
    {
        _pointer = _focus = false;
        if (TryGetComponent<CardView>(out var card)) card.SetInspectFocus(false);
        CombatDetailPanelUI.Instance?.Leave(this);
    }
    public void OnSubmit(BaseEventData eventData)
    {
        if (GameplayInputGate.IsBlocked || EventSystem.current == null) return;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        if (TryGetComponent<CardView>(out var card)) card.OnPointerClick(pointer);
        else if (TryGetComponent<CardActionDropTarget>(out var target)) target.OnPointerClick(pointer);
        else if (TryGetComponent<GearIconSlotUI>(out var gear)) gear.OnPointerClick(pointer);
    }
}
