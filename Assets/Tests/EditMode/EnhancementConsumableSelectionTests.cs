using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EnhancementConsumableSelectionTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    ActionButtonsUI _actions;
    Button[] _backpackButtons;
    CardEnhancementData _enhancement;
    ConsumableData _patch;
    ConsumableData _tonic;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
        _enhancement.id = "copy-flow-enhancement";
        _enhancement.displayName = "Focused Mod";
        _enhancement.description = "Adds three attack.";
        _enhancement.attackBonus = 3;
        _created.Add(_enhancement);
        _patch = ScriptableObject.CreateInstance<ConsumableData>();
        _patch.id = "copy-flow-patch";
        _patch.displayName = "Focused Patch";
        _patch.description = "Target: Card. Effect: Apply Focused Mod.";
        _patch.effectType = ConsumableEffectType.ApplyEnhancement;
        _patch.enhancementToApply = _enhancement;
        _patch.uses = 2;
        _created.Add(_patch);
        _tonic = ScriptableObject.CreateInstance<ConsumableData>();
        _tonic.id = "copy-flow-tonic";
        _tonic.displayName = "Tonic";
        _tonic.effectType = ConsumableEffectType.Heal;
        _tonic.healAmount = 3;
        _created.Add(_tonic);

        var handObject = Make("Enhancement Flow Hand", typeof(RectTransform), typeof(Field));
        var handField = handObject.GetComponent<Field>();
        handField.cardsHolder = handObject.GetComponent<RectTransform>();
        var canvasObject = Make("Enhancement Flow Canvas", typeof(Canvas));
        var cardPrefabObject = Make("Enhancement Flow Card Prefab", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        var prefab = cardPrefabObject.AddComponent<CardView>();
        prefab.face = cardPrefabObject.GetComponent<Image>();
        prefab.canvasGroup = cardPrefabObject.GetComponent<CanvasGroup>();
        _cards = Make("Enhancement Flow Cards", typeof(CardManager)).GetComponent<CardManager>();
        _cards.Configure(handField, canvasObject.GetComponent<Canvas>(), prefab,
            System.Array.Empty<RelicData>(), new[] { _enhancement }, new[] { _patch, _tonic });
        _cards.ConfigureRandom(new DeterministicRandom(38038));
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = Make("Enhancement Flow Combat", typeof(CombatManager)).GetComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
        CreateActionButtons();
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        if (_actions != null) InvokePrivate(_actions, "OnDisable");
        foreach (var item in _created.AsEnumerable().Reverse())
            if (item) Object.DestroyImmediate(item);
        _created.Clear();
    }

    [Test]
    public void SelectCardThenEnhancementTrickAndUse_AppliesExactInstanceAndSpendsOneCharge()
    {
        StartEncounter();
        Assert.That(_cards.AddConsumable(_patch), Is.True);
        var targetView = HandView(_cards.hand[0]);
        var otherCard = _cards.hand[1];
        _cards.SelectOnlyCard(targetView);
        Assert.That(_cards.AddConsumable(_tonic), Is.True);
        _backpackButtons[0].onClick.Invoke();

        var use = GetPrivate<Button>("_consumableUseButton");
        var details = GetPrivate<TMP_Text>("_consumableContextText");
        var instance = _cards.GetConsumableInstanceAtSlot(0);
        int usedNotifications = 0;
        bool reentrantUse = true;
        _cards.OnConsumableUsed += _ =>
        {
            usedNotifications++;
            reentrantUse = _cards.UseEnhancementConsumableAtSlot(0, targetView.data.Id);
        };

        Assert.That(_cards.SelectedCards, Has.Count.EqualTo(1));
        Assert.That(_cards.SelectedCards[0], Is.SameAs(targetView));
        Assert.That(details.text, Does.Contain(_enhancement.displayName));
        Assert.That(details.text, Does.Contain(targetView.data.DisplayName));
        Assert.That(use.interactable, Is.True);
        use.onClick.Invoke();

        Assert.That(targetView.data.Enhancement, Is.SameAs(_enhancement));
        Assert.That(_cards.FindOwnedCard(targetView.data.Id), Is.SameAs(targetView.data));
        Assert.That(otherCard.Enhancement, Is.Null);
        Assert.That(instance.RemainingCharges, Is.EqualTo(1));
        Assert.That(usedNotifications, Is.EqualTo(1));
        Assert.That(reentrantUse, Is.False, "A nested confirmation cannot apply/spend the same enhancement Trick twice.");
        Assert.That(_cards.SelectedCards, Does.Contain(targetView), "Using the Trick does not play or discard the selected card.");
        Assert.That(_cards.UseEnhancementConsumableAtSlot(0, targetView.data.Id), Is.False,
            "A second attempt cannot apply or spend another charge on an already-enhanced card.");
        Assert.That(instance.RemainingCharges, Is.EqualTo(1));
    }

    [Test]
    public void DeselectOrSwitchSelectedCardCancelsEnhancementUseWithoutSpending()
    {
        StartEncounter();
        Assert.That(_cards.AddConsumable(_patch), Is.True);
        var firstView = HandView(_cards.hand[0]);
        var secondView = HandView(_cards.hand[1]);
        var source = _cards.GetConsumableInstanceAtSlot(0);
        _cards.SelectOnlyCard(firstView);
        _backpackButtons[0].onClick.Invoke();
        Assert.That(GetPrivate<Button>("_consumableUseButton").interactable, Is.True);

        _cards.SelectOnlyCard(secondView);

        Assert.That(firstView.data.Enhancement, Is.Null);
        Assert.That(secondView.data.Enhancement, Is.Null);
        Assert.That(source.RemainingCharges, Is.EqualTo(_patch.uses));
        Assert.That(_actions.transform.Find("Selected Consumable Actions").gameObject.activeSelf, Is.False,
            "Changing card selection clears the selected Trick context.");

        _cards.SelectOnlyCard(firstView);
        _backpackButtons[0].onClick.Invoke();
        _backpackButtons[0].onClick.Invoke();
        Assert.That(source.RemainingCharges, Is.EqualTo(_patch.uses));
        Assert.That(firstView.data.Enhancement, Is.Null, "Deselecting the Trick cancels without applying.");
    }

    [Test]
    public void TargetRemovedAfterCardAndTrickSelectionDisablesUseWithoutCharging()
    {
        StartEncounter();
        Assert.That(_cards.AddConsumable(_patch), Is.True);
        var targetView = HandView(_cards.hand[0]);
        var source = _cards.GetConsumableInstanceAtSlot(0);
        _cards.SelectOnlyCard(targetView);
        _backpackButtons[0].onClick.Invoke();
        Assert.That(GetPrivate<Button>("_consumableUseButton").interactable, Is.True);

        Assert.That(_cards.DestroyOwnedCard(targetView.data), Is.True);

        var use = GetPrivate<Button>("_consumableUseButton");
        Assert.That(use.interactable, Is.False);
        use.onClick.Invoke();
        Assert.That(source.RemainingCharges, Is.EqualTo(_patch.uses));
        Assert.That(_cards.GetConsumableInstanceAtSlot(0), Is.SameAs(source));
        Assert.That(source.Definition.enhancementToApply, Is.SameAs(_enhancement));
    }

    [Test]
    public void RemovingSelectedEnhancementTrickCancelsContextWithoutCharging()
    {
        StartEncounter();
        Assert.That(_cards.AddConsumable(_patch), Is.True);
        var target = HandView(_cards.hand[0]);
        var source = _cards.GetConsumableInstanceAtSlot(0);
        _cards.SelectOnlyCard(target);
        _backpackButtons[0].onClick.Invoke();
        Assert.That(GetPrivate<Button>("_consumableUseButton").interactable, Is.True);

        Assert.That(_cards.RemoveConsumableInstanceAtSlot(0, source), Is.True);

        Assert.That(_actions.transform.Find("Selected Consumable Actions").gameObject.activeSelf, Is.False);
        Assert.That(source.RemainingCharges, Is.EqualTo(_patch.uses));
        Assert.That(target.data.Enhancement, Is.Null);
    }

    [Test]
    public void RunResetClearsSelectedEnhancementTrickAndCardTargetWithoutApplying()
    {
        StartEncounter();
        Assert.That(_cards.AddConsumable(_patch), Is.True);
        var targetView = HandView(_cards.hand[0]);
        var target = targetView.data;
        var source = _cards.GetConsumableInstanceAtSlot(0);
        _cards.SelectOnlyCard(targetView);
        _backpackButtons[0].onClick.Invoke();
        Assert.That(GetPrivate<Button>("_consumableUseButton").interactable, Is.True);

        _cards.Reset();
        InvokePrivate(_actions, "HandleRunStarted", "NEW-RUN");

        Assert.That(_cards.BackpackSlotsUsed, Is.Zero);
        Assert.That(_cards.SelectedCards, Is.Empty);
        Assert.That(_actions.transform.Find("Selected Consumable Actions").gameObject.activeSelf, Is.False);
        Assert.That(target.Enhancement, Is.Null);
        Assert.That(source.RemainingCharges, Is.EqualTo(_patch.uses), "Reset removes the item without consuming a use.");
    }

    [Test]
    public void SwitchingSelectedTrickCancelsEnhancementSelectionWithoutSpending()
    {
        StartEncounter();
        Assert.That(_cards.AddConsumable(_patch), Is.True);
        Assert.That(_cards.AddConsumable(_tonic), Is.True);
        var target = HandView(_cards.hand[0]);
        var source = _cards.GetConsumableInstanceAtSlot(0);
        _cards.SelectOnlyCard(target);
        _backpackButtons[0].onClick.Invoke();
        Assert.That(GetPrivate<Button>("_consumableUseButton").interactable, Is.True);

        _backpackButtons[1].onClick.Invoke();

        Assert.That(_cards.SelectedCards, Is.Empty);
        Assert.That(GetPrivate<TMP_Text>("_consumableContextText").text, Does.Contain(_tonic.displayName));
        Assert.That(source.RemainingCharges, Is.EqualTo(_patch.uses));
        Assert.That(target.data.Enhancement, Is.Null);
    }

    [Test]
    public void ZeroChargeEnhancementTrickCannotApplyOrEnableUse()
    {
        StartEncounter();
        Assert.That(_cards.AddConsumable(_patch), Is.True);
        var target = HandView(_cards.hand[0]);
        var source = _cards.GetConsumableInstanceAtSlot(0);
        _cards.SelectOnlyCard(target);
        _backpackButtons[0].onClick.Invoke();
        Assert.That(source.SpendCharge(), Is.True);
        Assert.That(source.SpendCharge(), Is.True);
        InvokePrivate(_actions, "RefreshAll");

        Assert.That(_cards.CanUseConsumableAtSlot(0, _combat), Is.False);
        Assert.That(_cards.UseEnhancementConsumableAtSlot(0, target.data.Id), Is.False);
        Assert.That(target.data.Enhancement, Is.Null);
    }

    [Test]
    public void InvalidOrDepartedTargetAndStaleSourceSpendNothing()
    {
        StartEncounter();
        Assert.That(_cards.AddConsumable(_patch), Is.True);
        var targetView = HandView(_cards.hand[0]);
        var source = _cards.GetConsumableInstanceAtSlot(0);
        Assert.That(_cards.ApplyEnhancement(targetView.data.Id, _enhancement), Is.True);
        _cards.SelectOnlyCard(targetView);
        _backpackButtons[0].onClick.Invoke();
        Assert.That(GetPrivate<Button>("_consumableUseButton").interactable, Is.False);
        GetPrivate<Button>("_consumableUseButton").onClick.Invoke();
        Assert.That(source.RemainingCharges, Is.EqualTo(_patch.uses));

        var secondView = HandView(_cards.hand.First(card => card != targetView.data));
        _cards.SelectOnlyCard(secondView);
        _backpackButtons[0].onClick.Invoke();
        var use = GetPrivate<Button>("_consumableUseButton");
        Assert.That(use.interactable, Is.True);
        Assert.That(_cards.RemoveConsumableInstanceAtSlot(0, source), Is.True);
        Assert.That(_actions.transform.Find("Selected Consumable Actions").gameObject.activeSelf, Is.False);
        use.onClick.Invoke();
        Assert.That(source.RemainingCharges, Is.EqualTo(_patch.uses));
        Assert.That(secondView.data.Enhancement, Is.Null);
    }

    void StartEncounter()
    {
        var enemy = EnemyTypeData.Create("Enhancement Flow Enemy", 100, 0, 0);
        _created.Add(enemy);
        _combat.StartEnemy(new EnemyRuntime(enemy));
    }

    CardView HandView(CardInstance instance) => _cards.handField.cardsHolder.GetComponentsInChildren<CardView>(true)
        .Single(view => view.data == instance);

    void CreateActionButtons()
    {
        var root = Make("Enhancement Flow Actions", typeof(RectTransform));
        root.SetActive(false);
        _actions = root.AddComponent<ActionButtonsUI>();
        var play = MakeButton(root.transform, "Attack Button");
        _actions.playButton = play;
        _backpackButtons = new[] { MakeButton(root.transform, "Backpack 0"), MakeButton(root.transform, "Backpack 1") };
        _actions.backpackSlotButtons = _backpackButtons;
        _actions.selectionLabel = Make("Action Selection Label", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TMP_Text>();
        _actions.selectionLabel.transform.SetParent(root.transform, false);
        root.SetActive(true);
        InvokePrivate(_actions, "OnDisable");
        InvokePrivate(_actions, "OnEnable");
    }

    Button MakeButton(Transform parent, string name)
    {
        var go = Make(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        var label = Make(name + " Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(go.transform, false);
        return button;
    }

    T GetPrivate<T>(string fieldName) where T : class =>
        typeof(ActionButtonsUI).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_actions) as T;

    static void InvokePrivate(object target, string methodName, params object[] arguments) =>
        target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);

    GameObject Make(string name, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        _created.Add(go);
        return go;
    }
}
