using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Brief visual feedback only; never adds or replaces command listeners.</summary>
public sealed class EditorialButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    public enum Role { Primary, Secondary, Destructive, Quiet }
    public Role role;
    public EditorialFrame frame;
    public Button button;
    public bool selected;
    bool _hover,_pressed,_focus;
    TMPro.TMP_Text[] _labels;
    bool? _lastAvailable;
    void LateUpdate()
    {
        var theme=DealbreakUIStyle.Theme;
        if (!button || !frame || !theme) return;
        bool available=button.IsInteractable();
        if(_lastAvailable!=available)
        {
            _labels=GetComponentsInChildren<TMPro.TMP_Text>(true);
            _lastAvailable=available;
        }
        bool open=GetComponent<EditorialSurface>()?.treatment==EditorialSurface.Treatment.Open;
        bool paper=GetComponent<EditorialSurface>()?.treatment==EditorialSurface.Treatment.Paper;
        foreach(var label in _labels)if(label)label.color=paper?theme.ink:open&&available&&(_hover||_focus||selected)?theme.ochre:!open&&available&&role==Role.Primary?theme.ink:theme.paper;
        if(button.targetGraphic)button.targetGraphic.color=Color.clear;
        Color fill=role==Role.Primary?theme.ochre:role==Role.Destructive?theme.brick:role==Role.Quiet?theme.charcoal:theme.slate;
        if(paper)fill=theme.paper;
        if (!available) fill=Color.Lerp(fill,paper?theme.slate:theme.charcoal,paper?.2f:.68f);
        else if (_hover||_focus||selected) fill=Color.Lerp(fill,theme.paper,.13f);
        if (_pressed&&available) fill=Color.Lerp(fill,theme.ink,.2f);
        Color rim=selected||available&&(_hover||_focus)?theme.paper:theme.slate;
        if(fill!=frame.fill||rim!=frame.rim){frame.fill=fill;frame.rim=rim;frame.Refresh();}
        var rect=frame.rectTransform;
        var target=new Vector2(0,_pressed&&available?-2:available&&(_hover||_focus)?2:0);
        rect.anchoredPosition=Vector2.Lerp(rect.anchoredPosition,target,1-Mathf.Exp(-Time.unscaledDeltaTime*24));
    }
    public void OnPointerEnter(PointerEventData _) => _hover=true;
    public void OnPointerExit(PointerEventData _) { _hover=false;_pressed=false; }
    public void OnPointerDown(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left)_pressed=true; }
    public void OnPointerUp(PointerEventData _) => _pressed=false;
    public void OnSelect(BaseEventData _) => _focus=true;
    public void OnDeselect(BaseEventData _) { _focus=false;_pressed=false; }
    void OnDisable() { _hover=_pressed=_focus=false; }
}
