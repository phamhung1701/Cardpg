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
    TMP_InputField _investmentStakeInput;
    TMP_Text _investmentResultText;
    Button _sellModeButton;
    TMP_Text _sellModeLabel;
    ScrollRect _offersScroll;
    RectTransform _selectedOfferRect;
    Vector2 _selectedOfferAnchorMax;

    string _selectedOfferId;
    bool _sellingMode;
    long _selectedArtifactInstanceId;
    int _selectedConsumableSlot = -1;
    int _selectedSaleAuthoredPrice = -1;
    ConsumableInstance _selectedConsumableInstance;
    public string SelectedOfferId => _selectedOfferId;
    public bool SellingMode => _sellingMode;

    void OnEnable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowShop += Open;
            RunManager.Instance.OnHideShop += Close;
            RunManager.Instance.OnShopOffersChanged += RefreshItems;
        }
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnGoldChanged += HandleGoldChanged;
            CardManager.Instance.OnBuildChanged += RefreshItems;
        }
        if (continueButton) continueButton.onClick.AddListener(OnContinue);
        if (buyButton) buyButton.onClick.AddListener(OnBuy);
        EnsureInvestmentControls();
        EnsureOfferScrolling();
        EnsureSellingControls();
        if (RunManager.Instance != null) RunManager.Instance.OnInvestmentResult += ShowInvestmentResult;
        Close();
    }

    void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowShop -= Open;
            RunManager.Instance.OnHideShop -= Close;
            RunManager.Instance.OnShopOffersChanged -= RefreshItems;
            RunManager.Instance.OnInvestmentResult -= ShowInvestmentResult;
        }
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnGoldChanged -= HandleGoldChanged;
            CardManager.Instance.OnBuildChanged -= RefreshItems;
        }
        if (continueButton) continueButton.onClick.RemoveListener(OnContinue);
        if (buyButton) buyButton.onClick.RemoveListener(OnBuyOrConfirmSale);
        if (_sellModeButton) _sellModeButton.onClick.RemoveListener(ToggleSellingMode);
        Close();
    }

    void EnsureInvestmentControls()
    {
        if (panel == null || _investmentStakeInput != null) return;
        var card = itemsContainer != null ? itemsContainer.parent : panel.transform;
        _selectedOfferRect = selectedOfferText != null ? selectedOfferText.rectTransform : null;
        if (_selectedOfferRect != null) _selectedOfferAnchorMax = _selectedOfferRect.anchorMax;
        var inputObject = new GameObject("InvestmentStakeInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObject.transform.SetParent(card, false);
        var rect = (RectTransform)inputObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.82f, 0.19f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(150f, 32f);
        var input = inputObject.GetComponent<TMP_InputField>();
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.placeholder = CreateInputLabel(inputObject.transform, "Stake (Gold)", true);
        input.textComponent = CreateInputLabel(inputObject.transform, string.Empty, false);
        input.targetGraphic = inputObject.GetComponent<Image>();
        inputObject.SetActive(false);
        _investmentStakeInput = input;
        input.onEndEdit.AddListener(HandleInvestmentStakeEdited);

        var resultObject = new GameObject("InvestmentResult", typeof(RectTransform), typeof(TextMeshProUGUI));
        resultObject.transform.SetParent(card, false);
        var resultRect = (RectTransform)resultObject.transform;
        resultRect.anchorMin = resultRect.anchorMax = new Vector2(0.5f, 0.84f);
        resultRect.anchoredPosition = Vector2.zero;
        resultRect.sizeDelta = new Vector2(680f, 32f);
        _investmentResultText = resultObject.GetComponent<TMP_Text>();
        _investmentResultText.alignment = TextAlignmentOptions.Center;
        _investmentResultText.fontSize = 16f;
        _investmentResultText.raycastTarget = false;
    }

    void EnsureSellingControls()
    {
        if (panel == null) return;
        if (_sellModeButton == null)
        {
            var card = _offersScroll != null ? _offersScroll.transform.parent :
                itemsContainer != null ? itemsContainer.parent : panel.transform;
            var go = new GameObject("ShopSellMode", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(card, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.87f, 0.9f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(130f, 32f);
            go.GetComponent<Image>().color = new Color(0.18f, 0.24f, 0.32f, 1f);
            _sellModeButton = go.GetComponent<Button>();
            _sellModeLabel = CreateInputLabel(go.transform, "Sell Items", false);
            _sellModeLabel.alignment = TextAlignmentOptions.Center;
            _sellModeLabel.raycastTarget = false;
        }
        _sellModeButton.onClick.RemoveListener(ToggleSellingMode);
        _sellModeButton.onClick.AddListener(ToggleSellingMode);
        if (buyButton)
        {
            buyButton.onClick.RemoveListener(OnBuy);
            buyButton.onClick.RemoveListener(OnBuyOrConfirmSale);
            buyButton.onClick.AddListener(OnBuyOrConfirmSale);
        }
    }

    void OnBuyOrConfirmSale()
    {
        if (_sellingMode) OnConfirmSale();
        else OnBuy();
    }

    void ToggleSellingMode()
    {
        if (GameplayInputGate.IsBlocked || panel == null || !panel.activeSelf) return;
        _sellingMode = !_sellingMode;
        ClearSaleSelection();
        if (_sellModeLabel) _sellModeLabel.text = _sellingMode ? "Return to Buying" : "Sell Items";
        RefreshItems();
    }

    void ClearSaleSelection()
    {
        _selectedArtifactInstanceId = 0;
        _selectedConsumableSlot = -1;
        _selectedSaleAuthoredPrice = -1;
        _selectedConsumableInstance = null;
        if (buyButton) SetButtonLabel(buyButton, _sellingMode ? "Confirm Sale" : "Buy");
        if (buyButton) buyButton.interactable = false;
    }

    static void SetButtonLabel(Button button, string label)
    {
        if (button && button.GetComponentInChildren<TMP_Text>())
            button.GetComponentInChildren<TMP_Text>().text = label;
    }

    void SelectArtifactForSale(long instanceId)
    {
        if (GameplayInputGate.IsBlocked || !IsShopOpen()) return;
        var item = CardManager.Instance != null ? CardManager.Instance.GetArtifactInstanceById(instanceId) : null;
        if (item == null || item.Definition == null || item.Definition.price < 0) return;
        _selectedArtifactInstanceId = instanceId;
        _selectedConsumableSlot = -1;
        _selectedSaleAuthoredPrice = item.Definition.price;
        _selectedConsumableInstance = null;
        UpdateSaleDetails(item.Definition.displayName, item.Definition.price / 2);
    }

    void SelectConsumableForSale(int slot, ConsumableInstance instance)
    {
        if (GameplayInputGate.IsBlocked || !IsShopOpen() || instance == null || instance.Definition == null ||
            instance.Definition.price < 0) return;
        _selectedArtifactInstanceId = 0;
        _selectedConsumableSlot = slot;
        _selectedSaleAuthoredPrice = instance.Definition.price;
        _selectedConsumableInstance = instance;
        UpdateSaleDetails(instance.Definition.displayName, instance.Definition.price / 2);
    }

    bool IsShopOpen() => panel != null && panel.activeSelf && RunManager.Instance != null &&
        RunManager.Instance.ActiveNode != null && RunManager.Instance.ActiveNode.kind == MapNodeType.Shop &&
        !RunManager.Instance.ActiveNode.completed;

    void UpdateSaleDetails(string itemName, int payout)
    {
        if (buyButton)
        {
            SetButtonLabel(buyButton, "Confirm Sale");
            buyButton.interactable = true;
        }
        if (selectedOfferText) selectedOfferText.text = $"Sell {itemName} for {payout}g? Confirm to complete sale.";
    }

    void OnConfirmSale()
    {
        if (GameplayInputGate.IsBlocked || !IsShopOpen() || !_sellingMode) return;
        var run = RunManager.Instance;
        bool sold = _selectedArtifactInstanceId != 0
            ? run.SellArtifact(_selectedArtifactInstanceId, _selectedSaleAuthoredPrice)
            : _selectedConsumableInstance != null &&
                run.SellConsumable(_selectedConsumableSlot, _selectedConsumableInstance, _selectedSaleAuthoredPrice);
        if (!sold)
        {
            ClearSaleSelection();
            UpdateSelectionDetails("Sale unavailable; item may have changed.");
            return;
        }
        ClearSaleSelection();
        RefreshItems();
    }

    void EnsureOfferScrolling()
    {
        if (_offersScroll != null || itemsContainer is not RectTransform content || content.parent is not RectTransform parent)
            return;
        var viewportObject = new GameObject("ShopOffersViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        var viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(parent, false);
        viewport.anchorMin = content.anchorMin;
        viewport.anchorMax = content.anchorMax;
        viewport.pivot = content.pivot;
        viewport.anchoredPosition = content.anchoredPosition + Vector2.up * 38f;
        viewport.sizeDelta = content.sizeDelta - new Vector2(0f, 76f);
        var image = viewportObject.GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout != null) layout.childControlHeight = true;
        var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _offersScroll = viewportObject.GetComponent<ScrollRect>();
        _offersScroll.viewport = viewport;
        _offersScroll.content = content;
        _offersScroll.horizontal = false;
        _offersScroll.vertical = true;
        _offersScroll.movementType = ScrollRect.MovementType.Clamped;
    }

    static TMP_Text CreateInputLabel(Transform parent, string value, bool placeholder)
    {
        var labelObject = new GameObject(placeholder ? "Placeholder" : "Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 2f);
        labelRect.offsetMax = new Vector2(-8f, -2f);
        var label = labelObject.GetComponent<TMP_Text>();
        label.text = value;
        label.fontSize = 18f;
        label.color = placeholder ? Color.gray : Color.white;
        return label;
    }

    void HandleInvestmentStakeEdited(string value)
    {
        if (!int.TryParse(value, out int stake) || RunManager.Instance == null || string.IsNullOrEmpty(_selectedOfferId))
        {
            UpdateSelectionDetails("Enter a whole Gold amount from 1 to your current Gold.");
            return;
        }
        if (!RunManager.Instance.SetInvestmentStake(_selectedOfferId, stake))
            UpdateSelectionDetails("Investment stake must be between 1g and your current Gold.");
        else UpdateSelectionDetails();
    }

    void ShowInvestmentResult(string message)
    {
        if (_investmentResultText) _investmentResultText.text = message;
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
            BoardPanelTransition.Hide(panel);
            return;
        }
#endif
        BoardPanelTransition.Show(panel);
        RefreshItems();
    }

    void Close()
    {
        _selectedOfferId = null;
        _sellingMode = false;
        ClearSaleSelection();
        if (_sellModeLabel) _sellModeLabel.text = "Sell Items";
        BoardPanelTransition.Hide(panel);
        UpdateSelectionDetails();
    }

    void RefreshItems()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevModeRuntime.Enabled && DevModeRuntime.UnlimitedShopOffers)
        {
            BoardPanelTransition.Hide(panel);
            return;
        }
#endif
        _selectedOfferId = null;
        if (_investmentStakeInput) _investmentStakeInput.gameObject.SetActive(false);
        ClearSaleSelection();
        UpdateSelectionDetails();
        if (itemsContainer == null || itemButtonPrefab == null) return;

        foreach (Transform child in itemsContainer)
        {
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        var cm = CardManager.Instance;
        if (_sellingMode)
        {
            RefreshSellItems(cm);
            Canvas.ForceUpdateCanvases();
            if (_offersScroll != null)
            {
                _offersScroll.velocity = Vector2.zero;
                _offersScroll.verticalNormalizedPosition = 1f;
            }
            return;
        }
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
            string priceLabel = offer.kind == ShopOfferKind.Investment
                ? (offer.investmentStake > 0 ? $"{offer.investmentStake}g stake" : "Set stake")
                : $"{offer.price}g";
                text.text = $"{offer.Icon} {offer.DisplayName} - {priceLabel}\n{offer.Description(cm)}{status}";
                text.overflowMode = TextOverflowModes.Overflow;
                var row = btn.GetComponent<LayoutElement>() ?? btn.AddComponent<LayoutElement>();
                float textWidth = Mathf.Max(1f, ((RectTransform)itemsContainer).rect.width - 32f);
                row.minHeight = Mathf.Max(58f, text.GetPreferredValues(text.text, textWidth, 0f).y + 16f);
                row.preferredHeight = row.minHeight;
            }

            var button = btn.GetComponent<Button>();
            if (button)
            {
                // Unavailable offers remain inspectable; Buy always revalidates in RunManager.
                string stableId = offer.StableId;
                button.onClick.AddListener(() => SelectOffer(stableId));
            }
        }
        Canvas.ForceUpdateCanvases();
        if (_offersScroll != null)
        {
            _offersScroll.velocity = Vector2.zero;
            _offersScroll.verticalNormalizedPosition = 1f;
        }
    }

    void RefreshSellItems(CardManager cards)
    {
        if (cards == null) return;
        for (int i = 0; i < cards.OwnedArtifactInstances.Count; i++)
        {
            var artifact = cards.OwnedArtifactInstances[i];
            if (artifact == null || artifact.Definition == null || artifact.Definition.price < 0) continue;
            AddSellRow($"artifact:{artifact.Id}", artifact.Definition.icon, artifact.Definition.displayName,
                artifact.Definition.price / 2, () => SelectArtifactForSale(artifact.Id));
        }
        for (int slot = 0; slot < cards.BackpackSlotsUsed; slot++)
        {
            var stackInstance = cards.GetConsumableInstanceAtSlot(slot);
            if (stackInstance == null || stackInstance.Definition == null || stackInstance.Definition.price < 0) continue;
            int capturedSlot = slot;
            var capturedInstance = stackInstance;
            AddSellRow($"consumable:{slot}", stackInstance.Definition.icon, stackInstance.Definition.displayName,
                stackInstance.Definition.price / 2, () => SelectConsumableForSale(capturedSlot, capturedInstance));
        }
        if (itemsContainer.childCount == 0 && selectedOfferText)
            selectedOfferText.text = "No owned Artifacts or Consumables to sell.";
    }

    void AddSellRow(string rowName, string icon, string itemName, int payout, UnityEngine.Events.UnityAction onClick)
    {
        var row = Instantiate(itemButtonPrefab, itemsContainer);
        row.name = $"Sell_{rowName.Replace(':', '_')}";
        var label = row.GetComponentInChildren<TMP_Text>();
        if (label)
        {
            label.text = $"{icon} {itemName} — Sell for {payout}g";
            var layout = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
            layout.minHeight = 58f;
            layout.preferredHeight = 58f;
        }
        var button = row.GetComponent<Button>();
        if (button) button.onClick.AddListener(onClick);
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
        var selectedOffer = FindOffer(stableId);
        bool investmentSelected = selectedOffer != null && selectedOffer.kind == ShopOfferKind.Investment;
        if (_investmentStakeInput)
        {
            _investmentStakeInput.gameObject.SetActive(investmentSelected);
            if (_selectedOfferRect != null)
                _selectedOfferRect.anchorMax = investmentSelected
                    ? new Vector2(0.7f, _selectedOfferAnchorMax.y) : _selectedOfferAnchorMax;
            if (investmentSelected)
            {
                if (selectedOffer.investmentStake <= 0 && CardManager.Instance != null && CardManager.Instance.gold > 0)
                    RunManager.Instance.SetInvestmentStake(stableId, 1);
                _investmentStakeInput.SetTextWithoutNotify(selectedOffer.investmentStake > 0 ? selectedOffer.investmentStake.ToString() : string.Empty);
            }
        }
        foreach (Transform child in itemsContainer)
        {
            var button = child.GetComponent<Button>();
            if (!button) continue;
            var colors = button.colors;
            bool selected = child.name == $"ShopOffer_{stableId.Replace(':', '_')}";
            // Persistent outline remains visible even while the button is hovered/focused.
            colors.normalColor = selected ? new Color(0.83f, 0.88f, 0.91f) : Color.white;
            button.colors = colors;
            var outline = child.GetComponent<Outline>();
            if (outline == null) outline = child.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.62f, 0.72f, 0.78f, 0.72f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.enabled = selected;
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
            selectedOfferText.text = _sellingMode
                ? "Select an owned Artifact or Consumable to sell."
                : "Select an offer to inspect before buying.";
            return;
        }
        var reason = RunManager.Instance.GetShopOfferUnavailableReason(_selectedOfferId);
        string status = string.IsNullOrEmpty(reason) ? "Ready to buy" : reason;
        string displayedPrice = selected.kind == ShopOfferKind.Investment
            ? (selected.investmentStake > 0 ? $"{selected.investmentStake}g stake" : "Stake not set")
            : $"{selected.price}g";
        selectedOfferText.text = $"SELECTED: {selected.Icon} {selected.DisplayName} — {displayedPrice}\n" +
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
        RunManager.Instance.OnShopDone();
    }
}
