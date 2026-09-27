using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation/input for choosing one owned CardInstance after selecting an Enhancement.
// The offer, price, node, and eligibility are always revalidated by RunManager on confirmation.
public sealed class EnhancementTargetUI : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text titleLabel;
    public TMP_Text feedbackLabel;
    public Transform cardsContainer;
    public GameObject cardButtonPrefab;
    public Button confirmButton;
    public Button backButton;

    string _shopOfferId;
    int _upgradeIndex = -1;
    int _eventChoiceIndex = -1;
    int _consumableSlotIndex = -1;
    CardEnhancementData _eventEnhancement;
    int _selectedCardId;
    public int SelectedCardId => _selectedCardId;

    void OnEnable()
    {
        var run = RunManager.Instance;
        if (run != null)
        {
            run.OnHideShop += Hide;
            run.OnHideUpgrade += Hide;
            run.OnHideEvent += Hide;
            run.OnRunStarted += HandleRunStarted;
        }
        if (CardManager.Instance != null) CardManager.Instance.OnBuildChanged += RefreshCards;
        if (confirmButton) confirmButton.onClick.AddListener(Confirm);
        if (backButton) backButton.onClick.AddListener(Back);
        Hide();
    }

    void OnDisable()
    {
        var run = RunManager.Instance;
        if (run != null)
        {
            run.OnHideShop -= Hide;
            run.OnHideUpgrade -= Hide;
            run.OnHideEvent -= Hide;
            run.OnRunStarted -= HandleRunStarted;
        }
        if (CardManager.Instance != null) CardManager.Instance.OnBuildChanged -= RefreshCards;
        if (confirmButton) confirmButton.onClick.RemoveListener(Confirm);
        if (backButton) backButton.onClick.RemoveListener(Back);
        Hide();
    }

    void HandleRunStarted(string seed) => Hide();

    public void OpenShop(string stableId)
    {
        var run = RunManager.Instance;
        if (run == null || GameplayInputGate.IsBlocked ||
            !string.IsNullOrEmpty(run.GetShopOfferUnavailableReason(stableId))) return;
        ShopOffer offer = null;
        foreach (var candidate in run.GetCurrentShopOffers())
            if (candidate.StableId == stableId) { offer = candidate; break; }
        if (offer == null || offer.kind != ShopOfferKind.Enhancement) return;
        _shopOfferId = stableId;
        _upgradeIndex = -1;
        _eventChoiceIndex = -1;
        _eventEnhancement = null;
        _consumableSlotIndex = -1;
        Show(offer);
    }

    public void OpenUpgrade(int index)
    {
        var run = RunManager.Instance;
        if (run == null || GameplayInputGate.IsBlocked ||
            run.ActiveNode == null || run.ActiveNode.kind != MapNodeType.Upgrade ||
            index < 0 || index >= run.GetCurrentUpgradeOffers().Count) return;
        var offer = run.GetCurrentUpgradeOffers()[index];
        if (offer.kind != ShopOfferKind.Enhancement || offer.enhancement == null) return;
        _shopOfferId = null;
        _upgradeIndex = index;
        _eventChoiceIndex = -1;
        _eventEnhancement = null;
        _consumableSlotIndex = -1;
        Show(offer);
    }

    public bool CanOpenEventEnhancementPicker => panel != null && cardsContainer != null &&
        cardButtonPrefab != null && confirmButton != null;
    public bool CanOpenConsumableTarget => CanOpenEventEnhancementPicker;

    public void OpenConsumableTarget(int slotIndex)
    {
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        var consumable = cards != null ? cards.GetConsumableAtSlot(slotIndex) : null;
        if (!CanOpenConsumableTarget || GameplayInputGate.IsBlocked || consumable == null ||
            consumable.effectType != ConsumableEffectType.ApplyEnhancement ||
            !cards.CanUseConsumableAtSlot(slotIndex, combat)) return;

        _shopOfferId = null;
        _upgradeIndex = -1;
        _eventChoiceIndex = -1;
        _eventEnhancement = null;
        _consumableSlotIndex = slotIndex;
        Show(new ShopOffer { kind = ShopOfferKind.Enhancement, enhancement = consumable.enhancementToApply, price = 0 });
    }

    public void OpenEventEnhancement(int choiceIndex)
    {
        var run = RunManager.Instance;
        if (run == null || GameplayInputGate.IsBlocked ||
            !string.IsNullOrEmpty(run.GetEventOptionUnavailableReason(choiceIndex))) return;
        _shopOfferId = null;
        _upgradeIndex = -1;
        _eventChoiceIndex = choiceIndex;
        _eventEnhancement = null;
        _consumableSlotIndex = -1;
        _selectedCardId = 0;
        ShowEventEnhancements();
    }

    void ShowEventEnhancements()
    {
        var run = RunManager.Instance;
        var candidates = run != null ? run.GetEventEnhancementsForChoice(_eventChoiceIndex) : null;
        if (!panel || !cardsContainer || !cardButtonPrefab || candidates == null) return;
        if (titleLabel) titleLabel.text = "CHOOSE AN ENHANCEMENT";
        if (feedbackLabel) feedbackLabel.text = "Choose one enhancement, then choose an owned card to receive it.";
        if (confirmButton) confirmButton.interactable = false;
        ClearTargetCards();
        foreach (var enhancement in candidates)
        {
            if (enhancement == null) continue;
            var obj = Instantiate(cardButtonPrefab, cardsContainer);
            obj.name = $"EventEnhancement_{enhancement.id}";
            var label = obj.GetComponentInChildren<TMP_Text>();
            if (label) label.text = $"{enhancement.icon} {enhancement.displayName}\n{enhancement.description}";
            var button = obj.GetComponent<Button>();
            if (button) button.onClick.AddListener(() => SelectEventEnhancement(enhancement));
        }
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
    }

    void SelectEventEnhancement(CardEnhancementData enhancement)
    {
        if (GameplayInputGate.IsBlocked || enhancement == null || _eventChoiceIndex < 0) return;
        _eventEnhancement = enhancement;
        Show(new ShopOffer { kind = ShopOfferKind.Enhancement, enhancement = enhancement, price = 0 });
    }

    void Show(ShopOffer offer)
    {
        _selectedCardId = 0;
        if (titleLabel) titleLabel.text = $"CHOOSE CARD: {offer.Icon} {offer.DisplayName}";
        if (panel) { panel.SetActive(true); panel.transform.SetAsLastSibling(); }
        RefreshCards();
        var scroll = cardsContainer ? cardsContainer.GetComponentInParent<ScrollRect>() : null;
        if (scroll) scroll.verticalNormalizedPosition = 1f;
    }

    public void Hide()
    {
        _shopOfferId = null;
        _upgradeIndex = -1;
        _eventChoiceIndex = -1;
        _eventEnhancement = null;
        _consumableSlotIndex = -1;
        _selectedCardId = 0;
        if (panel) panel.SetActive(false);
        if (confirmButton) confirmButton.interactable = false;
    }

    void Back()
    {
        if (_eventChoiceIndex >= 0 && _eventEnhancement != null)
        {
            _eventEnhancement = null;
            _selectedCardId = 0;
            ShowEventEnhancements();
            return;
        }
        Hide();
    }

    void ClearTargetCards()
    {
        if (!cardsContainer) return;
        foreach (Transform child in cardsContainer)
        {
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }

    void RefreshCards()
    {
        if (!panel || !panel.activeSelf || !cardsContainer || !cardButtonPrefab) return;
        ClearTargetCards();

        var cards = CardManager.Instance;
        if (cards != null)
        {
            foreach (var card in cards.ownedCards)
            {
                if (card == null) continue;
                int id = card.Id;
                string reason = RunManager.Instance.GetEnhancementTargetUnavailableReason(id);
                var obj = Instantiate(cardButtonPrefab, cardsContainer);
                obj.name = $"TargetCard_{id}";
                var label = obj.GetComponentInChildren<TMP_Text>();
                if (label) label.text = (id == _selectedCardId ? "SELECTED " : "") +
                    $"{card.SuitSymbol} {card.DisplayName}\n#{id}" +
                    (string.IsNullOrEmpty(reason) ? "" : $"\n{reason}");
                var button = obj.GetComponent<Button>();
                if (button)
                {
                    // Keep ineligible cards inspectable so the player can see why they cannot be chosen.
                    button.onClick.AddListener(() => SelectCard(id));
                    var colors = button.colors;
                    colors.normalColor = id == _selectedCardId ? new Color(0.52f, 0.86f, 1f) :
                        string.IsNullOrEmpty(reason) ? Color.white : new Color(0.48f, 0.48f, 0.48f);
                    button.colors = colors;
                }
            }
        }
        UpdateFeedback();
    }

    void SelectCard(int id)
    {
        if (GameplayInputGate.IsBlocked || !panel || !panel.activeSelf) return;
        _selectedCardId = id;
        RefreshCards();
    }

    void UpdateFeedback(string failure = null)
    {
        string reason = failure ?? (_selectedCardId == 0 ? "Select an owned card, then confirm." :
            RunManager.Instance.GetEnhancementTargetUnavailableReason(_selectedCardId));
        if (feedbackLabel) feedbackLabel.text = string.IsNullOrEmpty(reason)
            ? $"Selected card #{_selectedCardId}. Confirm to apply." : reason;
        if (confirmButton) confirmButton.interactable = _selectedCardId != 0;
    }

    void Confirm()
    {
        if (GameplayInputGate.IsBlocked || !panel || !panel.activeSelf || _selectedCardId == 0) return;
        var run = RunManager.Instance;
        if (run == null) return;
        string offerId = _shopOfferId;
        int index = _upgradeIndex;
        int targetId = _selectedCardId;
        bool success = _consumableSlotIndex >= 0
            ? CardManager.Instance != null && CardManager.Instance.UseEnhancementConsumableAtSlot(_consumableSlotIndex, targetId)
            : _eventChoiceIndex >= 0
                ? run.ChooseEventEnhancementTarget(_eventChoiceIndex, targetId, _eventEnhancement)
                : offerId != null
                    ? run.PurchaseShopEnhancement(offerId, targetId)
                    : run.ChooseUpgradeOffer(index, targetId);
        if (success) { Hide(); return; }
        string reason = run.GetEnhancementTargetUnavailableReason(targetId);
        if (string.IsNullOrEmpty(reason))
            reason = _consumableSlotIndex >= 0 ? "Consumable or target unavailable" :
                _eventChoiceIndex >= 0 ? "Event enhancement unavailable" :
                offerId != null ? run.GetShopOfferUnavailableReason(offerId) : "Upgrade unavailable";
        RefreshCards();
        UpdateFeedback(string.IsNullOrEmpty(reason) ? "Enhancement unavailable" : reason);
    }
}
