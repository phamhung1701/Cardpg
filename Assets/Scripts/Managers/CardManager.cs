using System;
using System.Collections.Generic;
using UnityEngine;

public class CardManager : Singleton<CardManager>
{
    public const int HAND_SIZE = 8;

    [Header("References")]
    public Field handField;
    public CardView cardPrefab;
    public Canvas dragCanvas;

    [Header("Relic Catalog")]
    public List<RelicData> relicCatalog = new();

    [Header("Runtime State")]
    public List<CardData> deck = new();
    public List<CardData> discardPile = new();
    public int gold;
    public HashSet<string> relics = new();
    public CardView selectedCard;

    public event Action<int> OnGoldChanged;
    public event Action OnDeckChanged;
    public event Action<CardView> OnCardSelected;

    public bool HasRelic(string id) => relics.Contains(id);

    public bool BuyRelic(RelicData relic)
    {
        if (relic == null || HasRelic(relic.id)) return false;
        if (!SpendGold(relic.price)) return false;
        relics.Add(relic.id);
        return true;
    }

    public void BuildDeck()
    {
        deck.Clear();
        discardPile.Clear();
        foreach (CardData.Suit suit in Enum.GetValues(typeof(CardData.Suit)))
        {
            foreach (CardData.Rank rank in Enum.GetValues(typeof(CardData.Rank)))
            {
                deck.Add(CardData.Create(suit, rank));
            }
        }
        ShuffleDeck();
        OnDeckChanged?.Invoke();
    }

    public void ShuffleDeck() => deck.Shuffle();

    public List<CardData> DrawCards(int count)
    {
        var drawn = new List<CardData>();
        for (int i = 0; i < count; i++)
        {
            if (deck.Count == 0) RecycleDiscard();
            if (deck.Count == 0) break;
            var c = deck[^1];
            deck.RemoveAt(deck.Count - 1);
            drawn.Add(c);
        }
        return drawn;
    }

    public int DrawToHand(int count)
    {
        if (handField == null)
        {
            Debug.LogError("CardManager: handField not set");
            return 0;
        }
        int maxDraw = Mathf.Max(0, HAND_SIZE - handField.CardCount);
        var drawn = DrawCards(Mathf.Min(count, maxDraw));
        foreach (var data in drawn)
        {
            var card = Instantiate(cardPrefab);
            handField.AddCard(card, data);
        }
        return drawn.Count;
    }

    public void DealHand() => DrawToHand(HAND_SIZE);
    public void RefillHand() => DrawToHand(HAND_SIZE);

    public void AddToDiscard(CardData data) => discardPile.Add(data);

    public void NotifyDeckChanged() => OnDeckChanged?.Invoke();

    void RecycleDiscard()
    {
        if (discardPile.Count == 0) return;
        deck.AddRange(discardPile);
        discardPile.Clear();
        ShuffleDeck();
    }

    public void SelectCard(CardView card)
    {
        if (selectedCard != null && selectedCard != card)
            selectedCard.ForceToIdle();
        selectedCard = card;
        OnCardSelected?.Invoke(card);
    }

    public void DeselectCard()
    {
        selectedCard = null;
        OnCardSelected?.Invoke(null);
    }

    public void AddGold(int amount)
    {
        gold += amount;
        OnGoldChanged?.Invoke(gold);
    }

    public bool SpendGold(int amount)
    {
        if (gold < amount) return false;
        gold -= amount;
        OnGoldChanged?.Invoke(gold);
        return true;
    }

    public void Reset()
    {
        deck.Clear();
        discardPile.Clear();
        gold = 0;
        relics.Clear();
        selectedCard = null;
    }
}
