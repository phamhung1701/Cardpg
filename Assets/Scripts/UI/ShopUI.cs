using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopUI : MonoBehaviour
{
    [Header("References")]
    public GameObject panel;
    public Transform itemsContainer;
    public GameObject itemButtonPrefab;
    public Button continueButton;
    public TMP_Text goldText;

    void OnEnable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowShop += Open;
            RunManager.Instance.OnHideShop += Close;
        }
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnGoldChanged += HandleGoldChanged;
            CardManager.Instance.OnBuildChanged += RefreshItems;
        }
        if (continueButton) continueButton.onClick.AddListener(OnContinue);
        Close();
    }

    void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowShop -= Open;
            RunManager.Instance.OnHideShop -= Close;
        }
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnGoldChanged -= HandleGoldChanged;
            CardManager.Instance.OnBuildChanged -= RefreshItems;
        }
        if (continueButton) continueButton.onClick.RemoveListener(OnContinue);
    }

    void HandleGoldChanged(int gold)
    {
        if (goldText) goldText.text = $"{gold}g";
        if (panel != null && panel.activeSelf) RefreshItems();
    }

    void Open()
    {
        if (panel) panel.SetActive(true);
        RefreshItems();
    }

    void Close()
    {
        if (panel) panel.SetActive(false);
    }

    void RefreshItems()
    {
        if (itemsContainer == null || itemButtonPrefab == null) return;

        foreach (Transform child in itemsContainer)
            Destroy(child.gameObject);

        var cm = CardManager.Instance;
        var offers = RunManager.Instance != null
            ? RunManager.Instance.GetCurrentShopOffers()
            : System.Array.Empty<ShopOffer>();
        foreach (var offer in offers)
        {
            string unavailableReason = RunManager.Instance != null
                ? RunManager.Instance.GetShopOfferUnavailableReason(offer.StableId)
                : "Offer unavailable";
            bool available = string.IsNullOrEmpty(unavailableReason);

            var btn = Instantiate(itemButtonPrefab, itemsContainer);
            btn.name = $"ShopOffer_{offer.StableId.Replace(':', '_')}";
            var text = btn.GetComponentInChildren<TMP_Text>();
            if (text)
            {
                string status = available ? string.Empty : $"\n<color=#DFA0A0>{unavailableReason}</color>";
                text.text = $"{offer.Icon} {offer.DisplayName} - {offer.price}g\n{offer.Description(cm)}{status}";
            }

            var button = btn.GetComponent<Button>();
            if (button)
            {
                button.interactable = available;
                string stableId = offer.StableId;
                button.onClick.AddListener(() =>
                {
                    if (RunManager.Instance.PurchaseShopOffer(stableId)) RefreshItems();
                });
            }
        }
    }

    void OnContinue()
    {
        Close();
        RunManager.Instance.OnShopDone();
    }
}
