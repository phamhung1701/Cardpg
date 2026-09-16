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
    public int gold;
    public HashSet<string> relics = new();
    public CardView selectedCard;

    readonly CardCollection _cards = new();
    readonly HashSet<CardView> _trackedViews = new();
    readonly List<CardData> _generatedDefinitions = new();
    int _nextCardId = 1;

    public IReadOnlyList<CardInstance> ownedCards => _cards.OwnedCards;
    public IReadOnlyList<CardInstance> deck => _cards.Deck;
    public IReadOnlyList<CardInstance> hand => _cards.Hand;
    public IReadOnlyList<CardInstance> discardPile => _cards.DiscardPile;
    public int HandCount => _cards.HandCount;

    public event Action<int> OnGoldChanged;
    public event Action OnDeckChanged;
    public event Action<CardView> OnCardSelected;

    public void Configure(Field field, Canvas canvas, CardView prefab, RelicData[] catalog)
    {
        handField = field;
        dragCanvas = canvas;
        cardPrefab = prefab;

        relicCatalog.Clear();
        if (catalog != null)
            relicCatalog.AddRange(catalog);
    }

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
        ClearCardState();

        var initialCards = new List<CardInstance>(40);
        foreach (CardData.Suit suit in Enum.GetValues(typeof(CardData.Suit)))
        {
            for (int rankValue = (int)CardData.Rank.Ace; rankValue <= (int)CardData.Rank.Ten; rankValue++)
            {
                var definition = CardData.Create(suit, (CardData.Rank)rankValue);
                _generatedDefinitions.Add(definition);
                initialCards.Add(new CardInstance(definition, _nextCardId++));
            }
        }

        _cards.Initialize(initialCards);
        _cards.ShuffleDeck();
        OnDeckChanged?.Invoke();
        OnCardSelected?.Invoke(null);
    }

    public CardInstance AddBossReward(CardData sourceCard)
    {
        if (sourceCard == null || !sourceCard.IsFaceCard)
            return null;

        foreach (var ownedCard in _cards.OwnedCards)
        {
            if (ownedCard.Suit == sourceCard.suit && ownedCard.Rank == sourceCard.rank)
                return null;
        }

        var definition = CardData.Create(sourceCard.suit, sourceCard.rank);
        var reward = new CardInstance(definition, _nextCardId);
        if (!_cards.AddOwnedToDeck(reward))
        {
            DestroyRuntimeObject(definition);
            return null;
        }

        _generatedDefinitions.Add(definition);
        _nextCardId++;
        _cards.ShuffleDeck();
        OnDeckChanged?.Invoke();
        return reward;
    }

    public void ShuffleDeck()
    {
        _cards.ShuffleDeck();
        OnDeckChanged?.Invoke();
    }

    public int DrawToHand(int count)
    {
        int previousHandCount = _cards.HandCount;
        int drawn = _cards.DrawToHand(count, HAND_SIZE);
        if (drawn <= 0) return 0;

        if (CanCreateViews())
        {
            for (int i = previousHandCount; i < _cards.HandCount; i++)
                CreateView(_cards.Hand[i]);
        }

        OnDeckChanged?.Invoke();
        return drawn;
    }

    public void DealHand() => DrawToHand(HAND_SIZE);
    public void RefillHand() => DrawToHand(HAND_SIZE);

    public int ReturnRandomDiscardToDeck(int count)
    {
        int moved = _cards.ReturnRandomDiscardToDeck(count);
        if (moved > 0)
            OnDeckChanged?.Invoke();
        return moved;
    }

    public bool TryDiscard(CardView card)
    {
        if (card == null || card.data == null || !_trackedViews.Contains(card))
            return false;
        if (!_cards.TryDiscard(card.data))
            return false;

        if (selectedCard == card)
            DeselectCard();
        else
            card.SetSelected(false);

        DetachAndDestroyView(card);
        OnDeckChanged?.Invoke();
        return true;
    }

    public void NotifyDeckChanged() => OnDeckChanged?.Invoke();

    public void SelectCard(CardView card)
    {
        if (card == null || card.data == null || !_trackedViews.Contains(card)) return;
        if (!_cards.ContainsInHand(card.data)) return;

        if (selectedCard != null && selectedCard != card)
            selectedCard.SetSelected(false);

        selectedCard = card;
        selectedCard.SetSelected(true);
        OnCardSelected?.Invoke(card);
    }

    public void DeselectCard()
    {
        if (selectedCard != null)
            selectedCard.SetSelected(false);
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
        ClearCardState();
        gold = 0;
        relics.Clear();

        OnGoldChanged?.Invoke(gold);
        OnDeckChanged?.Invoke();
        OnCardSelected?.Invoke(null);
    }

    protected override void OnDestroy()
    {
        ClearCardState();
        base.OnDestroy();
    }

    bool CanCreateViews() => handField != null && handField.cardsHolder != null && cardPrefab != null && dragCanvas != null;

    void CreateView(CardInstance cardInstance)
    {
        var view = Instantiate(cardPrefab);
        _trackedViews.Add(view);
        handField.AddCard(view, cardInstance);
    }

    void ClearCardState()
    {
        if (selectedCard != null)
            selectedCard.SetSelected(false);
        selectedCard = null;

        if (_trackedViews.Count > 0)
        {
            var views = new List<CardView>(_trackedViews);
            foreach (var view in views)
                DetachAndDestroyView(view);
        }
        _trackedViews.Clear();

        _cards.Clear();
        DestroyGeneratedDefinitions();
        _nextCardId = 1;
    }

    void DetachAndDestroyView(CardView view)
    {
        _trackedViews.Remove(view);
        if (view == null) return;

        view.homeField = null;
        view.transform.SetParent(null, false);
        view.gameObject.SetActive(false);
        DestroyRuntimeObject(view.gameObject);
    }

    void DestroyGeneratedDefinitions()
    {
        foreach (var definition in _generatedDefinitions)
        {
            if (definition != null)
                DestroyRuntimeObject(definition);
        }
        _generatedDefinitions.Clear();
    }

    static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(target);
        else
            UnityEngine.Object.DestroyImmediate(target);
    }
}
