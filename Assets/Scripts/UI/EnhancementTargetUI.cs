using System.Collections.Generic;
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
    int _mutationSlotIndex = -1;
    readonly List<int> _mutationCardIds = new();
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
    public bool CanOpenDeckMutationTarget => CanOpenEventEnhancementPicker;

    public void OpenDeckMutationTarget(int slotIndex)
    {
        var cards = CardManager.Instance;
        var item = cards != null ? cards.GetConsumableAtSlot(slotIndex) : null;
        if (!CanOpenDeckMutationTarget || GameplayInputGate.IsBlocked ||
            !cards.CanUseConsumableAtSlot(slotIndex, CombatManager.Instance) ||
            item.effectType is not (ConsumableEffectType.DuplicateCard or
                ConsumableEffectType.DestroyCards or ConsumableEffectType.ChangeSuit)) return;
        _shopOfferId = null;
        _upgradeIndex = -1;
        _eventChoiceIndex = -1;
        _consumableSlotIndex = -1;
        _mutationSlotIndex = slotIndex;
        _mutationCardIds.Clear();
        if (titleLabel) titleLabel.text = $"CHOOSE CARD: {item.displayName}";
        if (panel) { panel.SetActive(true); panel.transform.SetAsLastSibling(); }
        RefreshCards();
        var scroll = cardsContainer ? cardsContainer.GetComponentInParent<ScrollRect>() : null;
        if (scroll) scroll.verticalNormalizedPosition = 1f;
    }

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
        if (run == null || IsInputBlockedForCurrentChoice() ||
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
        _mutationSlotIndex = -1;
        _mutationCardIds.Clear();
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
        if (IsInputBlockedForCurrentChoice() || enhancement == null || _eventChoiceIndex < 0) return;
        _eventEnhancement = enhancement;
        Show(new ShopOffer { kind = ShopOfferKind.Enhancement, enhancement = enhancement, price = 0 });
    }

    void Show(ShopOffer offer)
    {
        _mutationSlotIndex = -1;
        _mutationCardIds.Clear();
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
        _mutationSlotIndex = -1;
        _mutationCardIds.Clear();
        _selectedCardId = 0;
        if (panel) panel.SetActive(false);
        if (confirmButton) confirmButton.interactable = false;
    }

    bool IsInputBlockedForCurrentChoice() => GameplayInputGate.IsBlocked &&
        !(RunManager.Instance != null && RunManager.Instance.IsPendingHammerReward &&
          GameplayInputGate.Reasons == GameplayInputBlockReason.ArtifactChoice);

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
                string reason = _mutationSlotIndex >= 0
                    ? cards.CanTargetDeckMutationAtSlot(_mutationSlotIndex, id) ? "" : "Not a valid target"
                    : RunManager.Instance.GetEnhancementTargetUnavailableReason(id);
                var obj = Instantiate(cardButtonPrefab, cardsContainer);
                obj.name = $"TargetCard_{id}";
                var label = obj.GetComponentInChildren<TMP_Text>();
                bool selected = _mutationSlotIndex >= 0 ? _mutationCardIds.Contains(id) : id == _selectedCardId;
                if (label) label.text = (selected ? "SELECTED " : "") +
                    $"{card.SuitSymbol} {card.DisplayName}\n#{id}" +
                    (string.IsNullOrEmpty(reason) ? "" : $"\n{reason}");
                var button = obj.GetComponent<Button>();
                if (button)
                {
                    button.onClick.AddListener(() => SelectCard(id));
                    var colors = button.colors;
                    colors.normalColor = selected ? new Color(0.52f, 0.86f, 1f) :
                        string.IsNullOrEmpty(reason) ? Color.white : new Color(0.48f, 0.48f, 0.48f);
                    button.colors = colors;
                }
            }
        }
        UpdateFeedback();
    }

    void SelectCard(int id)
    {
        if (IsInputBlockedForCurrentChoice() || !panel || !panel.activeSelf) return;
        if (_mutationSlotIndex >= 0)
        {
            var cards = CardManager.Instance;
            if (cards == null || !cards.CanTargetDeckMutationAtSlot(_mutationSlotIndex, id)) return;
            if (!_mutationCardIds.Remove(id))
            {
                if (cards.GetConsumableAtSlot(_mutationSlotIndex).effectType != ConsumableEffectType.DestroyCards)
                    _mutationCardIds.Clear();
                if (_mutationCardIds.Count < 2) _mutationCardIds.Add(id);
            }
        }
        else _selectedCardId = id;
        RefreshCards();
    }

    void UpdateFeedback(string failure = null)
    {
        if (_mutationSlotIndex >= 0)
        {
            var cards = CardManager.Instance;
            var item = cards != null ? cards.GetConsumableAtSlot(_mutationSlotIndex) : null;
            bool valid = item != null && cards.CanUseConsumableAtSlot(_mutationSlotIndex, CombatManager.Instance) &&
                _mutationCardIds.Count > 0 && _mutationCardIds.Count <=
                    (item.effectType == ConsumableEffectType.DestroyCards ? 2 : 1) &&
                _mutationCardIds.Count < cards.ownedCards.Count;
            foreach (int id in _mutationCardIds)
                valid &= cards.CanTargetDeckMutationAtSlot(_mutationSlotIndex, id);
            if (feedbackLabel) feedbackLabel.text = failure ?? (valid
                ? $"Selected {_mutationCardIds.Count} card(s). Confirm or cancel for free."
                : "Select eligible card(s). Torch destroys one or two; keep at least one card.");
            if (confirmButton) confirmButton.interactable = valid;
            return;
        }
        string reason = failure ?? (_selectedCardId == 0 ? "Select an owned card, then confirm." :
            RunManager.Instance.GetEnhancementTargetUnavailableReason(_selectedCardId));
        if (feedbackLabel) feedbackLabel.text = string.IsNullOrEmpty(reason)
            ? $"Selected card #{_selectedCardId}. Confirm to apply." : reason;
        if (confirmButton) confirmButton.interactable = _selectedCardId != 0;
    }

    void Confirm()
    {
        if (IsInputBlockedForCurrentChoice() || !panel || !panel.activeSelf) return;
        if (_mutationSlotIndex >= 0)
        {
            var cards = CardManager.Instance;
            if (cards != null && cards.UseDeckMutationConsumableAtSlot(_mutationSlotIndex, _mutationCardIds.ToArray()))
                Hide();
            else UpdateFeedback("Consumable or target unavailable");
            return;
        }
        if (_selectedCardId == 0) return;
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
