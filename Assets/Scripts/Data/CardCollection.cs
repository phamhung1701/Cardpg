using System;
using System.Collections.Generic;

/// <summary>
/// Plain runtime model for the cards owned by one run and their current zones.
/// All zone mutations are centralized here so a card cannot exist in multiple zones.
/// </summary>
[Serializable]
public sealed class CardCollection
{
    readonly List<CardInstance> _ownedCards = new();
    readonly List<CardInstance> _deck = new();
    readonly List<CardInstance> _hand = new();
    readonly List<CardInstance> _discardPile = new();
    IRandomSource _random = new DeterministicRandom(RunRandomContext.StableHash("CARDPG_DEFAULT_CARDS"));

    public IReadOnlyList<CardInstance> OwnedCards => _ownedCards;
    public IReadOnlyList<CardInstance> Deck => _deck;
    public IReadOnlyList<CardInstance> Hand => _hand;
    public IReadOnlyList<CardInstance> DiscardPile => _discardPile;
    public int HandCount => _hand.Count;

    public void ConfigureRandom(IRandomSource random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public void Initialize(IEnumerable<CardInstance> cards)
    {
        if (cards == null) throw new ArgumentNullException(nameof(cards));

        var validatedCards = new List<CardInstance>();
        var ids = new HashSet<int>();
        foreach (var card in cards)
        {
            if (card == null)
                throw new ArgumentException("Owned cards cannot contain null.", nameof(cards));
            if (!ids.Add(card.Id))
                throw new ArgumentException($"Duplicate card id {card.Id}.", nameof(cards));

            validatedCards.Add(card);
        }

        Clear();
        _ownedCards.AddRange(validatedCards);
        _deck.AddRange(validatedCards);
    }

    public bool AddOwnedToDeck(CardInstance card) => AddOwned(card, _deck);

    public bool AddOwnedToDiscard(CardInstance card) => AddOwned(card, _discardPile);

    bool AddOwned(CardInstance card, List<CardInstance> destination)
    {
        if (card == null) return false;

        foreach (var ownedCard in _ownedCards)
        {
            if (ReferenceEquals(ownedCard, card) || ownedCard.Id == card.Id)
                return false;
        }

        if (_deck.Contains(card) || _hand.Contains(card) || _discardPile.Contains(card))
            return false;

        _ownedCards.Add(card);
        destination.Add(card);
        return true;
    }

    public bool TryRemoveOwnedCards(IReadOnlyList<CardInstance> cards)
    {
        if (cards == null || cards.Count == 0 || cards.Count >= _ownedCards.Count) return false;
        var seen = new HashSet<CardInstance>();
        foreach (var card in cards)
            if (card == null || !seen.Add(card) || !_ownedCards.Contains(card)) return false;
        foreach (var card in cards)
        {
            _ownedCards.Remove(card);
            _deck.Remove(card);
            _hand.Remove(card);
            _discardPile.Remove(card);
        }
        return true;
    }

    public bool RemoveOwnedCard(CardInstance card)
    {
        if (card == null || !_ownedCards.Remove(card)) return false;
        _deck.Remove(card);
        _hand.Remove(card);
        _discardPile.Remove(card);
        return true;
    }

    public void Clear()
    {
        _ownedCards.Clear();
        _deck.Clear();
        _hand.Clear();
        _discardPile.Clear();
    }

    public void ShuffleDeck() => _deck.Shuffle(_random);

    public int DrawToHand(int requestedCount, int handCapacity)
    {
        if (requestedCount <= 0 || handCapacity <= _hand.Count) return 0;

        int targetCount = Math.Min(requestedCount, handCapacity - _hand.Count);
        int drawn = 0;
        while (drawn < targetCount)
        {
            if (_deck.Count == 0)
                RecycleDiscard();
            if (_deck.Count == 0)
                break;

            var card = _deck[^1];
            _deck.RemoveAt(_deck.Count - 1);
            _hand.Add(card);
            drawn++;
        }
        return drawn;
    }

    public bool ContainsInHand(CardInstance card) => card != null && _hand.Contains(card);

    public bool TryDiscard(CardInstance card)
    {
        if (card == null) return false;
        return TryDiscard(new[] { card });
    }

    public bool TryDiscard(IReadOnlyList<CardInstance> cards)
    {
        if (cards == null || cards.Count == 0) return false;

        var uniqueCards = new HashSet<CardInstance>();
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null || !uniqueCards.Add(card) || !_hand.Contains(card))
                return false;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            _hand.Remove(cards[i]);
            _discardPile.Add(cards[i]);
        }
        return true;
    }

    public bool TryReturnDiscardToHand(CardInstance card, int handCapacity)
    {
        if (card == null || _hand.Count >= handCapacity || !_discardPile.Remove(card)) return false;
        _hand.Add(card);
        return true;
    }

    public int ReturnRandomDiscardToDeck(int requestedCount)
    {
        if (requestedCount <= 0 || _discardPile.Count == 0) return 0;

        _discardPile.Shuffle(_random);
        int moved = Math.Min(requestedCount, _discardPile.Count);
        for (int i = 0; i < moved; i++)
        {
            int lastIndex = _discardPile.Count - 1;
            _deck.Add(_discardPile[lastIndex]);
            _discardPile.RemoveAt(lastIndex);
        }
        return moved;
    }

    void RecycleDiscard()
    {
        if (_discardPile.Count == 0) return;
        _deck.AddRange(_discardPile);
        _discardPile.Clear();
        ShuffleDeck();
    }
}
