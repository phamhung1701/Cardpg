using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ContextualConsumableAndEnemyTargetingTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    ActionButtonsUI _actions;
    Button _attackButton;
    Button _backpackButton;
    ConsumableData _heal;
    ConsumableData _knife;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _knife = UnityEditor.AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Consumables/ThrowingKnife.asset");
        Assert.That(_knife, Is.Not.Null);
        _heal = ScriptableObject.CreateInstance<ConsumableData>();
        _created.Add(_heal);
        _heal.id = "ui_test_heal";
        _heal.displayName = "Test Tonic";
        _heal.description = "Restore 5 health.";
        _heal.price = 10;
        _heal.effectType = ConsumableEffectType.Heal;
        _heal.healAmount = 5;

        var fieldRoot = Create("Targeting Test Hand", typeof(RectTransform), typeof(Field));
        var field = fieldRoot.GetComponent<Field>();
        field.cardsHolder = fieldRoot.GetComponent<RectTransform>();
        var dragCanvasObject = Create("Targeting Test Drag Canvas", typeof(Canvas));
        var cardPrefabObject = Create("Targeting Test Card Prefab", typeof(RectTransform), typeof(Image), typeof(CardView));
        _cards = Create("Targeting Test Cards", typeof(CardManager)).GetComponent<CardManager>();
        _cards.Configure(field, dragCanvasObject.GetComponent<Canvas>(), cardPrefabObject.GetComponent<CardView>(),
            System.Array.Empty<RelicData>(), System.Array.Empty<CardEnhancementData>(), new[] { _knife, _heal });
        _cards.ConfigureRandom(new DeterministicRandom(540021));
        _cards.BuildDeck();

        _combat = Create("Targeting Test Combat", typeof(CombatManager)).GetComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
        _run = Create("Targeting Test Run", typeof(RunManager)).GetComponent<RunManager>();
        CreateActionButtons();
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        foreach (var item in _created.AsEnumerable().Reverse())
            if (item) Object.DestroyImmediate(item);
        _created.Clear();
    }

    [Test]
    public void ConsumableClickSelectsWithoutUsing_SellIsShopGatedAndDelegatesToRunManager()
    {
        _combat.player.TakeDamage(10);
        Assert.That(_cards.AddConsumable(_heal), Is.True);
        _backpackButton.onClick.Invoke();

        Assert.That(_combat.player.currentHealth, Is.EqualTo(20), "Selecting an item must not execute it.");
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(1));
        var use = GetPrivate<Button>(_actions, "_consumableUseButton");
        var sell = GetPrivate<Button>(_actions, "_consumableSellButton");
        Assert.That(use.interactable, Is.True);
        Assert.That(sell.interactable, Is.False);
        var outsideShopItem = _cards.GetConsumableInstanceAtSlot(0);
        Assert.That(_run.SellConsumable(0, outsideShopItem, _heal.price), Is.False,
            "The authoritative sale command must reject every outside-Shop call path.");
        Assert.That(_cards.GetConsumableAtSlot(0), Is.SameAs(_heal));

        var shop = new PathNode { kind = MapNodeType.Shop, completed = false };
        typeof(RunManager).GetField("_activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_run, shop);
        InvokePrivate(_actions, "RefreshAll");
        Assert.That(_run.CanSellItems, Is.True);
        Assert.That(sell.interactable, Is.True);
        sell.onClick.Invoke();
        sell.onClick.Invoke();

        Assert.That(_cards.GetConsumableAtSlot(0), Is.Null);
        Assert.That(_cards.gold, Is.EqualTo(5), "The selected instance is sold and paid exactly once.");
        Assert.That(_actions.transform.Find("Selected Consumable Actions").gameObject.activeSelf, Is.False);

        Assert.That(_cards.AddConsumable(_heal), Is.True);
        _backpackButton.onClick.Invoke();
        Assert.That(_actions.transform.Find("Selected Consumable Actions").gameObject.activeSelf, Is.True);
        _run.OnShopDone();
        Assert.That(_actions.transform.Find("Selected Consumable Actions").gameObject.activeSelf, Is.False,
            "Leaving the Shop clears the selected Consumable context.");
        Assert.That(_run.CanSellItems, Is.False);
    }

    [Test]
    public void VictoryDefeatAndRestartClearPendingTargetAndConsumableContexts()
    {
        var enemies = StartEncounter(2);
        Assert.That(_cards.AddConsumable(_knife), Is.True);
        _backpackButton.onClick.Invoke();
        GetPrivate<Button>(_actions, "_consumableUseButton").onClick.Invoke();
        Assert.That(_cards.IsEnemyTargeting, Is.True);
        foreach (var enemy in enemies)
            if (!_combat.player.IsDefeated) _combat.TryUseConsumableDamage(100, enemy);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameWon));
        Assert.That(_cards.IsEnemyTargeting, Is.False);
        Assert.That(_actions.transform.Find("Selected Consumable Actions").gameObject.activeSelf, Is.False);

        var dangerous = EnemyTypeData.Create("Defeat cleanup target", 100, 100, 0);
        _created.Add(dangerous);
        _combat.StartEnemy(new EnemyRuntime(dangerous));
        SelectHandCard();
        _attackButton.onClick.Invoke();
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        _backpackButton.onClick.Invoke();
        Assert.That(_actions.transform.Find("Selected Consumable Actions").gameObject.activeSelf, Is.True);
        _combat.TakeRemainingDamage();
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameOver));
        Assert.That(_actions.transform.Find("Selected Consumable Actions").gameObject.activeSelf, Is.False);

        _run.thiefType = dangerous;
        _run.goblinType = dangerous;
        _run.knightType = dangerous;
        _backpackButton.onClick.Invoke();
        Assert.That(_actions.transform.Find("Selected Consumable Actions").gameObject.activeSelf, Is.True);
        _run.StartRunWithSeed("target-ux-cleanup");
        Assert.That(_cards.IsEnemyTargeting, Is.False);
        Assert.That(_cards.BackpackSlotsUsed, Is.Zero);
        Assert.That(_actions.transform.Find("Selected Consumable Actions").gameObject.activeSelf, Is.False);
        Assert.That(_cards.SelectedCards, Is.Empty);
    }

    [Test]
    public void AttackButtonResolvesDirectlyWhenOnlyOneEnemyIsValid()
    {
        var enemy = StartEncounter(1)[0];
        var card = SelectHandCard();

        _attackButton.onClick.Invoke();

        Assert.That(_cards.IsEnemyTargeting, Is.False);
        Assert.That(enemy.currentHp, Is.LessThan(enemy.maxHp));
        Assert.That(_cards.hand.Contains(card.data), Is.False);
    }

    [Test]
    public void AttackButtonUsesActualEnemyViewWhenMultipleTargetsAreValid_AndCancelRestoresInput()
    {
        var enemies = StartEncounter(2);
        SelectHandCard();

        _attackButton.onClick.Invoke();

        Assert.That(_cards.IsEnemyTargeting, Is.True);
        Assert.That(enemies[0].currentHp, Is.EqualTo(enemies[0].maxHp));
        Assert.That(enemies[1].currentHp, Is.EqualTo(enemies[1].maxHp));
        var clickTarget = CreateEnemyView(enemies[1]);
        clickTarget.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });

        Assert.That(_cards.IsEnemyTargeting, Is.False);
        Assert.That(enemies[0].currentHp, Is.EqualTo(enemies[0].maxHp));
        Assert.That(enemies[1].currentHp, Is.LessThan(enemies[1].maxHp));

        var selected = SelectHandCard();
        _attackButton.onClick.Invoke();
        Assert.That(_cards.IsEnemyTargeting, Is.True);
        _cards.EndEnemyTargeting();
        Assert.That(_cards.IsEnemyTargeting, Is.False);
        Assert.That(_cards.hand.Contains(selected.data), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void OffensiveConsumableUseTargetsTheClickedEnemyAndSingleEnemyNeedsNoPicker()
    {
        var enemies = StartEncounter(2);
        Assert.That(_cards.AddConsumable(_knife), Is.True);
        Assert.That(_cards.GetConsumableTargetTypeAtSlot(0), Is.EqualTo(ConsumableTargetType.Enemy));
        Assert.That(_cards.GetConsumableEffectTargetMinimumAtSlot(0), Is.EqualTo(1));
        Assert.That(_cards.GetConsumableEffectTargetMaximumAtSlot(0), Is.EqualTo(1));
        Assert.That(_cards.GetValidEnemyTargetsForConsumableAtSlot(0), Has.Count.EqualTo(2));
        _backpackButton.onClick.Invoke();
        var use = GetPrivate<Button>(_actions, "_consumableUseButton");
        Assert.That(use.interactable, Is.True);
        use.onClick.Invoke();

        Assert.That(_cards.IsEnemyTargeting, Is.True);
        var invalidType = EnemyTypeData.Create("Not in this encounter", 50, 0, 0);
        _created.Add(invalidType);
        var invalidEnemy = new EnemyRuntime(invalidType);
        var invalidView = CreateEnemyView(invalidEnemy);
        invalidView.GetComponent<EnemyDisplayUI>().SetEnemyTargeting(true, false);
        Assert.That(invalidView.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0.38f));
        invalidView.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(_cards.IsEnemyTargeting, Is.True, "An enemy outside the authoritative valid-target set cannot resolve the action.");
        Assert.That(invalidEnemy.currentHp, Is.EqualTo(invalidEnemy.maxHp));
        _cards.EndEnemyTargeting();
        Assert.That(_cards.IsEnemyTargeting, Is.False);
        Assert.That(_cards.GetConsumableAtSlot(0), Is.SameAs(_knife), "Cancel preserves the item and spends no charge.");
        var normalCard = _cards.handField.cardsHolder.GetComponentsInChildren<CardView>(true)
            .Single(view => view.data == _cards.hand[0]);
        _cards.HandleCardClick(normalCard);
        Assert.That(_cards.SelectedCards, Does.Contain(normalCard), "Normal hand interaction resumes after target cancel.");
        _cards.ClearSelection();
        _backpackButton.onClick.Invoke();
        use.onClick.Invoke();
        Assert.That(_cards.IsEnemyTargeting, Is.True, "Use begins enemy targeting again after cancel.");
        var clickTarget = CreateEnemyView(enemies[0]);
        clickTarget.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(enemies[0].currentHp, Is.EqualTo(enemies[0].maxHp - _knife.damageAmount));
        Assert.That(enemies[1].currentHp, Is.EqualTo(enemies[1].maxHp));
        Assert.That(_cards.IsEnemyTargeting, Is.False);
        Assert.That(_cards.GetConsumableAtSlot(0), Is.Null);
    }

    [Test]
    public void OffensiveConsumableUsesSingleEnemyDirectlyAfterUseConfirmation()
    {
        var singleEnemy = StartEncounter(1)[0];
        Assert.That(_cards.AddConsumable(_knife), Is.True);
        _backpackButton.onClick.Invoke();
        var use = GetPrivate<Button>(_actions, "_consumableUseButton");
        Assert.That(use.interactable, Is.True);
        use.onClick.Invoke();
        Assert.That(_cards.IsEnemyTargeting, Is.False);
        Assert.That(singleEnemy.currentHp, Is.EqualTo(singleEnemy.maxHp - _knife.damageAmount));
        Assert.That(_cards.BackpackSlotsUsed, Is.Zero);
    }

    EnemyRuntime[] StartEncounter(int count)
    {
        var definition = EnemyTypeData.Create("Direct target test enemy", 100, 0, 0);
        _created.Add(definition);
        var enemies = Enumerable.Range(0, count).Select(i => new EnemyRuntime(definition, i + 1)).ToArray();
        _combat.StartEncounter(enemies);
        _cards.DealHand();
        return enemies;
    }

    CardView SelectHandCard()
    {
        var instance = _cards.hand[0];
        var view = _cards.handField.cardsHolder.GetComponentsInChildren<CardView>(true)
            .Single(card => card.data == instance);
        _cards.SelectOnlyCard(view);
        Assert.That(_cards.SelectedCards, Does.Contain(view));
        return view;
    }

    CardActionDropTarget CreateEnemyView(EnemyRuntime enemy)
    {
        var go = Create("Actual Enemy View", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(EnemyDisplayUI), typeof(CardActionDropTarget));
        var display = go.GetComponent<EnemyDisplayUI>();
        display.portrait = go.GetComponent<Image>();
        display.Bind(enemy);
        var target = go.GetComponent<CardActionDropTarget>();
        target.enemyDisplay = display;
        return target;
    }

    void CreateActionButtons()
    {
        var root = Create("Targeting Test Player Area", typeof(RectTransform));
        root.SetActive(false);
        _actions = root.AddComponent<ActionButtonsUI>();
        var attackObject = Create("Attack Action", typeof(RectTransform), typeof(Image), typeof(Button));
        attackObject.transform.SetParent(root.transform, false);
        _attackButton = attackObject.GetComponent<Button>();
        _attackButton.targetGraphic = attackObject.GetComponent<Image>();
        var attackLabelObject = Create("Attack Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        attackLabelObject.transform.SetParent(attackObject.transform, false);
        _actions.playButton = _attackButton;
        var backpackObject = Create("Backpack Slot", typeof(RectTransform), typeof(Image), typeof(Button));
        backpackObject.transform.SetParent(root.transform, false);
        _backpackButton = backpackObject.GetComponent<Button>();
        _backpackButton.targetGraphic = backpackObject.GetComponent<Image>();
        var backpackLabel = Create("Backpack Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        backpackLabel.transform.SetParent(backpackObject.transform, false);
        _actions.backpackSlotButtons = new[] { _backpackButton };
        root.SetActive(true);
        InvokePrivate(_actions, "OnDisable");
        InvokePrivate(_actions, "OnEnable");
    }

    T GetPrivate<T>(object target, string name) where T : class =>
        typeof(ActionButtonsUI).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target) as T;

    static void InvokePrivate(object target, string name) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    GameObject Create(string name, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        _created.Add(go);
        return go;
    }
}
