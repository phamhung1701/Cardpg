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
