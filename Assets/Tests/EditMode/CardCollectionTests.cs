using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class CardCollectionTests
{
    readonly List<CardData> _definitions = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var definition in _definitions)
        {
            if (definition != null)
                UnityEngine.Object.DestroyImmediate(definition);
        }

        _definitions.Clear();
    }

    [Test]
    public void CardInstance_SameDefinitionAndDifferentIds_AreIndependentInstances()
    {
        var definition = CreateDefinition(CardData.Suit.Spades, CardData.Rank.Queen);
        var first = new CardInstance(definition, 10);
        var second = new CardInstance(definition, 11);

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.Definition, Is.SameAs(definition));
        Assert.That(second.Definition, Is.SameAs(definition));
        Assert.That(first.Id, Is.EqualTo(10));
        Assert.That(second.Id, Is.EqualTo(11));
    }

    [Test]
    public void Initialize_Standard52Cards_PreservesEveryExactInstanceAndUniqueId()
    {
        var cards = CreateStandardDeck();
        var collection = new CardCollection();

        collection.Initialize(cards);

        Assert.That(collection.OwnedCards, Has.Count.EqualTo(52));
        Assert.That(collection.Deck, Has.Count.EqualTo(52));
        Assert.That(collection.Hand, Is.Empty);
        Assert.That(collection.DiscardPile, Is.Empty);
        Assert.That(collection.OwnedCards.Select(card => card.Id).Distinct().Count(), Is.EqualTo(52));
        CollectionAssert.AreEqual(cards, collection.OwnedCards);
        CollectionAssert.AreEqual(cards, collection.Deck);
        AssertConserved(collection);
    }

    [Test]
    public void Initialize_DuplicateIds_Throws()
    {
        var first = CreateCard(7, CardData.Suit.Clubs, CardData.Rank.Ace);
        var second = CreateCard(7, CardData.Suit.Hearts, CardData.Rank.King);
        var collection = new CardCollection();

        Assert.Throws<ArgumentException>(() => collection.Initialize(new[] { first, second }));
    }

    [Test]
    public void DrawToHand_RequestBeyondCapacity_CapsHandAtEight()
    {
        var collection = CreateCollection(12);

        int firstDraw = collection.DrawToHand(12, 8);
        int secondDraw = collection.DrawToHand(1, 8);

        Assert.That(firstDraw, Is.EqualTo(8));
        Assert.That(secondDraw, Is.Zero);
        Assert.That(collection.HandCount, Is.EqualTo(8));
        Assert.That(collection.Deck, Has.Count.EqualTo(4));
        AssertConserved(collection);
    }

    [Test]
    public void DrawToHand_NegativeRequest_IsNoOp()
    {
        var collection = CreateCollection(5);
        var deckBefore = collection.Deck.ToArray();

        int drawn = collection.DrawToHand(-3, 8);

        Assert.That(drawn, Is.Zero);
        Assert.That(collection.Hand, Is.Empty);
        CollectionAssert.AreEqual(deckBefore, collection.Deck);
        AssertConserved(collection);
    }

    [Test]
    public void TryDiscard_ValidCardSucceedsOnlyOnce_AndForeignCardIsRejected()
    {
        var collection = CreateCollection(3);
        collection.DrawToHand(1, 8);
        var heldCard = collection.Hand[0];
        var foreignCard = CreateCard(99, CardData.Suit.Diamonds, CardData.Rank.Ten);

        Assert.That(collection.TryDiscard(foreignCard), Is.False);
        Assert.That(collection.TryDiscard(heldCard), Is.True);
        Assert.That(collection.TryDiscard(heldCard), Is.False);

        Assert.That(collection.ContainsInHand(heldCard), Is.False);
        Assert.That(collection.DiscardPile, Has.Count.EqualTo(1));
        Assert.That(collection.DiscardPile[0], Is.SameAs(heldCard));
        AssertConserved(collection);
    }

    [Test]
    public void DrawToHand_WhenDeckIsEmpty_RecyclesDiscardWithoutReplacingInstances()
    {
        var collection = CreateCollection(4);
        collection.DrawToHand(4, 8);
        var originalCards = collection.Hand.ToArray();

        foreach (var card in originalCards)
            Assert.That(collection.TryDiscard(card), Is.True);

        int drawn = collection.DrawToHand(3, 8);

        Assert.That(drawn, Is.EqualTo(3));
        Assert.That(collection.Hand, Has.Count.EqualTo(3));
        Assert.That(collection.Deck, Has.Count.EqualTo(1));
        Assert.That(collection.DiscardPile, Is.Empty);
        CollectionAssert.IsSubsetOf(collection.Hand, originalCards);
        CollectionAssert.IsSubsetOf(collection.Deck, originalCards);
        AssertConserved(collection);
    }

    [Test]
    public void HeartsReturn_MovesRequestedDiscardCardsBackToDeckAsExactInstances()
    {
        var collection = CreateCollection(5, CardData.Suit.Hearts);
        collection.DrawToHand(5, 8);
        var discardedCards = collection.Hand.Take(3).ToArray();

        foreach (var card in discardedCards)
            Assert.That(collection.TryDiscard(card), Is.True);

        int moved = collection.ReturnRandomDiscardToDeck(2);

        Assert.That(moved, Is.EqualTo(2));
        Assert.That(collection.Deck, Has.Count.EqualTo(2));
        Assert.That(collection.DiscardPile, Has.Count.EqualTo(1));
        CollectionAssert.IsSubsetOf(collection.Deck, discardedCards);
        CollectionAssert.IsSubsetOf(collection.DiscardPile, discardedCards);
        CollectionAssert.AreEquivalent(discardedCards, collection.Deck.Concat(collection.DiscardPile));
        AssertConserved(collection);
    }

    [Test]
    public void ReturnRandomDiscardToDeck_NegativeRequest_IsNoOp()
    {
        var collection = CreateCollection(2, CardData.Suit.Hearts);
        collection.DrawToHand(2, 8);
        var card = collection.Hand[0];
        collection.TryDiscard(card);

        int moved = collection.ReturnRandomDiscardToDeck(-1);

        Assert.That(moved, Is.Zero);
        Assert.That(collection.DiscardPile.Single(), Is.SameAs(card));
        Assert.That(collection.Deck, Is.Empty);
        AssertConserved(collection);
    }

    [Test]
    public void ShuffleDeck_PreservesEveryExactInstance()
    {
        var collection = CreateCollection(20);
        var deckBefore = collection.Deck.ToArray();

        collection.ShuffleDeck();

        CollectionAssert.AreEquivalent(deckBefore, collection.Deck);
        AssertConserved(collection);
    }

    [Test]
    public void Clear_RemovesOwnedCardsAndAllZoneState()
    {
        var collection = CreateCollection(6);
        collection.DrawToHand(4, 8);
        collection.TryDiscard(collection.Hand[0]);

        collection.Clear();

        Assert.That(collection.OwnedCards, Is.Empty);
        Assert.That(collection.Deck, Is.Empty);
        Assert.That(collection.Hand, Is.Empty);
        Assert.That(collection.DiscardPile, Is.Empty);
        Assert.That(collection.HandCount, Is.Zero);
    }

    CardCollection CreateCollection(int count, CardData.Suit suit = CardData.Suit.Clubs)
    {
        var cards = Enumerable.Range(1, count)
            .Select(id => CreateCard(id, suit, (CardData.Rank)((id - 1) % 13)))
            .ToArray();
        var collection = new CardCollection();
        collection.Initialize(cards);
        return collection;
    }

    List<CardInstance> CreateStandardDeck()
    {
        var cards = new List<CardInstance>(52);
        int id = 1;
        foreach (CardData.Suit suit in Enum.GetValues(typeof(CardData.Suit)))
        {
            foreach (CardData.Rank rank in Enum.GetValues(typeof(CardData.Rank)))
                cards.Add(CreateCard(id++, suit, rank));
        }

        return cards;
    }

    CardInstance CreateCard(int id, CardData.Suit suit, CardData.Rank rank)
    {
        return new CardInstance(CreateDefinition(suit, rank), id);
    }

    CardData CreateDefinition(CardData.Suit suit, CardData.Rank rank)
    {
        var definition = ScriptableObject.CreateInstance<CardData>();
        definition.suit = suit;
        definition.rank = rank;
        _definitions.Add(definition);
        return definition;
    }

    static void AssertConserved(CardCollection collection)
    {
        var zoneCards = collection.Deck
            .Concat(collection.Hand)
            .Concat(collection.DiscardPile)
            .ToArray();

        Assert.That(zoneCards, Has.Length.EqualTo(collection.OwnedCards.Count));
        Assert.That(zoneCards.Distinct().Count(), Is.EqualTo(zoneCards.Length),
            "A card instance appeared in more than one zone.");
        CollectionAssert.AreEquivalent(collection.OwnedCards, zoneCards,
            "Owned cards must equal the exact instances across deck, hand, and discard.");
    }
}
