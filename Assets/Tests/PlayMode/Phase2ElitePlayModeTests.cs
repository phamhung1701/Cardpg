using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>PlayMode integration coverage for Royal Guard combat telegraphs and UI event subscriptions.</summary>
public sealed class Phase2ElitePlayModeTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    EnemyDisplayUI _display;
    TMP_Text _status;
    Field _field;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.Destroy(_created[i]);
        _created.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator RoyalKnightCombat_UpdatesShieldAndChargedTelegraphsThroughSubscriptions()
    {
        GameplayInputGate.Clear();
        _field = CreateHandField();
        var dragCanvas = CreateComponent<Canvas>("Phase 2 PlayMode Drag Canvas");
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var cardPrefab = CreateCardView("Phase 2 PlayMode Card Prefab");
        var cardsObject = CreateGameObject("Phase 2 PlayMode CardManager");
        _cards = cardsObject.AddComponent<CardManager>();
        _cards.Configure(_field, dragCanvas, cardPrefab, System.Array.Empty<RelicData>(),
            System.Array.Empty<CardEnhancementData>());
        _cards.ConfigureRandom(new DeterministicRandom(41));
        _cards.BuildDeck();
        _cards.DealHand();

        var combatObject = CreateGameObject("Phase 2 PlayMode CombatManager");
        _combat = combatObject.AddComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
        var displayObject = CreateGameObject("Royal Knight Intent Display", typeof(RectTransform));
        _display = displayObject.AddComponent<EnemyDisplayUI>();
        var statusObject = CreateGameObject("Royal Knight Intent Text", typeof(RectTransform));
        statusObject.transform.SetParent(displayObject.transform, false);
        _status = statusObject.AddComponent<TextMeshProUGUI>();
        _display.statusText = _status;

        var knightType = EnemyTypeData.Create("Royal Knight", 32, 5, 0);
        _created.Add(knightType);
        var royalGuard = ScriptableObject.CreateInstance<EnemyAbility>();
        _created.Add(royalGuard);
        royalGuard.effect = EnemyAbilityEffect.RoyalGuard;
        knightType.abilities = new[] { royalGuard };
        var knight = new EnemyRuntime(knightType);

        _combat.StartEnemy(knight);
        _display.Bind(knight);
        yield return null;

        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(_status.text, Does.Contain("Next: +1 Shield and 5 damage"));

        var attack = HandViews().OrderBy(view => _combat.CalculateCardAttackDamage(view.data)).FirstOrDefault();
        Assert.That(attack, Is.Not.Null, "The deterministic fixture should provide an attack card.");
        Assert.That(_combat.TryPlayCards(new[] { attack }, knight), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That((_combat.pendingDamage, knight.ShieldCharges), Is.EqualTo((5, 1)));
        Assert.That(_status.text, Does.Contain("Shield: 1"), "The state-change subscription should refresh the live display.");

        _combat.TakeRemainingDamage();
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(knight.ShieldCharges, Is.EqualTo(1));
        Assert.That(_status.text, Does.Contain("charged attack next (10 damage)"),
            "Taking the response advances Royal Guard intent and the subscribed UI updates without a manual refresh.");

        attack = HandViews().OrderBy(view => _combat.CalculateCardAttackDamage(view.data)).FirstOrDefault();
        Assert.That(attack, Is.Not.Null, "A remaining deterministic hand card should be available.");
        int knightHp = knight.currentHp;
        Assert.That(_combat.TryPlayCards(new[] { attack }, knight), Is.True);
        Assert.That((knight.currentHp, knight.ShieldCharges, _combat.pendingDamage), Is.EqualTo((knightHp, 0, 10)),
            "The next card consumes Royal Guard's shield and starts the charged response.");
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(_status.text, Does.Contain("Charged attack incoming (10 damage)"),
            "The EnemyAttacking state event should update the charged-attack warning without a manual refresh.");
    }

    IEnumerable<CardView> HandViews() => _field.cardsHolder.GetComponentsInChildren<CardView>()
        .Where(view => view != null && view.data != null && _cards.hand.Contains(view.data));

    Field CreateHandField()
    {
        var canvas = CreateComponent<Canvas>("Phase 2 PlayMode Hand Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = CreateGameObject("Phase 2 PlayMode Hand Holder", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(canvas.transform, false);
        var field = holder.AddComponent<Field>();
        field.cardsHolder = (RectTransform)holder.transform;
        return field;
    }

    CardView CreateCardView(string name)
    {
        var go = CreateGameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        var view = go.AddComponent<CardView>();
        view.face = go.GetComponent<Image>();
        view.canvasGroup = go.GetComponent<CanvasGroup>();
        return view;
    }

    T CreateComponent<T>(string name) where T : Component => CreateGameObject(name).AddComponent<T>();

    GameObject CreateGameObject(string name, params System.Type[] components)
    {
        var go = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        _created.Add(go);
        return go;
    }
}
