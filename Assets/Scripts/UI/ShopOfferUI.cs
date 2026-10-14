using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Readable store card. Selecting reveals its own explicitly committed purchase control.</summary>
public sealed class ShopOfferUI : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
{
    bool _selected,_hover,_focus;
    bool _sizing;
    float _lastWidth;
    void OnRectTransformDimensionsChange()
    {
        if(_sizing||Offer==null||!descriptionLabel)return;
        float width=((RectTransform)transform).rect.width;
        if(Mathf.Abs(width-_lastWidth)<1)return;
        _lastWidth=width;_sizing=true;SetSelected(_selected);_sizing=false;
    }
    public TMP_Text titleLabel,priceLabel,statusLabel,descriptionLabel;
    public Button purchaseButton;
    public ShopOffer Offer { get; private set; }
    public void Bind(ShopOffer offer,string availability)
    {
        Offer=offer;
        if(!titleLabel)titleLabel=GetComponentInChildren<TMP_Text>();
        if(!priceLabel)priceLabel=Label("OfferPrice",.60f,.80f,.95f,.96f);
        if(!statusLabel)statusLabel=Label("OfferAvailability",.05f,.06f,.95f,.22f);
        if(!descriptionLabel)descriptionLabel=Label("OfferDescription",.05f,.27f,.95f,.75f);
        var surface=GetComponent<EditorialSurface>();if(!surface)surface=gameObject.AddComponent<EditorialSurface>();surface.treatment=EditorialSurface.Treatment.Paper;
        DealbreakUIStyle.Frame(transform,true);
        var old=GetComponent<EditorialButton>();if(old)old.enabled=false;
        DealbreakUIStyle.Place(titleLabel.transform,.05f,.80f,.69f,.96f);
        titleLabel.text=offer.DisplayName;
        descriptionLabel.text=offer.Description(CardManager.Instance);
        statusLabel.text=availability?.Replace("Backpack Full","Trick slots full");
        priceLabel.text=offer.kind==ShopOfferKind.Investment?"STAKE":offer.kind==ShopOfferKind.Supply?"3 / CARD":$"{offer.price} Credits";
        var theme=DealbreakUIStyle.Theme;if(!theme)return;
        DealbreakUIStyle.Text(titleLabel,theme.headingFont,29,theme.ink);
        DealbreakUIStyle.Text(priceLabel,theme.numericFont,23,theme.ink);
        DealbreakUIStyle.Text(descriptionLabel,theme.bodyFont,20,theme.ink);
        DealbreakUIStyle.Text(statusLabel,theme.bodyFont,18,theme.brick);
        foreach(var label in new[]{titleLabel,descriptionLabel,statusLabel,priceLabel})
        {label.alignment=TextAlignmentOptions.TopLeft;label.overflowMode=TextOverflowModes.Overflow;label.margin=Vector4.zero;}
        priceLabel.alignment=TextAlignmentOptions.TopRight;
        if(!purchaseButton)
        {
            var control=DealbreakUIStyle.Child(transform,"OfferBuy",typeof(Image),typeof(Button));
            purchaseButton=control.GetComponent<Button>();purchaseButton.targetGraphic=control.GetComponent<Image>();
            var text=DealbreakUIStyle.Child(control,"BuyLabel",typeof(TextMeshProUGUI));DealbreakUIStyle.Place(text,0,0,1,1);
            text.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Center;
        }
        SetSelected(false);
    }
    public void SetSelected(bool selected)
    {
        _selected=selected;
        if(Offer==null||!descriptionLabel)return;
        float width=Mathf.Max(180,((RectTransform)transform.parent).rect.width*.90f);
        float descriptionHeight=descriptionLabel.GetPreferredValues(descriptionLabel.text,width,0).y+12;
        float height=100+descriptionHeight+Mathf.Max(30,statusLabel.GetPreferredValues(statusLabel.text,width,0).y+8)+(selected?64:0);
        var layout=GetComponent<LayoutElement>();if(!layout)layout=gameObject.AddComponent<LayoutElement>();layout.minHeight=layout.preferredHeight=Mathf.Max(180,height);
        var desc=descriptionLabel.rectTransform;desc.anchorMin=new Vector2(.05f,1);desc.anchorMax=new Vector2(.95f,1);desc.pivot=new Vector2(.5f,1);desc.anchoredPosition=new Vector2(0,-65);desc.sizeDelta=new Vector2(0,descriptionHeight);
        var title=titleLabel.rectTransform;title.anchorMin=new Vector2(.05f,1);title.anchorMax=new Vector2(.69f,1);title.offsetMin=new Vector2(0,-58);title.offsetMax=new Vector2(0,-16);
        var price=priceLabel.rectTransform;price.anchorMin=new Vector2(.69f,1);price.anchorMax=new Vector2(.95f,1);price.offsetMin=new Vector2(0,-58);price.offsetMax=new Vector2(0,-18);
        var status=statusLabel.rectTransform;status.anchorMin=new Vector2(.05f,0);status.anchorMax=new Vector2(.95f,0);status.pivot=new Vector2(.5f,0);status.anchoredPosition=new Vector2(0,selected?70:14);status.sizeDelta=new Vector2(0,Mathf.Max(25,height-100-descriptionHeight-(selected?64:0)));
        purchaseButton.gameObject.SetActive(selected);
        var buy=(RectTransform)purchaseButton.transform;buy.anchorMin=new Vector2(.58f,0);buy.anchorMax=new Vector2(.95f,0);buy.offsetMin=new Vector2(0,14);buy.offsetMax=new Vector2(0,60);
        RefreshBorder();
    }
    void RefreshBorder(){var frame=transform.Find("Editorial Frame")?.GetComponent<EditorialFrame>();if(frame){frame.rim=_selected?DealbreakUIStyle.Theme.brick:_hover||_focus?DealbreakUIStyle.Theme.ochre:DealbreakUIStyle.Theme.ink;frame.border=_selected||_hover||_focus?5:3;frame.Refresh();}}
    public void OnPointerEnter(PointerEventData _){_hover=true;RefreshBorder();}
    public void OnPointerExit(PointerEventData _){_hover=false;RefreshBorder();}
    public void OnSelect(BaseEventData _){_focus=true;RefreshBorder();}
    public void OnDeselect(BaseEventData _){_focus=false;RefreshBorder();}
    public void RefreshPurchase(string reason,int cost)
    {
        if(!purchaseButton)return;
        purchaseButton.interactable=string.IsNullOrEmpty(reason)&&!GameplayInputGate.IsBlocked;
        purchaseButton.GetComponentInChildren<TMP_Text>(true).text=$"BUY  {cost} Credits";
        DealbreakUIStyle.Button(purchaseButton,EditorialButton.Role.Primary);
    }
    TMP_Text Label(string name,float x1,float y1,float x2,float y2)
    {var r=DealbreakUIStyle.Child(transform,name,typeof(TextMeshProUGUI));DealbreakUIStyle.Place(r,x1,y1,x2,y2);return r.GetComponent<TMP_Text>();}
}
