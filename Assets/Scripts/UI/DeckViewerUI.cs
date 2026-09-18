using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeckViewerUI : MonoBehaviour
{
    [Header("References")]
    public GameObject panel;
    public TMP_Text deckText;
    public TMP_Text discardText;
    public Button toggleButton;
    public Button closeButton;

    bool _isOpen;

    void OnEnable()
    {
        if (toggleButton) toggleButton.onClick.AddListener(Toggle);
        if (closeButton) closeButton.onClick.AddListener(Close);
        if (CardManager.Instance != null)
            CardManager.Instance.OnDeckChanged += Refresh;
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += CloseForRunStart;
            RunManager.Instance.OnShowPathScreen += Close;
            RunManager.Instance.OnHidePathScreen += Close;
            RunManager.Instance.OnShowShop += Close;
            RunManager.Instance.OnShowEvent += CloseForEvent;
            RunManager.Instance.OnShowUpgrade += Close;
            RunManager.Instance.OnRunCompleted += Close;
        }
        if (CombatManager.Instance != null)
            CombatManager.Instance.OnEncounterResult += CloseForEncounterResult;
        Close();
    }

    void OnDisable()
    {
        if (toggleButton) toggleButton.onClick.RemoveListener(Toggle);
        if (closeButton) closeButton.onClick.RemoveListener(Close);
        if (CardManager.Instance != null)
            CardManager.Instance.OnDeckChanged -= Refresh;
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= CloseForRunStart;
            RunManager.Instance.OnShowPathScreen -= Close;
            RunManager.Instance.OnHidePathScreen -= Close;
            RunManager.Instance.OnShowShop -= Close;
            RunManager.Instance.OnShowEvent -= CloseForEvent;
            RunManager.Instance.OnShowUpgrade -= Close;
            RunManager.Instance.OnRunCompleted -= Close;
        }
        if (CombatManager.Instance != null)
            CombatManager.Instance.OnEncounterResult -= CloseForEncounterResult;
    }

    void Toggle()
    {
        _isOpen = !_isOpen;
        if (panel) panel.SetActive(_isOpen);
        if (_isOpen)
        {
            Refresh();
            panel.transform.SetAsLastSibling();
        }
    }

    void Close()
    {
        _isOpen = false;
        if (panel) panel.SetActive(false);
    }

    void Refresh()
    {
        var cards = CardManager.Instance;
        if (cards == null) return;

        if (deckText) deckText.text = FormatPile(cards.deck, "DRAW PILE");
        if (discardText) discardText.text = $"{FormatPile(cards.discardPile, "DISCARD PILE")}\n\n{FormatBuild(cards)}";
    }

    void CloseForRunStart(string _) => Close();
    void CloseForEvent(RunEventDefinition _) => Close();
    void CloseForEncounterResult(EncounterResult _) => Close();

    static string FormatBuild(CardManager cards)
    {
        var lines = new List<string> { $"<b>ARTIFACTS  •  {cards.ownedArtifacts.Count}</b>" };
        if (cards.ownedArtifacts.Count == 0)
        {
            lines.Add("None");
        }
        else
        {
            int shown = Mathf.Min(6, cards.ownedArtifacts.Count);
            for (int i = 0; i < shown; i++)
            {
                var artifact = cards.ownedArtifacts[i];
                if (artifact != null) lines.Add($"{artifact.icon} {artifact.displayName}");
            }
            if (cards.ownedArtifacts.Count > shown)
                lines.Add($"+{cards.ownedArtifacts.Count - shown} more");
        }

        var enhancedCards = new List<CardInstance>();
        foreach (var card in cards.ownedCards)
            if (card.Enhancement != null)
                enhancedCards.Add(card);

        lines.Add(string.Empty);
        lines.Add($"<b>ENHANCED CARDS  •  {enhancedCards.Count}</b>");
        if (enhancedCards.Count == 0)
        {
            lines.Add("None");
        }
        else
        {
            int shown = Mathf.Min(6, enhancedCards.Count);
            for (int i = 0; i < shown; i++)
                lines.Add(enhancedCards[i].DisplayName);
            if (enhancedCards.Count > shown)
                lines.Add($"+{enhancedCards.Count - shown} more");
        }

        return string.Join("\n", lines);
    }

    static string FormatPile(IReadOnlyList<CardInstance> pile, string label)
    {
        var counts = new Dictionary<string, int>();
        foreach (var card in pile)
        {
            string key = card.SuitSymbol;
            if (!counts.ContainsKey(key)) counts[key] = 0;
            counts[key]++;
        }

        var lines = new List<string> { $"<b>{label}  •  {pile.Count}</b>" };
        if (pile.Count == 0)
        {
            lines.Add("Empty");
        }
        else
        {
            foreach (var suit in new[] { "♣", "♦", "♥", "♠" })
                if (counts.TryGetValue(suit, out int count))
                    lines.Add($"{suit}  {count}");
        }
        return string.Join("\n", lines);
    }
}
