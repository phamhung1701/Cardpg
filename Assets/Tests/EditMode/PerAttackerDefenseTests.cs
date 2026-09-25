using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class PerAttackerDefenseTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        var canvas = CreateComponent<Canvas>("Defense Hand Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = CreateGameObject("Hand Holder", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(canvas.transform, false);
        _field = holder.AddComponent<Field>();
        _field.cardsHolder = (RectTransform)holder.transform;
        var dragCanvas = CreateComponent<Canvas>("Defense Drag Canvas");
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = CreateGameObject("Defense Card Prefab", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = CreateComponent<CardManager>("Defense CardManager");
        _cards.Configure(_field, dragCanvas, prefab, System.Array.Empty<RelicData>(), System.Array.Empty<CardEnhancementData>());
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = CreateComponent<CombatManager>("Defense CombatManager");
        _combat.ConfigurePlayer(30);
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void OneDefenseSix_BlocksExactlyOneOfTwoThreeAttacks_AndLosesExcess()
    {
        var enemies = StartDefenseWindow(3, 3);
        AddDefenseBonus(6);
        var card = HandViews()[0];
        Assert.That(_combat.CalculateCardDefense(card.data), Is.GreaterThanOrEqualTo(6));
        int handBefore = _cards.HandCount;

        Assert.That(_combat.TryDefendWithCards(new[] { card }, enemies[1]), Is.True);
        Assert.That(_combat.PendingAttackCount, Is.EqualTo(1));
        Assert.That(_combat.BlockedAttackCount, Is.EqualTo(1));
        Assert.That(_combat.pendingDamage, Is.EqualTo(3));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(_cards.HandCount, Is.EqualTo(handBefore - 1));
    }

    [Test]
    public void TwoAdequateCards_BlockTwoDistinctAttacks_EvenWhenCommittedTogether()
    {
        StartDefenseWindow(3, 3);
        AddDefenseBonus(3);
        var pair = HandViews().Take(2).ToArray();
        _cards.ToggleCardSelection(pair[0]);
        _cards.ToggleCardSelection(pair[1]);
        Assert.That(_cards.SelectedCards, Is.EqualTo(pair));
        Assert.That(_combat.TryPreviewDefense(pair, out int count, out int remaining), Is.True);
        Assert.That((count, remaining), Is.EqualTo((2, 0)));
        int discarded = _cards.discardPile.Count;

        Assert.That(_combat.TryDefendWithCards(pair), Is.True);
        Assert.That(_cards.discardPile.Count, Is.EqualTo(discarded + 2));
        Assert.That(_combat.pendingDamage, Is.Zero);
        Assert.That(_combat.PendingAttackCount, Is.Zero);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void InsufficientAndUnmatchedSets_AreRejectedAtomically_AndCannotBeSelected()
    {
        StartDefenseWindow(3, 3);
        var cards = HandViews().OrderBy(card => _combat.CalculateCardDefense(card.data)).ToArray();
        int tooSmallDefense = _combat.CalculateCardDefense(cards[0].data);
        // Use attacks strictly above every hand card's defense in a separate window when needed.
        if (tooSmallDefense >= 3)
        {
            _combat.Reset();
            StartDefenseWindow(20, 20);
            cards = HandViews().ToArray();
        }
        var insufficient = cards.First(card => _combat.CalculateCardDefense(card.data) < _combat.pendingDamage / 2);
        int hand = _cards.HandCount;
        int discarded = _cards.discardPile.Count;
        int pending = _combat.pendingDamage;
        Assert.That(_combat.CanDefendWithCards(new[] { insufficient }), Is.False);
        _cards.ToggleCardSelection(insufficient);
        Assert.That(_cards.SelectedCards, Is.Empty);
        Assert.That(_combat.TryDefendWithCards(new[] { insufficient }), Is.False);
        Assert.That(_combat.TryDefendWithCards(cards), Is.False, "More cards than attacks cannot be submitted.");
        Assert.That((_cards.HandCount, _cards.discardPile.Count, _combat.pendingDamage), Is.EqualTo((hand, discarded, pending)));
    }

    [Test]
    public void DifferentAttacks_StrongestCardsMatchStrongestBlockableAttacks_RegardlessOfSubmissionOrder()
    {
        StartDefenseWindow(8, 5, 3);
        AddDefenseBonus(8);
        var pair = HandViews().Take(2).Reverse().ToArray();
        Assert.That(_combat.TryDefendWithCards(pair), Is.True);
        Assert.That(_combat.pendingDamage, Is.EqualTo(3), "Highest two attacks are matched, not arbitrary attacks.");
        Assert.That(_combat.PendingAttackCount, Is.EqualTo(1));
    }

    [Test]
    public void CardThatCannotBlockHighestAttack_CanMatchLowerAttack()
    {
        StartDefenseWindow(20, 3);
        AddDefenseBonus(3);
        var card = HandViews()[0];
        Assert.That(_combat.TryDefendWithCards(new[] { card }), Is.True);
        Assert.That(_combat.pendingDamage, Is.EqualTo(20));
        Assert.That(_combat.PendingAttackCount, Is.EqualTo(1));
    }

    [Test]
    public void DefenseWindow_SnapshotsOnlyPositiveLivingAttacks_AndIgnoresLaterAttackChanges()
    {
        var enemies = StartDefenseWindow(3, 0);
        Assert.That(_combat.PendingAttackCount, Is.EqualTo(1));
        enemies[0].ReduceAttack(3);
        Assert.That(_combat.TotalEnemyAttack, Is.Zero);
        Assert.That(_combat.pendingDamage, Is.EqualTo(3));
        AddDefenseBonus(3);
        Assert.That(_combat.TryDefendWithCards(new[] { HandViews()[0] }), Is.True);
        Assert.That(_combat.pendingDamage, Is.Zero);
    }

    [Test]
    public void RemainingDamage_ResolvesAsOneEnemyAggregateHit_AndConsumesOnlyOneShield()
    {
        StartDefenseWindow(3, 3);
        AddDefenseBonus(3);
        Assert.That(_combat.TryDefendWithCards(new[] { HandViews()[0] }), Is.True);
        _combat.GrantPlayerShield(1);
        var results = new List<DamageResult>();
        _combat.OnDamageResolved += results.Add;
        int health = _combat.player.currentHealth;

        _combat.TakeRemainingDamage();

        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].Request.Origin, Is.EqualTo(CombatDamageOrigin.EnemyAggregate));
        Assert.That(results[0].Request.RequestedDamage, Is.EqualTo(3));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(health));
        Assert.That(_combat.player.ShieldCharges, Is.Zero);
        Assert.That(_combat.pendingDamage, Is.Zero);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    EnemyRuntime[] StartDefenseWindow(params int[] attacks)
    {
        var enemies = new EnemyRuntime[attacks.Length];
        for (int i = 0; i < attacks.Length; i++)
        {
            var type = EnemyTypeData.Create($"Attacker {i}", 100, attacks[i], 0);
            _created.Add(type);
            enemies[i] = new EnemyRuntime(type);
        }
        _combat.StartEncounter(enemies);
        Assert.That(_combat.TryPlayCards(new[] { HandViews()[0] }, enemies[0]), Is.True);
        Assert.That(_combat.pendingDamage, Is.EqualTo(attacks.Sum()));
        return enemies;
    }

    void AddDefenseBonus(int bonus)
    {
        var relic = ScriptableObject.CreateInstance<RelicData>();
        relic.defenseBonus = bonus;
        _created.Add(relic);
        _cards.ownedArtifacts.Add(relic);
    }

    List<CardView> HandViews()
    {
        var views = new List<CardView>();
        for (int i = 0; i < _field.cardsHolder.childCount; i++)
            if (_field.cardsHolder.GetChild(i).TryGetComponent<CardView>(out var view)) views.Add(view);
        return views;
    }

    T CreateComponent<T>(string name) where T : Component => CreateGameObject(name).AddComponent<T>();

    GameObject CreateGameObject(string name, params System.Type[] components)
    {
        var gameObject = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        _created.Add(gameObject);
        return gameObject;
    }
}
