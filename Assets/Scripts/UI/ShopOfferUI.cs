using TMPro;
using UnityEngine;

/// <summary>Compact offer anatomy; ShopUI owns selection and RunManager owns transactions.</summary>
public sealed class ShopOfferUI : MonoBehaviour
{
    public TMP_Text titleLabel,priceLabel,statusLabel;
    public void Bind(ShopOffer offer,string availability)
    {
        if(!titleLabel)titleLabel=GetComponentInChildren<TMP_Text>();
        if(!priceLabel)priceLabel=Label("OfferPrice",.50f,.10f,.92f,.44f);
        if(!statusLabel)statusLabel=Label("OfferAvailability",.07f,.08f,.48f,.44f);
        DealbreakUIStyle.Place(titleLabel.transform,.07f,.50f,.91f,.92f);
        titleLabel.text=offer.DisplayName;
        priceLabel.text=offer.kind==ShopOfferKind.Investment?"SET STAKE":offer.kind==ShopOfferKind.Supply?"3 / CARD":$"{offer.price} Credits";
        statusLabel.text=string.IsNullOrEmpty(availability)?offer.IsService?"SERVICE":"GOODS":
            availability.Contains("Capacity")?"CAPACITY FULL":availability.Contains("Credits")?"LOW CREDITS":"UNAVAILABLE";
        if(string.IsNullOrEmpty(availability))
        {
            string effect=offer.Description(CardManager.Instance)??string.Empty;
            int marker=effect.IndexOf("Effect: ");if(marker>=0)effect=effect.Substring(marker+8);
            int line=effect.IndexOf('\n');if(line>=0)effect=effect.Substring(0,line);
            var words=effect.Split(' ');statusLabel.text=string.Join(" ",words,0,Mathf.Min(7,words.Length));
        }
        else if(offer.kind==ShopOfferKind.Investment&&offer.investmentStake<=0)statusLabel.text="SET STAKE";
        else if(!availability.StartsWith("Need ")&&!availability.Contains("Insufficient Credits")&&!availability.Contains("Capacity"))statusLabel.text="UNAVAILABLE";
        var theme=DealbreakUIStyle.Theme;if(!theme)return;
        DealbreakUIStyle.Text(titleLabel,theme.bodyFont,21,theme.paper);
        DealbreakUIStyle.Text(priceLabel,theme.numericFont,18,theme.ochre);
        DealbreakUIStyle.Text(statusLabel,theme.bodyFont,15,string.IsNullOrEmpty(availability)?theme.paper:theme.ochre);
        titleLabel.alignment=TextAlignmentOptions.MidlineLeft;priceLabel.alignment=TextAlignmentOptions.MidlineRight;
        statusLabel.alignment=TextAlignmentOptions.MidlineLeft;
        titleLabel.overflowMode=priceLabel.overflowMode=TextOverflowModes.Ellipsis;
        statusLabel.overflowMode=TextOverflowModes.Ellipsis;
    }
    TMP_Text Label(string name,float x1,float y1,float x2,float y2)
    {var r=DealbreakUIStyle.Child(transform,name,typeof(TextMeshProUGUI));DealbreakUIStyle.Place(r,x1,y1,x2,y2);return r.GetComponent<TMP_Text>();}
}
