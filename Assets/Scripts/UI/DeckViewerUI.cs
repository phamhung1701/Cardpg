using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DeckViewerUI : MonoBehaviour
{
    [Header("References")]
    public GameObject panel;
    public Transform contentContainer;
    public TMP_Text deckText;
    public TMP_Text discardText;
    public Button toggleButton;

    bool _isOpen;

    void OnEnable()
    {
        if (toggleButton) toggleButton.onClick.AddListener(Toggle);
        if (CardManager.Instance != null)
            CardManager.Instance.OnDeckChanged += Refresh;
    }

    void OnDisable()
    {
        if (toggleButton) toggleButton.onClick.RemoveListener(Toggle);
        if (CardManager.Instance != null)
            CardManager.Instance.OnDeckChanged -= Refresh;
    }

    void Toggle()
    {
        _isOpen = !_isOpen;
        if (panel) panel.SetActive(_isOpen);
        if (_isOpen) Refresh();
    }

    void Refresh()
    {
        var cm = CardManager.Instance;
        if (cm == null) return;

        if (deckText) deckText.text = FormatPile(cm.deck, "Deck");
        if (discardText) discardText.text = FormatPile(cm.discardPile, "Discard");
    }

    string FormatPile(List<CardData> pile, string label)
    {
        var counts = new Dictionary<string, int>();
        foreach (var card in pile)
        {
            string key = card.SuitSymbol;
            if (!counts.ContainsKey(key)) counts[key] = 0;
            counts[key]++;
        }

        var lines = new List<string> { $"<b>{label} ({pile.Count})</b>" };
        foreach (var kvp in counts)
            lines.Add($"{kvp.Key} x{kvp.Value}");
        return string.Join("\n", lines);
    }
}
