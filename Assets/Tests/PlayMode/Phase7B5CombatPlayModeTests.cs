using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class Phase7B5CombatPlayModeTests
{
    [UnityTest]
    public IEnumerator CardPointerSelection_ReplacesOrdinaryChoiceAndRespectsInputLock()
    {
        GameplayInputGate.Clear();
        var objects = new System.Collections.Generic.List<GameObject>();
        var handCanvasObject = new GameObject("Selection PlayMode Canvas", typeof(RectTransform), typeof(Canvas));
        objects.Add(handCanvasObject);
        var handObject = new GameObject("Selection PlayMode Hand", typeof(RectTransform));
        objects.Add(handObject);
        handObject.transform.SetParent(handCanvasObject.transform, false);
        var field = handObject.AddComponent<Field>();
        field.cardsHolder = (RectTransform)handObject.transform;

        var dragCanvasObject = new GameObject("Selection PlayMode Drag Canvas", typeof(RectTransform), typeof(Canvas));
        objects.Add(dragCanvasObject);
        var prefabObject = new GameObject("Selection PlayMode Card", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        objects.Add(prefabObject);
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();

        var cardManagerObject = new GameObject("Selection PlayMode CardManager");
        objects.Add(cardManagerObject);
        var cards = cardManagerObject.AddComponent<CardManager>();
        cards.Configure(field, dragCanvasObject.GetComponent<Canvas>(), prefab, System.Array.Empty<RelicData>());
        cards.BuildDeck();
        cards.DealHand();
        var combatObject = new GameObject("Selection PlayMode CombatManager");
        objects.Add(combatObject);
        var combat = combatObject.AddComponent<CombatManager>();
        var enemyType = EnemyTypeData.Create("Selection target", 100, 0, 0);
        combat.StartEnemy(new EnemyRuntime(enemyType));
        yield return null;

        var views = handObject.GetComponentsInChildren<CardView>();
        CardView first = null, second = null;
        foreach (var candidate in views)
        {
            if (candidate?.data == null || candidate.data.Rank == CardData.Rank.Ace) continue;
            if (first == null) first = candidate;
            else if (candidate.data.Rank != first.data.Rank) { second = candidate; break; }
        }
        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null);
        var firstPointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
        var secondPointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(first.gameObject, firstPointer, ExecuteEvents.pointerClickHandler);
        Assert.That(cards.SelectedCards, Has.Count.EqualTo(1));
        Assert.That(cards.SelectedCards[0], Is.SameAs(first));

        GameplayInputGate.Set(GameplayInputBlockReason.FrontendMenu, true);
        ExecuteEvents.Execute(second.gameObject, secondPointer, ExecuteEvents.pointerClickHandler);
        Assert.That(cards.SelectedCards, Has.Count.EqualTo(1));
        Assert.That(cards.SelectedCards[0], Is.SameAs(first), "A modal input lock prevents pointer selection changes.");
        GameplayInputGate.Clear();
        ExecuteEvents.Execute(second.gameObject, secondPointer, ExecuteEvents.pointerClickHandler);
        Assert.That(cards.SelectedCards, Has.Count.EqualTo(1));
        Assert.That(cards.SelectedCards[0], Is.SameAs(second), "A valid ordinary second choice replaces the first.");
        ExecuteEvents.Execute(second.gameObject, secondPointer, ExecuteEvents.pointerClickHandler);
        Assert.That(cards.SelectedCards, Is.Empty, "Clicking the selected card deselects it.");

        foreach (var go in objects) Object.Destroy(go);
        Object.Destroy(enemyType);
        GameplayInputGate.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator OverflowAutoPlay_ResolvesBeforeEnemyResponseAndReleasesInputLock()
    {
        GameplayInputGate.Clear();
        var objects = new System.Collections.Generic.List<GameObject>();
        var canvasObject = new GameObject("Overflow PlayMode Canvas", typeof(RectTransform), typeof(Canvas));
        objects.Add(canvasObject);
        var handObject = new GameObject("Overflow PlayMode Hand", typeof(RectTransform));
        objects.Add(handObject);
        handObject.transform.SetParent(canvasObject.transform, false);
        var field = handObject.AddComponent<Field>();
        field.cardsHolder = (RectTransform)handObject.transform;
        var dragObject = new GameObject("Overflow PlayMode Drag", typeof(RectTransform), typeof(Canvas));
        objects.Add(dragObject);
        var prefabObject = new GameObject("Overflow PlayMode Card", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        objects.Add(prefabObject);
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        var overflow = ScriptableObject.CreateInstance<RelicData>();
        overflow.id = "rel_015"; overflow.canonicalId = "rel_015"; overflow.displayName = "Overflow";
        overflow.rarity = "Common"; overflow.specialRule = ArtifactSpecialRule.OverflowCommon;
        var cardsObject = new GameObject("Overflow PlayMode CardManager");
        objects.Add(cardsObject);
        var cards = cardsObject.AddComponent<CardManager>();
        cards.Configure(field, dragObject.GetComponent<Canvas>(), prefab, new[] { overflow });
        cards.BuildDeck();
        cards.DealHand();
        Assert.That(cards.BuyArtifact(overflow, 0), Is.True);
        var combatObject = new GameObject("Overflow PlayMode CombatManager");
        objects.Add(combatObject);
        var combat = combatObject.AddComponent<CombatManager>();
        var enemyType = EnemyTypeData.Create("Overflow target", 500, 1, 0);
        combat.StartEnemy(new EnemyRuntime(enemyType));
        var draw = ScriptableObject.CreateInstance<CardEnhancementData>();
        draw.displayName = "Quickdraw Two"; draw.drawOnPlay = 2;
        var handViews = handObject.GetComponentsInChildren<CardView>();
        Assert.That(handViews.Length, Is.GreaterThanOrEqualTo(2));
        Assert.That(handViews[0].data.TryApplyEnhancement(draw), Is.True);
        int hits = 0;
        combat.OnDamageResolved += _ => hits++;
        Assert.That(combat.TryPlayCards(new[] { handViews[0] }, combat.currentEnemy), Is.True);
        Assert.That(hits, Is.EqualTo(2));
        Assert.That(combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(combat.IsResolvingAction, Is.False, "Queued auto-play resolution releases the combat input lock.");
        yield return null;
        foreach (var go in objects) Object.Destroy(go);
        Object.Destroy(overflow);
        Object.Destroy(draw);
        Object.Destroy(enemyType);
        GameplayInputGate.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator ShieldedMultiHit_ResolvesAsIndependentRuntimeHits()
    {
        var type = EnemyTypeData.Create("PlayMode Target", 20, 0, 0);
        var guard = ScriptableObject.CreateInstance<EnemyAbility>();
        guard.effect = EnemyAbilityEffect.ReduceIncomingCardDamage;
        guard.amount = 1;
        type.abilities = new[] { guard };
        var enemy = new EnemyRuntime(type);
        var player = new PlayerRuntime();
        enemy.GainShield(1);
        var action = new CombatActionContext(
            1,
            CombatActionOrigin.PlayerCard,
            sourcePlayer: player,
            targetEnemy: enemy,
            hitCount: 3);
        var resolver = new CombatResolver();

        var first = resolver.Resolve(new DamageRequest(action, 0, CombatDamageOrigin.Card, player, enemy, 5), null);
        var second = resolver.Resolve(new DamageRequest(action, 1, CombatDamageOrigin.Card, player, enemy, 5), null);
        var third = resolver.Resolve(new DamageRequest(action, 2, CombatDamageOrigin.Card, player, enemy, 5), null);

        Assert.That(first.ShieldConsumed, Is.True);
        Assert.That(first.ActualHpLost, Is.Zero);
        Assert.That(second.ActualHpLost, Is.EqualTo(4));
        Assert.That(third.ActualHpLost, Is.EqualTo(4));
        Assert.That(enemy.currentHp, Is.EqualTo(12));
        Assert.That(enemy.ShieldCharges, Is.Zero);

        Object.Destroy(type);
        Object.Destroy(guard);
        yield return null;
    }
}
