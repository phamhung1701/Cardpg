using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Inspect the current build; existing rail/Trick controls retain ownership of all actions.</summary>
public sealed class BuildInventoryUI : MonoBehaviour
{
    public GameObject panel;
    public RectTransform gearRows,trickRows;
    public TMP_Text capacity;
    public Button closeButton;
    public GameplayMenuCoordinator coordinator;
    public ActionButtonsUI actions;
    void OnEnable(){if(closeButton)closeButton.onClick.AddListener(Close);if(panel)panel.SetActive(false);}
    void OnDisable(){if(closeButton)closeButton.onClick.RemoveListener(Close);}
    void Update(){if(coordinator&&coordinator.IsTop(this)&&Keyboard.current?.escapeKey.wasPressedThisFrame==true)
    {if(ContextualTooltipUI.Instance&&ContextualTooltipUI.Instance.DismissPinned())return;Close();}}
    public void Open()
    {
        if(!coordinator||GameplayInputGate.IsBlocked||!CardManager.Instance)return;
        Rebuild();
        var card=(RectTransform)panel.transform.GetChild(0);var parent=(RectTransform)panel.transform;
        int count=Mathf.Max(CardManager.Instance.ownedGear.Count,CardManager.Instance.BackpackSlotsUsed);
        card.anchorMin=card.anchorMax=new Vector2(.5f,.5f);card.anchoredPosition=Vector2.zero;
        card.sizeDelta=new Vector2(Mathf.Min(1100,parent.rect.width*.72f),Mathf.Min(parent.rect.height*.78f,260+Mathf.Clamp(count,1,4)*80));
        coordinator.Push(this,panel,closeButton);
    }
    public void Close(){if(coordinator&&coordinator.IsTop(this))coordinator.Pop(this);}
    void Rebuild()
    {
        Clear(gearRows);Clear(trickRows);
        var cards=CardManager.Instance;
        capacity.text=$"Gear {cards.GearSlotsUsed}/{cards.GearCapacity}   •   Tricks {cards.BackpackSlotsUsed}/{cards.BackpackCapacity}";
        for(int i=0;i<cards.ownedGear.Count;i++)
        {
            var gear=cards.ownedGear[i];if(!gear)continue;
            var row=Row(gearRows,gear.displayName,"Inspect in Gear rail",new ContextualInfo("Gear",gear.displayName,gear.description));
            long id=cards.GetGearInstanceAt(i)?.Id??0;
            row.onClick.AddListener(()=>{
                Close();var rail=Object.FindFirstObjectByType<GearRailUI>();
                var slots=rail?rail.GetComponentsInChildren<GearIconSlotUI>():null;
                var selected=slots==null?null:System.Array.Find(slots,s=>s.InstanceId==id);
                if(selected)selected.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
            });
        }
        for(int i=0;i<actions.backpackSlotButtons.Length;i++)
        {
            var item=cards.GetConsumableInstanceAtSlot(i);if(item==null)continue;
            int slot=i;
            var row=Row(trickRows,item.Definition.displayName,$"{cards.GetConsumableStackCountAtSlot(i)} copies • {item.RemainingCharges} charges",
                new ContextualInfo("Trick",item.Definition.displayName,item.Definition.description));
            row.onClick.AddListener(()=>{Close();if(cards.GetConsumableInstanceAtSlot(slot)==item)actions.backpackSlotButtons[slot].onClick.Invoke();});
        }
        if(gearRows.childCount==0)Row(gearRows,"No Gear equipped","Acquire Gear in the Black Market.",new ContextualInfo("Gear","Empty build","Your equipped Gear will appear here."));
        if(trickRows.childCount==0)Row(trickRows,"No Tricks carried","Acquire Tricks in the Black Market.",new ContextualInfo("Trick","Empty slots","Your carried Tricks will appear here."));
        DealbreakUIStyle.ApplyHierarchy(panel.transform);
    }
    static void Clear(Transform rows){while(rows.childCount>0){var child=rows.GetChild(0);child.SetParent(null);child.gameObject.SetActive(false);Destroy(child.gameObject);}}
    static Button Row(Transform rows,string name,string subtitle,ContextualInfo info)
    {
        var r=DealbreakUIStyle.Child(rows,"Item "+rows.childCount,typeof(Image),typeof(Button),typeof(LayoutElement));
        r.GetComponent<LayoutElement>().preferredHeight=80;
        var text=DealbreakUIStyle.Child(r,"Item name",typeof(TextMeshProUGUI));DealbreakUIStyle.Place(text,.06f,.15f,.94f,.90f);
        var label=text.GetComponent<TMP_Text>();label.text=$"{name}\n<size=70%>{subtitle}</size>";label.fontSize=22;label.alignment=TextAlignmentOptions.MidlineLeft;
        var trigger=r.gameObject.AddComponent<ContextualInfoTrigger>();trigger.describe=()=>info;trigger.allowWhileBlocked=true;
        return r.GetComponent<Button>();
    }
}
