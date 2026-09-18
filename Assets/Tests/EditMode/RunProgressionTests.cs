using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class RunProgressionTests
{
    readonly List<ScriptableObject> _testOwnedAssets = new();

    GameObject _cardManagerObject;
    GameObject _combatManagerObject;
    GameObject _runManagerObject;
    CardManager _cardManager;
    CombatManager _combatManager;
    RunManager _runManager;

    [SetUp]
    public void SetUp()
    {
        _cardManagerObject = new GameObject("Test CardManager");
        _cardManager = _cardManagerObject.AddComponent<CardManager>();

        _combatManagerObject = new GameObject("Test CombatManager");
        _combatManager = _combatManagerObject.AddComponent<CombatManager>();

        _runManagerObject = new GameObject("Test RunManager");
        _runManager = _runManagerObject.AddComponent<RunManager>();
        _runManager.thiefType = CreateEnemyType("Test Thief", 10, 2, 1);
        _runManager.goblinType = CreateEnemyType("Test Goblin", 15, 3, 2);
        _runManager.knightType = CreateEnemyType("Test Knight", 20, 4, 3);

        Assert.That(CardManager.Instance, Is.SameAs(_cardManager));
        Assert.That(CombatManager.Instance, Is.SameAs(_combatManager));
        Assert.That(RunManager.Instance, Is.SameAs(_runManager));
    }

    [TearDown]
    public void TearDown()
    {
        // RunManager owns and destroys the generated boss types and their source cards.
        DestroyGameObject(_runManagerObject);
        DestroyGameObject(_combatManagerObject);
        DestroyGameObject(_cardManagerObject);

        foreach (var asset in _testOwnedAssets)
        {
            if (asset != null)
                UnityEngine.Object.DestroyImmediate(asset);
        }

        _testOwnedAssets.Clear();
        _runManager = null;
        _combatManager = null;
        _cardManager = null;
    }

    [Test]
    public void BuildDeck_CreatesAceThroughTenInEverySuit_AndConservesOwnedCardsAcrossZones()
    {
        _cardManager.BuildDeck();

        Assert.That(_cardManager.ownedCards, Has.Count.EqualTo(40));
        Assert.That(_cardManager.deck, Has.Count.EqualTo(40));
        Assert.That(_cardManager.hand, Is.Empty);
        Assert.That(_cardManager.discardPile, Is.Empty);
        Assert.That(_cardManager.ownedCards.Any(card => card.Definition.IsFaceCard), Is.False);

        foreach (CardData.Suit suit in Enum.GetValues(typeof(CardData.Suit)))
        {
            var suitCards = _cardManager.ownedCards.Where(card => card.Suit == suit).ToArray();
            Assert.That(suitCards, Has.Length.EqualTo(10));
            CollectionAssert.AreEquivalent(
                Enumerable.Range((int)CardData.Rank.Ace, 10).Select(value => (CardData.Rank)value),
                suitCards.Select(card => card.Rank));
        }

        Assert.That(_cardManager.DrawToHand(CardManager.HAND_SIZE), Is.EqualTo(CardManager.HAND_SIZE));
        Assert.That(_cardManager.hand, Has.Count.EqualTo(CardManager.HAND_SIZE));
        Assert.That(_cardManager.deck, Has.Count.EqualTo(40 - CardManager.HAND_SIZE));
        AssertCardStateIsConserved();
    }

    [Test]
    public void AddBossReward_AddsExactFaceCardOnce_WithUniqueId_AndRejectsInvalidRewards()
    {
        _cardManager.BuildDeck();
        var sourceCard = CreateCard(CardData.Suit.Diamonds, CardData.Rank.Queen);
        var nonFaceCard = CreateCard(CardData.Suit.Spades, CardData.Rank.Ten);
        var idsBefore = _cardManager.ownedCards.Select(card => card.Id).ToHashSet();

        var reward = _cardManager.AddBossReward(sourceCard);

        Assert.That(reward, Is.Not.Null);
        Assert.That(reward.Suit, Is.EqualTo(CardData.Suit.Diamonds));
        Assert.That(reward.Rank, Is.EqualTo(CardData.Rank.Queen));
        Assert.That(reward.Definition, Is.Not.SameAs(sourceCard));
        Assert.That(idsBefore.Contains(reward.Id), Is.False);
        Assert.That(_cardManager.ownedCards, Has.Count.EqualTo(41));
        Assert.That(_cardManager.deck, Has.Count.EqualTo(41));
        Assert.That(_cardManager.ownedCards.Count(card =>
            card.Suit == sourceCard.suit && card.Rank == sourceCard.rank), Is.EqualTo(1));
        Assert.That(_cardManager.ownedCards.Select(card => card.Id).Distinct().Count(), Is.EqualTo(41));
        AssertCardStateIsConserved();

        Assert.That(_cardManager.AddBossReward(sourceCard), Is.Null);
        Assert.That(_cardManager.AddBossReward(nonFaceCard), Is.Null);
        Assert.That(_cardManager.AddBossReward(null), Is.Null);
        Assert.That(_cardManager.ownedCards, Has.Count.EqualTo(41));
        Assert.That(_cardManager.deck, Has.Count.EqualTo(41));
        AssertCardStateIsConserved();
    }

    [Test]
    public void StartRun_BuildsTwelveBosses_InRankGroupsWithFourUniqueSuitsAndSourceCards()
    {
        _runManager.StartRun();

        Assert.That(_runManager.bossDeck, Has.Count.EqualTo(12));
        AssertBossGroup(0, CardData.Rank.Jack);
        AssertBossGroup(4, CardData.Rank.Queen);
        AssertBossGroup(8, CardData.Rank.King);
        Assert.That(_runManager.bossDeck.Select(boss => boss.sourceCard).Distinct().Count(), Is.EqualTo(12));
    }

    [Test]
    public void BossVictory_RecordsCompletedMapSplitAndStartsFreshNextMapTimer()
    {
        _runManager.StartRunWithSeed("TIMING-SPLIT");
        _runManager.Timing.Advance(42.5d);

        typeof(RunManager).GetMethod("HandleBossVictory", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(_runManager, null);

        Assert.That(_runManager.bossIndex, Is.EqualTo(1));
        Assert.That(_runManager.Timing.CompletedSplits, Has.Count.EqualTo(1));
        Assert.That(_runManager.Timing.CompletedSplits[0].MapNumber, Is.EqualTo(1));
        Assert.That(_runManager.Timing.CompletedSplits[0].ElapsedSeconds, Is.EqualTo(42.5d));
        Assert.That(_runManager.Timing.CurrentMapIndex, Is.EqualTo(1));
        Assert.That(_runManager.Timing.CurrentMapElapsedSeconds, Is.Zero);
        Assert.That(_runManager.Timing.IsActive, Is.True);
    }

    [Test]
    public void OnPathChosen_AccessibleNodeIsRevealedAndCommitted_WithoutCompletingOrUnlockingSuccessors()
    {
        _runManager.StartRun();
        var selectedNode = _runManager.currentPath.First(node => node.accessible);
        var successors = selectedNode.next
            .Select(id => _runManager.currentPath.Single(node => node.id == id))
            .ToArray();

        Assert.That(selectedNode.completed, Is.False);
        Assert.That(successors, Is.Not.Empty);
        Assert.That(successors.All(node => !node.accessible), Is.True);

        _runManager.OnPathChosen(selectedNode.id);

        Assert.That(selectedNode.revealed, Is.True);
        Assert.That(selectedNode.accessible, Is.False);
        Assert.That(selectedNode.completed, Is.False);
        Assert.That(_runManager.ActiveNode, Is.SameAs(selectedNode));
        Assert.That(successors.All(node => !node.accessible), Is.True);
        Assert.That(successors.All(node => !node.completed), Is.True);
        Assert.That(_combatManager.currentEnemy, Is.Not.Null);
    }

    void AssertBossGroup(int startIndex, CardData.Rank expectedRank)
    {
        var group = _runManager.bossDeck.Skip(startIndex).Take(4).ToArray();
        var expectedSuits = Enum.GetValues(typeof(CardData.Suit)).Cast<CardData.Suit>().ToArray();

        Assert.That(group, Has.Length.EqualTo(4));
        Assert.That(group.All(boss => boss != null && boss.sourceCard != null), Is.True);
        Assert.That(group.All(boss => boss.sourceCard.rank == expectedRank), Is.True);
        CollectionAssert.AreEquivalent(expectedSuits, group.Select(boss => boss.sourceCard.suit));
    }

    void AssertCardStateIsConserved()
    {
        var zoneCards = _cardManager.deck
            .Concat(_cardManager.hand)
            .Concat(_cardManager.discardPile)
            .ToArray();

        Assert.That(zoneCards, Has.Length.EqualTo(_cardManager.ownedCards.Count));
        Assert.That(zoneCards.Distinct().Count(), Is.EqualTo(zoneCards.Length));
        CollectionAssert.AreEquivalent(_cardManager.ownedCards, zoneCards);
    }

    EnemyTypeData CreateEnemyType(string name, int hp, int attack, int gold)
    {
        var enemyType = EnemyTypeData.Create(name, hp, attack, gold);
        _testOwnedAssets.Add(enemyType);
        return enemyType;
    }

    CardData CreateCard(CardData.Suit suit, CardData.Rank rank)
    {
        var card = CardData.Create(suit, rank);
        _testOwnedAssets.Add(card);
        return card;
    }

    static void DestroyGameObject(GameObject gameObject)
    {
        if (gameObject != null)
            UnityEngine.Object.DestroyImmediate(gameObject);
    }
}
