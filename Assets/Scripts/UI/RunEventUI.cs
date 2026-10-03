using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RunEventUI : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text titleLabel;
    public TMP_Text descriptionLabel;
    public Transform choicesContainer;
    public GameObject choiceButtonPrefab;
    public EnhancementTargetUI enhancementTargetUI;

    readonly HashSet<int> _selectedDiscardCardIds = new();
    int _discardChoiceIndex = -1;
    Action<int> _artifactDiscardChoiceResolved;
    RunEventChoiceInteraction _discardInteraction = RunEventChoiceInteraction.Immediate;


    void OnEnable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowEvent += Show;
            RunManager.Instance.OnHideEvent += Hide;
            RunManager.Instance.OnShowArtifactDiscardChoice += ShowArtifactDiscardChoice;
            RunManager.Instance.OnShowUpgrade += ShowUpgrade;
            RunManager.Instance.OnHideUpgrade += Hide;
        }
        Hide();
    }

    void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowEvent -= Show;
            RunManager.Instance.OnHideEvent -= Hide;
            RunManager.Instance.OnShowArtifactDiscardChoice -= ShowArtifactDiscardChoice;
            RunManager.Instance.OnShowUpgrade -= ShowUpgrade;
            RunManager.Instance.OnHideUpgrade -= Hide;
        }
    }

    void Show(RunEventDefinition definition)
    {
        if (definition == null || panel == null || choicesContainer == null || choiceButtonPrefab == null)
            return;

        if (titleLabel) titleLabel.text = definition.title;
        if (descriptionLabel) descriptionLabel.text = definition.description;

        ClearChoices();

        for (int i = 0; i < definition.choices.Length; i++)
        {
            int choiceIndex = i;
            var choice = definition.choices[i];
            var buttonObject = Instantiate(choiceButtonPrefab, choicesContainer);
            buttonObject.name = $"EventChoice_{choiceIndex + 1}";
            string unavailableReason = RunManager.Instance.GetEventOptionUnavailableReason(choiceIndex);
            if (string.IsNullOrEmpty(unavailableReason) && !HasRequiredInteractionUI(choice.interaction))
                unavailableReason = choice.interaction == RunEventChoiceInteraction.ChooseEnhancementTarget
                    ? "Enhancement selection UI unavailable"
                    : "Card selection UI unavailable";
            bool available = string.IsNullOrEmpty(unavailableReason);
            var label = buttonObject.GetComponentInChildren<TMP_Text>();
            if (label)
            {
                string status = available ? string.Empty : $"\n<color=#DFA0A0>{unavailableReason}</color>";
                label.text = $"<b>{choice.label}</b>\n{choice.description}{status}";
            }

            var button = buttonObject.GetComponent<Button>();
            if (button)
            {
                button.interactable = available;
                button.onClick.AddListener(() => HandleChoice(choiceIndex, choice.interaction));
            }
        }

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
    }

    bool HasRequiredInteractionUI(RunEventChoiceInteraction interaction)
    {
        if (interaction == RunEventChoiceInteraction.ChooseEnhancementTarget)
            return enhancementTargetUI != null && enhancementTargetUI.CanOpenEventEnhancementPicker;
        if (interaction == RunEventChoiceInteraction.DiscardTwoForRandomEnhancement ||
            interaction == RunEventChoiceInteraction.DiscardCardsForTotalValue)
        {
            var cards = CardManager.Instance;
            return choicesContainer != null && choiceButtonPrefab != null && cards != null &&
                cards.handField != null && cards.handField.cardsHolder != null;
        }
        return true;
    }

    void HandleChoice(int choiceIndex, RunEventChoiceInteraction interaction)
    {
        switch (interaction)
        {
            case RunEventChoiceInteraction.HammerRetry:
                RunManager.Instance.RetryHammerReward();
                break;
            case RunEventChoiceInteraction.HammerDecline:
                RunManager.Instance.DeclineHammerReward();
                break;
            case RunEventChoiceInteraction.ChooseEnhancementTarget:
                enhancementTargetUI?.OpenEventEnhancement(choiceIndex);
                break;
            case RunEventChoiceInteraction.DiscardTwoForRandomEnhancement:
            case RunEventChoiceInteraction.DiscardCardsForTotalValue:
                ShowDiscardSelection(choiceIndex);
                break;
            default:
                RunManager.Instance.ChooseEventOption(choiceIndex);
                break;
        }
    }

    void ShowArtifactDiscardChoice(string title, string description,
        IReadOnlyList<CardInstance> choices, Action<int> onResolved)
    {
        if (panel == null || choicesContainer == null || choiceButtonPrefab == null || choices == null || onResolved == null)
        {
            onResolved?.Invoke(0);
            return;
        }
        _artifactDiscardChoiceResolved = onResolved;
        ClearChoices();
        if (titleLabel) titleLabel.text = title;
        if (descriptionLabel) descriptionLabel.text = description;
        foreach (var card in choices)
        {
            if (card == null) continue;
            int id = card.Id;
            var buttonObject = Instantiate(choiceButtonPrefab, choicesContainer);
            buttonObject.name = $"ArtifactDiscard_{id}";
            var label = buttonObject.GetComponentInChildren<TMP_Text>();
            if (label) label.text = $"{card.SuitSymbol} {card.DisplayName}  Value {card.BaseAttackValue}  #{id}";
            var button = buttonObject.GetComponent<Button>();
            if (button) button.onClick.AddListener(() => ResolveArtifactDiscardChoice(id));
        }
        var cancelObject = Instantiate(choiceButtonPrefab, choicesContainer);
        cancelObject.name = "ArtifactDiscardCancel";
        var cancelLabel = cancelObject.GetComponentInChildren<TMP_Text>();
        if (cancelLabel) cancelLabel.text = "Cancel (skip this draw)";
        var cancel = cancelObject.GetComponent<Button>();
        if (cancel) cancel.onClick.AddListener(() => ResolveArtifactDiscardChoice(0));
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
    }

    void ResolveArtifactDiscardChoice(int cardId)
    {
        var callback = _artifactDiscardChoiceResolved;
        _artifactDiscardChoiceResolved = null;
        ClearChoices();
        if (panel) panel.SetActive(false);
        callback?.Invoke(cardId);
    }

    void ShowDiscardSelection(int choiceIndex)
    {
        var run = RunManager.Instance;
        var cards = CardManager.Instance;
        if (run == null || cards == null ||
            !string.IsNullOrEmpty(run.GetEventOptionUnavailableReason(choiceIndex))) return;
        _discardChoiceIndex = choiceIndex;
        var choice = run.ActiveEvent.choices[choiceIndex];
        _discardInteraction = choice.interaction;
        _selectedDiscardCardIds.Clear();
        if (titleLabel) titleLabel.text = run.ActiveEvent.title;
        if (descriptionLabel)
        {
            string prompt = _discardInteraction == RunEventChoiceInteraction.DiscardCardsForTotalValue
                ? $"Select cards with a total value of exactly {choice.requiredDiscardValue}."
                : "Choose exactly two cards to discard.";
            descriptionLabel.text = $"{choice.description} {prompt}";
        }
        ClearChoices();

        var views = cards.handField != null && cards.handField.cardsHolder != null
            ? cards.handField.cardsHolder.GetComponentsInChildren<CardView>()
            : System.Array.Empty<CardView>();
        foreach (var view in views.Where(view => view != null && view.data != null && cards.hand.Contains(view.data)))
        {
            int id = view.data.Id;
            var buttonObject = Instantiate(choiceButtonPrefab, choicesContainer);
            buttonObject.name = $"DiscardCard_{id}";
            var label = buttonObject.GetComponentInChildren<TMP_Text>();
            if (label) label.text = $"{view.data.SuitSymbol} {view.data.DisplayName}  Value {view.data.BaseAttackValue}  #{id}";
            var button = buttonObject.GetComponent<Button>();
            if (button) button.onClick.AddListener(() => ToggleDiscardCard(id));
        }

        var confirmObject = Instantiate(choiceButtonPrefab, choicesContainer);
        confirmObject.name = "ConfirmEventDiscard";
        var confirmLabel = confirmObject.GetComponentInChildren<TMP_Text>();
        if (confirmLabel)
            confirmLabel.text = _discardInteraction == RunEventChoiceInteraction.DiscardCardsForTotalValue
                ? $"Confirm Discard (0/{choice.requiredDiscardValue})" : "Confirm Discard (0/2)";
        var confirm = confirmObject.GetComponent<Button>();
        if (confirm) { confirm.interactable = false; confirm.onClick.AddListener(ConfirmDiscard); }

        var backObject = Instantiate(choiceButtonPrefab, choicesContainer);
        backObject.name = "BackToEventChoices";
        var backLabel = backObject.GetComponentInChildren<TMP_Text>();
        if (backLabel) backLabel.text = "Back";
        var back = backObject.GetComponent<Button>();
        if (back) back.onClick.AddListener(() => Show(RunManager.Instance != null ? RunManager.Instance.ActiveEvent : null));
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
    }

    void ToggleDiscardCard(int id)
    {
        if (_selectedDiscardCardIds.Remove(id)) { RefreshDiscardSelection(); return; }
        if (_discardInteraction == RunEventChoiceInteraction.DiscardTwoForRandomEnhancement &&
            _selectedDiscardCardIds.Count >= 2) return;
        if (_discardInteraction == RunEventChoiceInteraction.DiscardCardsForTotalValue)
        {
            var cards = CardManager.Instance;
            var run = RunManager.Instance;
            int selectedValue = 0;
            if (cards != null)
                foreach (int selectedId in _selectedDiscardCardIds)
                    selectedValue += cards.FindOwnedCard(selectedId)?.BaseAttackValue ?? 0;
            int cardValue = cards != null ? cards.FindOwnedCard(id)?.BaseAttackValue ?? 0 : 0;
            int requiredValue = run?.ActiveEvent?.choices?[_discardChoiceIndex]?.requiredDiscardValue ?? 0;
            if (requiredValue <= 0 || selectedValue + cardValue > requiredValue) return;
        }
        _selectedDiscardCardIds.Add(id);
        RefreshDiscardSelection();
    }

    void RefreshDiscardSelection()
    {
        foreach (Transform child in choicesContainer)
        {
            if (!child.name.StartsWith("DiscardCard_")) continue;
            if (!int.TryParse(child.name.Substring("DiscardCard_".Length), out int id)) continue;
            var label = child.GetComponentInChildren<TMP_Text>();
            if (label)
            {
                string prefix = label.text.StartsWith("SELECTED  ") ? label.text.Substring("SELECTED  ".Length) : label.text;
                label.text = _selectedDiscardCardIds.Contains(id) ? $"SELECTED  {prefix}" : prefix;
            }
        }
        var run = RunManager.Instance;
        var interaction = _discardInteraction;
        int selectedValue = 0;
        int requiredValue = run?.ActiveEvent?.choices?[_discardChoiceIndex]?.requiredDiscardValue ?? 0;
        var cards = CardManager.Instance;
        if (cards != null)
            foreach (int id in _selectedDiscardCardIds)
                selectedValue += cards.FindOwnedCard(id)?.BaseAttackValue ?? 0;

        var confirmObject = choicesContainer.Find("ConfirmEventDiscard");
        if (confirmObject == null) return;
        var labelText = confirmObject.GetComponentInChildren<TMP_Text>();
        if (labelText) labelText.text = interaction == RunEventChoiceInteraction.DiscardCardsForTotalValue
            ? $"Confirm Discard ({selectedValue}/{requiredValue})"
            : $"Confirm Discard ({_selectedDiscardCardIds.Count}/2)";
        bool canConfirm = interaction == RunEventChoiceInteraction.DiscardCardsForTotalValue
            ? run != null && run.CanChooseEventDiscardForTotalValue(_discardChoiceIndex, _selectedDiscardCardIds.ToArray())
            : _selectedDiscardCardIds.Count == 2;
        var button = confirmObject.GetComponent<Button>();
        if (button) button.interactable = canConfirm;
    }

    void ConfirmDiscard()
    {
        if (_discardChoiceIndex < 0) return;
        var run = RunManager.Instance;
        bool success = run != null && (_discardInteraction == RunEventChoiceInteraction.DiscardCardsForTotalValue
            ? run.ChooseEventDiscardForTotalValue(_discardChoiceIndex, _selectedDiscardCardIds.ToArray())
            : _selectedDiscardCardIds.Count == 2 &&
                run.ChooseEventDiscardAndReward(_discardChoiceIndex, _selectedDiscardCardIds.ToArray()));
        if (success)
        {
            _discardChoiceIndex = -1;
            _selectedDiscardCardIds.Clear();
        }
    }

    void ShowUpgrade()
    {
        if (panel == null || choicesContainer == null || choiceButtonPrefab == null || RunManager.Instance == null)
            return;

        if (titleLabel) titleLabel.text = "CARD ENHANCEMENT";
        if (descriptionLabel) descriptionLabel.text = "Choose one permanent enhancement for this run.";
        ClearChoices();

        var offers = RunManager.Instance.GetCurrentUpgradeOffers();
        for (int i = 0; i < offers.Count; i++)
        {
            int choiceIndex = i;
            var offer = offers[i];
            var buttonObject = Instantiate(choiceButtonPrefab, choicesContainer);
            buttonObject.name = $"EnhancementChoice_{choiceIndex + 1}";
            var label = buttonObject.GetComponentInChildren<TMP_Text>();
            if (label)
                label.text = $"<b>{offer.Icon} {offer.DisplayName}</b>\n{offer.Description(CardManager.Instance)}";

            var button = buttonObject.GetComponent<Button>();
            if (button)
                button.onClick.AddListener(() => { if (enhancementTargetUI) enhancementTargetUI.OpenUpgrade(choiceIndex); });
        }

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
    }

    void ClearChoices()
    {
        if (choicesContainer == null) return;
        var children = new List<GameObject>();
        foreach (Transform child in choicesContainer)
            if (child != null) children.Add(child.gameObject);
        foreach (var child in children)
        {
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    void Hide()
    {
        _artifactDiscardChoiceResolved = null;
        _discardChoiceIndex = -1;
        _discardInteraction = RunEventChoiceInteraction.Immediate;
        _selectedDiscardCardIds.Clear();
        if (panel) panel.SetActive(false);
    }
}
