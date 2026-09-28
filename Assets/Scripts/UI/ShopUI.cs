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
    public TMP_Text selectedOfferText;
    public Button buyButton;
    public EnhancementTargetUI enhancementTargetUI;

    string _selectedOfferId;
    public string SelectedOfferId => _selectedOfferId;

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
        if (buyButton) buyButton.onClick.AddListener(OnBuy);
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
        if (buyButton) buyButton.onClick.RemoveListener(OnBuy);
        Close();
    }

    void HandleGoldChanged(int gold)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (goldText) goldText.text = CardManager.Instance != null && CardManager.Instance.HasInfiniteMoney
            ? "∞ Gold" : $"{gold}g";
#else
        if (goldText) goldText.text = $"{gold}g";
#endif
        if (panel != null && panel.activeSelf) RefreshItems();
    }

    void Open()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevModeRuntime.Enabled && DevModeRuntime.UnlimitedShopOffers)
        {
            if (panel) panel.SetActive(false);
            return;
        }
#endif
        if (panel) panel.SetActive(true);
        RefreshItems();
    }

    void Close()
    {
        _selectedOfferId = null;
        if (panel) panel.SetActive(false);
        UpdateSelectionDetails();
    }

    void RefreshItems()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevModeRuntime.Enabled && DevModeRuntime.UnlimitedShopOffers)
        {
            if (panel) panel.SetActive(false);
            return;
        }
#endif
        _selectedOfferId = null;
        UpdateSelectionDetails();
        if (itemsContainer == null || itemButtonPrefab == null) return;

        foreach (Transform child in itemsContainer)
        {
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        var cm = CardManager.Instance;
        var offers = RunManager.Instance != null
            ? RunManager.Instance.GetCurrentShopOffers()
            : System.Array.Empty<ShopOffer>();
        foreach (var offer in offers)
        {
            string unavailableReason = RunManager.Instance != null
                ? RunManager.Instance.GetShopOfferUnavailableReason(offer.StableId)
                : "Offer unavailable";

            var btn = Instantiate(itemButtonPrefab, itemsContainer);
            btn.name = $"ShopOffer_{offer.StableId.Replace(':', '_')}";
            var text = btn.GetComponentInChildren<TMP_Text>();
            if (text)
            {
                string status = string.IsNullOrEmpty(unavailableReason) ? string.Empty : $"\n<color=#DFA0A0>{unavailableReason}</color>";
                text.text = $"{offer.Icon} {offer.DisplayName} - {offer.price}g\n{offer.Description(cm)}{status}";
            }

            var button = btn.GetComponent<Button>();
            if (button)
            {
                // Unavailable offers remain inspectable; Buy always revalidates in RunManager.
                string stableId = offer.StableId;
                button.onClick.AddListener(() => SelectOffer(stableId));
            }
        }
    }

    void SelectOffer(string stableId)
    {
        if (GameplayInputGate.IsBlocked || panel == null || !panel.activeSelf) return;
        var offers = RunManager.Instance != null ? RunManager.Instance.GetCurrentShopOffers() : null;
        if (offers == null) return;
        bool found = false;
        foreach (var offer in offers)
            if (offer.StableId == stableId) { found = true; break; }
        if (!found) return;

        _selectedOfferId = stableId;
        foreach (Transform child in itemsContainer)
        {
            var button = child.GetComponent<Button>();
            if (!button) continue;
            var colors = button.colors;
            // A persistent tint and label distinguish selection from hover/focus.
            colors.normalColor = child.name == $"ShopOffer_{stableId.Replace(':', '_')}"
                ? new Color(0.52f, 0.86f, 1f) : Color.white;
            button.colors = colors;
            var label = child.GetComponentInChildren<TMP_Text>();
            if (label)
            {
                const string marker = "SELECTED  ";
                if (label.text.StartsWith(marker)) label.text = label.text.Substring(marker.Length);
                if (child.name == $"ShopOffer_{stableId.Replace(':', '_')}") label.text = marker + label.text;
            }
        }
        UpdateSelectionDetails();
    }

    void UpdateSelectionDetails(string failure = null)
    {
        ShopOffer selected = null;
        var offers = RunManager.Instance != null ? RunManager.Instance.GetCurrentShopOffers() : null;
        if (offers != null)
            foreach (var offer in offers)
                if (offer.StableId == _selectedOfferId) { selected = offer; break; }

        if (buyButton) buyButton.interactable = selected != null;
        if (!selectedOfferText) return;
        if (!string.IsNullOrEmpty(failure))
        {
            selectedOfferText.text = failure;
            return;
        }
        if (selected == null)
        {
            selectedOfferText.text = "Select an offer to inspect before buying.";
            return;
        }
        var reason = RunManager.Instance.GetShopOfferUnavailableReason(_selectedOfferId);
        string status = string.IsNullOrEmpty(reason) ? "Ready to buy" : reason;
        selectedOfferText.text = $"SELECTED: {selected.Icon} {selected.DisplayName} — {selected.price}g\n" +
            $"{selected.Description(CardManager.Instance)}\n{status}";
    }

    void OnBuy()
    {
        if (GameplayInputGate.IsBlocked || panel == null || !panel.activeSelf || string.IsNullOrEmpty(_selectedOfferId)) return;
        var run = RunManager.Instance;
        if (run == null) return;
        string selectedId = _selectedOfferId;
        var offer = FindOffer(selectedId);
        if (offer != null && offer.kind == ShopOfferKind.Enhancement)
        {
            var unavailable = run.GetShopOfferUnavailableReason(selectedId);
            if (!string.IsNullOrEmpty(unavailable))
            {
                UpdateSelectionDetails(unavailable);
                return;
            }
            if (enhancementTargetUI) enhancementTargetUI.OpenShop(selectedId);
            return;
        }
        if (run.PurchaseShopOffer(selectedId))
        {
            RefreshItems();
            return;
        }

        // Revalidation failure must not commit the purchase; explain it before clearing stale selection.
        string reason = run.GetShopOfferUnavailableReason(selectedId);
        _selectedOfferId = null;
        RefreshItems();
        UpdateSelectionDetails(string.IsNullOrEmpty(reason) ? "Offer unavailable" : reason);
    }

    ShopOffer FindOffer(string stableId)
    {
        if (RunManager.Instance == null || string.IsNullOrEmpty(stableId)) return null;
        foreach (var offer in RunManager.Instance.GetCurrentShopOffers())
            if (offer.StableId == stableId) return offer;
        return null;
    }

    void OnContinue()
    {
        Close();
        RunManager.Instance.OnShopDone();
    }
}
