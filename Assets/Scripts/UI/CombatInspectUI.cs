using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Inspect/focus adapter; all commit input still routes through the existing handlers.</summary>
public sealed class CombatInspectUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler, ISubmitHandler
{
    bool _pointer, _focus;
    public bool IsInspected => _pointer || _focus;
    public void OnPointerEnter(PointerEventData _) { _pointer = true; Inspect(); }
    public void OnPointerExit(PointerEventData _) { _pointer = false; if (!_focus) Leave(); }
    public void OnSelect(BaseEventData _)
    {
        _focus = true;
        if (TryGetComponent<CardView>(out var card) && !GameplayInputGate.IsBlocked) card.SetInspectFocus(true);
        Inspect();
    }
    public void OnDeselect(BaseEventData _)
    {
        _focus = false;
        if (TryGetComponent<CardView>(out var card)) card.SetInspectFocus(false);
        if (!_pointer) Leave();
    }
    void OnDisable()
    {
        _pointer = _focus = false;
        if (TryGetComponent<CardView>(out var card)) card.SetInspectFocus(false);
        Leave();
    }
    void Inspect(){if(ContextualTooltipUI.Instance)ContextualTooltipUI.Instance.Inspect(this);else CombatDetailPanelUI.Instance?.Inspect(this);}
    void Leave(){if(ContextualTooltipUI.Instance)ContextualTooltipUI.Instance.Leave(this);else CombatDetailPanelUI.Instance?.Leave(this);}
    public void OnSubmit(BaseEventData eventData)
    {
        if (EventSystem.current == null) return;
        var selectable=GetComponent<UnityEngine.UI.Selectable>();
        if(selectable&&!selectable.IsInteractable())return;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        if (TryGetComponent<CardView>(out var card))
        {
            if(card.isActiveAndEnabled)card.OnPointerClick(pointer);
            else
            {
                var choice=GetComponentInParent<UnityEngine.UI.Button>();
                if(choice&&!GameplayInputGate.IsBlocked&&choice.IsInteractable())choice.onClick.Invoke();
                else ContextualTooltipUI.Instance?.Pin(this);
            }
            return;
        }
        if(GameplayInputGate.IsBlocked)return;
        if (TryGetComponent<CardActionDropTarget>(out var target)) target.OnPointerClick(pointer);
        else if (TryGetComponent<GearIconSlotUI>(out var gear)) gear.OnPointerClick(pointer);
    }
}
