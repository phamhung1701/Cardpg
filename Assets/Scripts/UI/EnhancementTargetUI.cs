using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Owns hand-field Enhancement targeting for Shop/Upgrade/Event acquisitions plus
// the existing explicit card-list flows for owned-card consumables and deck mutations.
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
    CardEnhancementData _acquisitionEnhancement;
    int _selectedCardId;
    bool _replacementConfirmed;
    bool _acquisitionTargetMode;
    GameObject _originPanel;
    bool _restoreOriginOnClose;
    bool _hasSavedPanelLayout;
    CardView _targetedHandView;
    CardEnhancementData _replacementToConfirm;
    RectTransform _dialogCardRect;
    RectTransform _titleRect;
    RectTransform _feedbackRect;
    RectTransform _confirmRect;
    RectTransform _backRect;
    GameObject _targetListViewport;
    bool _targetListViewportWasActive;
    RectTransformSnapshot _dialogCardSnapshot, _titleSnapshot, _feedbackSnapshot, _confirmSnapshot, _backSnapshot;
    Vector2 _savedAnchorMin, _savedAnchorMax, _savedPivot, _savedSizeDelta;
    Vector3 _savedAnchoredPosition;
    public int SelectedCardId => _selectedCardId;

    struct RectTransformSnapshot
    {
        public Vector2 anchorMin, anchorMax, pivot, sizeDelta;
        public Vector3 anchoredPosition;

        public static RectTransformSnapshot Capture(RectTransform rect) => rect == null ? default : new RectTransformSnapshot
        {
            anchorMin = rect.anchorMin,
            anchorMax = rect.anchorMax,
            pivot = rect.pivot,
            sizeDelta = rect.sizeDelta,
            anchoredPosition = rect.anchoredPosition3D
        };

        public void Restore(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition3D = anchoredPosition;
        }
    }

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
        _consumableSlotIndex = -1;
        BeginAcquisitionTargeting(offer);
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
        _consumableSlotIndex = -1;
        BeginAcquisitionTargeting(offer);
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
        if (feedbackLabel) feedbackLabel.text = "Choose one Enhancement, then click a card in your hand to receive it.";
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
        BeginAcquisitionTargeting(new ShopOffer { kind = ShopOfferKind.Enhancement, enhancement = enhancement, price = 0 });
    }

    void BeginAcquisitionTargeting(ShopOffer offer)
    {
        if (offer == null || offer.enhancement == null || CardManager.Instance == null) return;
        _acquisitionTargetMode = true;
        _acquisitionEnhancement = offer.enhancement;
        _selectedCardId = 0;
        _replacementConfirmed = false;
        _replacementToConfirm = null;
        _mutationSlotIndex = -1;
        _mutationCardIds.Clear();
        _targetedHandView = null;
        _originPanel = _shopOfferId != null ? FindFirstObjectByType<ShopUI>()?.panel :
            _eventChoiceIndex >= 0 || _upgradeIndex >= 0 ? FindFirstObjectByType<RunEventUI>()?.panel : null;
        if (_originPanel) _originPanel.SetActive(false);
        if (titleLabel) titleLabel.text = $"{offer.Icon} {offer.DisplayName}";
        if (feedbackLabel) feedbackLabel.text = "Click a card in your hand to target this Enhancement.";
        if (confirmButton) confirmButton.interactable = false;
        SaveAndCompactPanel();
        if (panel) { panel.SetActive(true); panel.transform.SetAsLastSibling(); }
        CardManager.Instance.BeginHandEnhancementTargeting(HandleHandCardTargeted);
    }

    void HandleHandCardTargeted(CardView view)
    {
        if (!_acquisitionTargetMode || view == null || view.data == null || IsInputBlockedForCurrentChoice()) return;
        int id = view.data.Id;
        var run = RunManager.Instance;
        string unavailableReason = run != null
            ? run.GetEnhancementAcquisitionTargetUnavailableReason(id, _acquisitionEnhancement)
            : "Enhancement unavailable";
        if (!string.IsNullOrEmpty(unavailableReason))
        {
            if (_targetedHandView) _targetedHandView.SetSelected(false);
            _targetedHandView = null;
            _selectedCardId = 0;
            _replacementConfirmed = false;
            _replacementToConfirm = null;
            UpdateFeedback(unavailableReason);
            return;
        }
        if (_targetedHandView && _targetedHandView != view) _targetedHandView.SetSelected(false);
        _targetedHandView = view;
        _targetedHandView.SetSelected(true);
        _selectedCardId = id;
        _replacementConfirmed = false;
        _replacementToConfirm = null;
        UpdateFeedback();
    }

    void SaveAndCompactPanel()
    {
        if (!panel || _hasSavedPanelLayout) return;
        var rect = panel.transform as RectTransform;
        if (rect == null) return;
        _savedAnchorMin = rect.anchorMin;
        _savedAnchorMax = rect.anchorMax;
        _savedPivot = rect.pivot;
        _savedSizeDelta = rect.sizeDelta;
        _savedAnchoredPosition = rect.anchoredPosition3D;
        _dialogCardRect = titleLabel ? titleLabel.rectTransform.parent as RectTransform : null;
        _titleRect = titleLabel ? titleLabel.rectTransform : null;
        _feedbackRect = feedbackLabel ? feedbackLabel.rectTransform : null;
        _confirmRect = confirmButton ? confirmButton.transform as RectTransform : null;
        _backRect = backButton ? backButton.transform as RectTransform : null;
        _dialogCardSnapshot = RectTransformSnapshot.Capture(_dialogCardRect);
        _titleSnapshot = RectTransformSnapshot.Capture(_titleRect);
        _feedbackSnapshot = RectTransformSnapshot.Capture(_feedbackRect);
        _confirmSnapshot = RectTransformSnapshot.Capture(_confirmRect);
        _backSnapshot = RectTransformSnapshot.Capture(_backRect);
        // Acquisition starts while this overlay is still hidden; include inactive parents so the
        // authored CardViewport, not just its content, is disabled during hand-field targeting.
        var scroll = cardsContainer ? cardsContainer.GetComponentInParent<ScrollRect>(true) : null;
        _targetListViewport = scroll ? scroll.gameObject : cardsContainer ? cardsContainer.gameObject : null;
        _targetListViewportWasActive = _targetListViewport && _targetListViewport.activeSelf;
        if (_targetListViewport) _targetListViewport.SetActive(false);
        _hasSavedPanelLayout = true;

        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(460f, 190f);
        rect.anchoredPosition3D = Vector3.zero;
        CompactRect(_dialogCardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(440f, 170f));
        CompactRect(_titleRect, new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.96f), Vector2.zero);
        CompactRect(_feedbackRect, new Vector2(0.05f, 0.33f), new Vector2(0.95f, 0.68f), Vector2.zero);
        CompactRect(_confirmRect, new Vector2(0.05f, 0.05f), new Vector2(0.48f, 0.28f), Vector2.zero);
        CompactRect(_backRect, new Vector2(0.52f, 0.05f), new Vector2(0.95f, 0.28f), Vector2.zero);
    }

    static void CompactRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
    {
        if (rect == null) return;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition3D = Vector3.zero;
    }

    void RestorePanelLayout()
    {
        if (!_hasSavedPanelLayout || !panel) return;
        var rect = panel.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = _savedAnchorMin;
            rect.anchorMax = _savedAnchorMax;
            rect.pivot = _savedPivot;
            rect.sizeDelta = _savedSizeDelta;
            rect.anchoredPosition3D = _savedAnchoredPosition;
        }
        _dialogCardSnapshot.Restore(_dialogCardRect);
        _titleSnapshot.Restore(_titleRect);
        _feedbackSnapshot.Restore(_feedbackRect);
        _confirmSnapshot.Restore(_confirmRect);
        _backSnapshot.Restore(_backRect);
        if (_targetListViewport && _targetListViewportWasActive)
            _targetListViewport.SetActive(true);
        _dialogCardRect = _titleRect = _feedbackRect = _confirmRect = _backRect = null;
        _targetListViewport = null;
        _targetListViewportWasActive = false;
        _hasSavedPanelLayout = false;
    }

    void Show(ShopOffer offer)
    {
        _mutationSlotIndex = -1;
        _mutationCardIds.Clear();
        _acquisitionTargetMode = false;
        if (cardsContainer) cardsContainer.gameObject.SetActive(true);
        _selectedCardId = 0;
        if (titleLabel) titleLabel.text = $"CHOOSE CARD: {offer.Icon} {offer.DisplayName}";
        if (panel) { panel.SetActive(true); panel.transform.SetAsLastSibling(); }
        RefreshCards();
        var scroll = cardsContainer ? cardsContainer.GetComponentInParent<ScrollRect>() : null;
        if (scroll) scroll.verticalNormalizedPosition = 1f;
    }

    public void Hide()
    {
        var cards = CardManager.Instance;
        if (cards != null && cards.IsHandEnhancementTargeting) cards.EndHandEnhancementTargeting();
        bool restoreOrigin = _restoreOriginOnClose;
        _restoreOriginOnClose = false;
        _acquisitionTargetMode = false;
        _acquisitionEnhancement = null;
        RestorePanelLayout();
        if (cardsContainer) cardsContainer.gameObject.SetActive(true);
        _shopOfferId = null;
        _upgradeIndex = -1;
        _eventChoiceIndex = -1;
        _eventEnhancement = null;
        _consumableSlotIndex = -1;
        _mutationSlotIndex = -1;
        _mutationCardIds.Clear();
        _selectedCardId = 0;
        _replacementConfirmed = false;
        _replacementToConfirm = null;
        if (_targetedHandView) _targetedHandView.SetSelected(false);
        _targetedHandView = null;
        if (panel) panel.SetActive(false);
        if (confirmButton) confirmButton.interactable = false;
        var origin = _originPanel;
        _originPanel = null;
        if (restoreOrigin && origin)
        {
            origin.SetActive(true);
            origin.transform.SetAsLastSibling();
        }
    }

    bool IsInputBlockedForCurrentChoice() => GameplayInputGate.IsBlocked &&
        !(RunManager.Instance != null && RunManager.Instance.IsPendingHammerReward &&
          GameplayInputGate.Reasons == GameplayInputBlockReason.ArtifactChoice);

    void Back()
    {
        if (IsInputBlockedForCurrentChoice()) return;
        if (_acquisitionTargetMode)
        {
            _restoreOriginOnClose = true;
            Hide();
            return;
        }
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
        if (!panel || !panel.activeSelf || !cardsContainer || !cardButtonPrefab || _acquisitionTargetMode ||
            _eventChoiceIndex >= 0 && _eventEnhancement == null) return;
        ClearTargetCards();

        var cards = CardManager.Instance;
        if (cards != null)
        {
            var targetCards = _mutationSlotIndex >= 0 || _consumableSlotIndex >= 0
                ? cards.ownedCards : cards.hand;
            foreach (var card in targetCards)
            {
                if (card == null) continue;
                int id = card.Id;
                string reason = _mutationSlotIndex >= 0
                    ? cards.CanTargetDeckMutationAtSlot(_mutationSlotIndex, id) ? "" : "Not a valid target"
                    : _consumableSlotIndex >= 0 ? RunManager.Instance.GetEnhancementTargetUnavailableReason(id) :
                        RunManager.Instance.GetEnhancementAcquisitionTargetUnavailableReason(id);
                var obj = Instantiate(cardButtonPrefab, cardsContainer);
                obj.name = $"TargetCard_{id}";
                var label = obj.GetComponentInChildren<TMP_Text>();
                bool selected = _mutationSlotIndex >= 0 ? _mutationCardIds.Contains(id) : id == _selectedCardId;
                string current = _mutationSlotIndex < 0 && card.Enhancement != null && _consumableSlotIndex < 0
                    ? $"\nReplace: {card.Enhancement.displayName}" : "";
                if (label) label.text = (selected ? "SELECTED " : "") +
                    $"{card.SuitSymbol} {card.DisplayName}{current}\n#{id}" +
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
        else
        {
            _selectedCardId = id;
            _replacementConfirmed = false;
            _replacementToConfirm = null;
        }
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
        string reason = failure ?? (_selectedCardId == 0 ?
            (_acquisitionTargetMode ? "Click a card in your hand to target this Enhancement." : "Select an owned card, then confirm.") :
            _acquisitionTargetMode ? RunManager.Instance.GetEnhancementAcquisitionTargetUnavailableReason(_selectedCardId, _acquisitionEnhancement) :
                RunManager.Instance.GetEnhancementTargetUnavailableReason(_selectedCardId));
        var selectedCard = CardManager.Instance != null ? CardManager.Instance.FindOwnedCard(_selectedCardId) : null;
        if (feedbackLabel) feedbackLabel.text = !string.IsNullOrEmpty(reason) ? reason :
            selectedCard != null && selectedCard.Enhancement != null && _acquisitionTargetMode
                ? (_replacementConfirmed ? $"Replace {selectedCard.Enhancement.displayName}? Confirm again to replace it." :
                    $"Hand target: {selectedCard.DisplayName}. Confirm to review replacement.")
                : $"Hand target: {selectedCard?.DisplayName}. Confirm to apply.";
        if (confirmButton) confirmButton.interactable = _selectedCardId != 0 && string.IsNullOrEmpty(reason);
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
        var target = CardManager.Instance != null ? CardManager.Instance.FindOwnedCard(targetId) : null;
        bool isAcquisition = _acquisitionTargetMode;
        if (isAcquisition)
        {
            string targetReason = run.GetEnhancementAcquisitionTargetUnavailableReason(targetId, _acquisitionEnhancement);
            if (!string.IsNullOrEmpty(targetReason))
            {
                UpdateFeedback(targetReason);
                return;
            }
        }
        if (isAcquisition && target != null && target.Enhancement != null && !_replacementConfirmed)
        {
            _replacementConfirmed = true;
            _replacementToConfirm = target.Enhancement;
            UpdateFeedback();
            return;
        }
        if (_replacementConfirmed && target != null && target.Enhancement != _replacementToConfirm)
        {
            _replacementConfirmed = false;
            _replacementToConfirm = null;
            UpdateFeedback("The existing Enhancement changed. Review the replacement again.");
            return;
        }
        bool replaceExisting = isAcquisition && target != null && target.Enhancement != null;
        bool shopAcquisition = offerId != null;
        bool success = _consumableSlotIndex >= 0
            ? CardManager.Instance != null && CardManager.Instance.UseEnhancementConsumableAtSlot(_consumableSlotIndex, targetId)
            : _eventChoiceIndex >= 0
                ? run.ChooseEventEnhancementTarget(_eventChoiceIndex, targetId, _eventEnhancement, replaceExisting)
                : shopAcquisition
                    ? run.PurchaseShopEnhancement(offerId, targetId, replaceExisting)
                    : run.ChooseUpgradeOffer(index, targetId, replaceExisting);
        if (success)
        {
            _restoreOriginOnClose = shopAcquisition;
            Hide();
            return;
        }
        string reason = isAcquisition
            ? run.GetEnhancementAcquisitionTargetUnavailableReason(targetId, _acquisitionEnhancement)
            : run.GetEnhancementTargetUnavailableReason(targetId);
        if (string.IsNullOrEmpty(reason))
            reason = _consumableSlotIndex >= 0 ? "Consumable or target unavailable" :
                _eventChoiceIndex >= 0 ? "Event enhancement unavailable" :
                offerId != null ? run.GetShopOfferUnavailableReason(offerId) : "Upgrade unavailable";
        UpdateFeedback(string.IsNullOrEmpty(reason) ? "Enhancement unavailable" : reason);
    }
}
