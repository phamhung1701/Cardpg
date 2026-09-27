using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class TargetedConsumableTests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    ConsumableData _knife, _whetstone, _bomb;
    CardEnhancementData _sharpened;
    RunEventDefinition _blacksmith;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _knife = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Consumables/ThrowingKnife.asset");
        _whetstone = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Consumables/Whetstone.asset");
        _bomb = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Consumables/Bomb.asset");
        _sharpened = AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Enhancements/Sharpened.asset");
        _blacksmith = AssetDatabase.LoadAssetAtPath<RunEventDefinition>("Assets/Data/Run/Events/Blacksmith.asset");
        Assert.That(new UnityEngine.Object[] { _knife, _whetstone, _bomb, _sharpened, _blacksmith }, Has.All.Not.Null);

        _cards = Make<CardManager>("Targeted Consumable Cards");
        _cards.Configure(null, null, null, System.Array.Empty<RelicData>(),
            System.Array.Empty<CardEnhancementData>(), new[] { _knife, _whetstone, _bomb });
        _cards.ConfigureRandom(new DeterministicRandom(3207));
        _cards.BuildDeck();
        _combat = Make<CombatManager>("Targeted Consumable Combat");
        _combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Targeted Consumable Run");
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
    public void ThrowingKnife_DealsFiveToSelectedEnemyWithoutEndingPlayersTurn()
    {
        var first = Enemy("First Knife Target", 30, 0, 0);
        var selected = Enemy("Selected Knife Target", 30, 0, 0);
        _combat.StartEncounter(new[] { new EnemyRuntime(first), new EnemyRuntime(selected) });
        Assert.That(_combat.SelectEnemyTarget(_combat.Enemies[1]), Is.True);
        Assert.That(_cards.AddConsumable(_knife), Is.True);

        Assert.That(_cards.CanUseConsumableAtSlot(0, _combat), Is.True);
        Assert.That(_cards.UseConsumableAtSlot(0, _combat), Is.True);

        Assert.That(_combat.Enemies[0].currentHp, Is.EqualTo(30));
        Assert.That(_combat.Enemies[1].currentHp, Is.EqualTo(25));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(_cards.BackpackSlotsUsed, Is.Zero);
    }

    [Test]
    public void ThrowingKnife_IsUnavailableOutsideCombatOrOutsidePlayerTurn()
    {
        Assert.That(_cards.AddConsumable(_knife), Is.True);
        Assert.That(_cards.CanUseConsumableAtSlot(0, _combat), Is.False);

        var enemy = Enemy("Knife Target", 30, 1, 0);
        _combat.StartEnemy(new EnemyRuntime(enemy));
        Assert.That(_cards.CanUseConsumableAtSlot(0, _combat), Is.True);
        Assert.That(_cards.UseConsumableAtSlot(0, _combat), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void Whetstone_AppliesSharpenedToChosenUnenhancedCardAndConsumesThatSlot()
    {
        Assert.That(_cards.AddConsumable(_whetstone), Is.True);
        var card = _cards.ownedCards.First(value => value.Enhancement == null);
        int baseDamage = _combat.CalculateCardAttackDamage(card);
        Assert.That(_cards.CanUseConsumableAtSlot(0, _combat), Is.True);

        Assert.That(_cards.UseEnhancementConsumableAtSlot(0, card.Id), Is.True);

        Assert.That(card.Enhancement, Is.SameAs(_sharpened));
        Assert.That(_combat.CalculateCardAttackDamage(card), Is.EqualTo(baseDamage + 3));
        Assert.That(_cards.BackpackSlotsUsed, Is.Zero);
    }

    [Test]
    public void Whetstone_CannotReplaceAnExistingEnhancementAndIsNotConsumed()
    {
        Assert.That(_cards.AddConsumable(_whetstone), Is.True);
        var card = _cards.ownedCards.First(value => value.Enhancement == null);
        Assert.That(_cards.ApplyEnhancement(card.Id, _sharpened), Is.True);

        Assert.That(_cards.UseEnhancementConsumableAtSlot(0, card.Id), Is.False);

        Assert.That(card.Enhancement, Is.SameAs(_sharpened));
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(1));
    }

    [Test]
    public void EnhancementConsumables_AreExcludedFromEnemyDropsEvenIfFlagged()
    {
        var testItem = ScriptableObject.CreateInstance<ConsumableData>();
        _created.Add(testItem);
        testItem.id = "test_enhancement_consumable";
        testItem.effectType = ConsumableEffectType.ApplyEnhancement;
        testItem.enhancementToApply = _sharpened;
        testItem.dropAvailable = true;
        testItem.dropChance = 1f;
        _cards.consumableCatalog.Clear();
        _cards.consumableCatalog.Add(testItem);
        typeof(RunManager).GetField("_randomContext", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).SetValue(_run, new RunRandomContext("NO-ENHANCEMENT-DROPS"));
        var node = new PathNode { id = 44, mapIndex = 0, kind = MapNodeType.Combat };
        var method = typeof(RunManager).GetMethod("TryGrantConsumableDropForNode",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.That((bool)method.Invoke(_run, new object[] { node }), Is.False);
        Assert.That(_cards.BackpackSlotsUsed, Is.Zero);
    }

    [Test]
    public void BlacksmithEvent_CanGrantWhetstoneAndRespectsBackpackCapacity()
    {
        var choice = _blacksmith.choices.Single(value => value.label == "Take a Whetstone");
        Assert.That(choice.effects.Single().type, Is.EqualTo(RunEffectType.GainConsumable));
        Assert.That(choice.effects.Single().consumableId, Is.EqualTo(_whetstone.id));
        Assert.That(RunEffectResolver.GetFailureReason(choice, _cards, _combat), Is.Empty);
        Assert.That(RunEffectResolver.Apply(choice, _cards, _combat), Is.True);
        Assert.That(_cards.GetConsumableAtSlot(0), Is.SameAs(_whetstone));

        Assert.That(_cards.AddConsumable(_knife, CardManager.BACKPACK_CAPACITY - 1), Is.True);
        Assert.That(RunEffectResolver.GetFailureReason(choice, _cards, _combat), Is.EqualTo("Backpack Full"));
        Assert.That(RunEffectResolver.Apply(choice, _cards, _combat), Is.False);
    }

    [Test]
    public void EnhancementConsumables_AreAvailableFromShopAndEventButNeverEnemyDrops()
    {
        var all = AssetDatabase.FindAssets("t:ConsumableData", new[] { "Assets/Data/Consumables" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<ConsumableData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(item => item != null && item.effectType == ConsumableEffectType.ApplyEnhancement).ToArray();
        var expected = new[] { "con_003", "con_006", "con_007", "con_008", "con_009", "con_010", "con_011", "con_012", "con_013", "con_014", "con_015" };
        CollectionAssert.AreEquivalent(expected, all.Select(item => item.canonicalId));
        foreach (var item in all)
        {
            Assert.That(item.shopAvailable, Is.True, item.displayName);
            Assert.That(item.eventAvailable, Is.True, item.displayName);
            Assert.That(item.dropAvailable, Is.False, item.displayName);
            Assert.That(item.dropChance, Is.Zero, item.displayName);
        }
    }

    [Test]
    public void ShopOffersOneRandomConsumableFromCatalogPerVisit()
    {
        var all = AssetDatabase.FindAssets("t:ConsumableData", new[] { "Assets/Data/Consumables" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<ConsumableData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(item => item != null).ToArray();
        _cards.consumableCatalog.Clear();
        _cards.consumableCatalog.AddRange(all);
        _run.thiefType = Enemy("Shop Drop Thief", 10, 1, 1);
        _run.goblinType = Enemy("Shop Drop Goblin", 10, 1, 1);
        _run.knightType = Enemy("Shop Drop Knight", 10, 1, 1);
        _run.StartRunWithSeed("ENHANCEMENT-CONSUMABLE-SHOP");
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        _run.OnPathChosen(shop.id);

        var offers = _run.GetCurrentShopOffers().Where(offer => offer.kind == ShopOfferKind.Consumable).ToArray();
        Assert.That(offers, Has.Length.EqualTo(1));
        Assert.That(offers[0].consumable, Is.Not.Null);
        Assert.That(offers[0].consumable.shopAvailable, Is.True);
        Assert.That(all, Does.Contain(offers[0].consumable));
    }

    [Test]
    public void BombCanBeGrantedByAnEventConsumableEffect()
    {
        var choice = new RunEventChoice
        {
            effects = new[] { new RunEffect { type = RunEffectType.GainConsumable, amount = 1, consumableId = _bomb.id } }
        };

        Assert.That(RunEffectResolver.GetFailureReason(choice, _cards, _combat), Is.Empty);
        Assert.That(RunEffectResolver.Apply(choice, _cards, _combat), Is.True);
        Assert.That(_cards.GetConsumableAtSlot(0), Is.SameAs(_bomb));
    }

    [Test]
    public void WhetstoneAsset_ReferencesExistingSharpenedAndSupportsEventSource()
    {
        Assert.That(_knife.canonicalId, Is.EqualTo("con_002"));
        Assert.That(_knife.effectType, Is.EqualTo(ConsumableEffectType.DirectEnemyDamage));
        Assert.That(_knife.damageAmount, Is.EqualTo(5));
        Assert.That(_whetstone.canonicalId, Is.EqualTo("con_003"));
        Assert.That(_whetstone.effectType, Is.EqualTo(ConsumableEffectType.ApplyEnhancement));
        Assert.That(_whetstone.enhancementToApply, Is.SameAs(_sharpened));
        Assert.That(_whetstone.eventAvailable, Is.True);
        Assert.That(_whetstone.dropAvailable, Is.False);
        Assert.That(_bomb.canonicalId, Is.EqualTo("con_006"));
        Assert.That(_bomb.effectType, Is.EqualTo(ConsumableEffectType.ApplyEnhancement));
        Assert.That(_bomb.enhancementToApply, Is.SameAs(AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Enhancements/Explosive.asset")));
        Assert.That(_bomb.shopAvailable, Is.True);
        Assert.That(_bomb.dropAvailable, Is.False);
        Assert.That(_bomb.eventAvailable, Is.True);
    }

    EnemyTypeData Enemy(string name, int hp, int attack, int gold)
    {
        var type = EnemyTypeData.Create(name, hp, attack, gold);
        _created.Add(type);
        return type;
    }

    T Make<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        _created.Add(gameObject);
        return gameObject.AddComponent<T>();
    }
}
