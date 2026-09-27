using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BreadConsumableTests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    ConsumableData _bread;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _bread = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Consumables/Bread.asset");
        Assert.That(_bread, Is.Not.Null);
        _cards = Make<CardManager>("Bread Test Cards");
        _cards.Configure(null, null, null, System.Array.Empty<RelicData>(),
            System.Array.Empty<CardEnhancementData>(), new[] { _bread });
        _combat = Make<CombatManager>("Bread Test Combat");
        _combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Bread Test Run");
        _run.thiefType = Enemy("Thief");
        _run.goblinType = Enemy("Goblin");
        _run.knightType = Enemy("Knight");
        _run.StartRunWithSeed("BREAD-CONSUMABLE-TEST");
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
    public void BreadAsset_UsesCanonicalOneChargeFiveHpHealingAndShopDropSources()
    {
        Assert.That(_bread.id, Is.EqualTo("bread"));
        Assert.That(_bread.canonicalId, Is.EqualTo("con_001"));
        Assert.That(_bread.healAmount, Is.EqualTo(5));
        Assert.That(_bread.uses, Is.EqualTo(1));
        Assert.That(_bread.price, Is.EqualTo(5));
        Assert.That(_bread.shopAvailable, Is.True);
        Assert.That(_bread.dropAvailable, Is.True);
        Assert.That(_bread.dropChance, Is.EqualTo(0.1f).Within(0.0001f));
    }

    [Test]
    public void BackpackHasThreeIndividualSlotsAndDoesNotStackDuplicates()
    {
        Assert.That(_cards.BackpackSlotsUsed, Is.Zero);
        Assert.That(_cards.AddConsumable(_bread, 2), Is.True);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(2));
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(2));
        Assert.That(_cards.GetConsumableAtSlot(0), Is.SameAs(_bread));
        Assert.That(_cards.GetConsumableAtSlot(1), Is.SameAs(_bread));

        Assert.That(_cards.AddConsumable(_bread), Is.True);
        Assert.That(_cards.IsBackpackFull, Is.True);
        Assert.That(_cards.AddConsumable(_bread), Is.False);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(CardManager.BACKPACK_CAPACITY));
    }

    [Test]
    public void FullBackpack_RejectsShopPurchaseWithoutChargingGold()
    {
        Assert.That(_cards.AddConsumable(_bread, CardManager.BACKPACK_CAPACITY), Is.True);
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        _cards.AddGold(10);
        _run.OnPathChosen(shop.id);
        var offer = _run.GetCurrentShopOffers().First(value => value.kind == ShopOfferKind.Consumable);

        Assert.That(_run.GetShopOfferUnavailableReason(offer.StableId), Is.EqualTo("Backpack Full"));
        Assert.That(_run.PurchaseShopOffer(offer.StableId), Is.False);
        Assert.That(_cards.gold, Is.EqualTo(10));
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(CardManager.BACKPACK_CAPACITY));
    }

    [Test]
    public void FullBackpack_DoesNotStackOrAcceptEncounterDrop()
    {
        Assert.That(_cards.AddConsumable(_bread, CardManager.BACKPACK_CAPACITY), Is.True);
        const int nodeId = 23;
        const int mapIndex = 1;
        int context = unchecked(mapIndex * 7919 ^ nodeId);
        string seed = Enumerable.Range(0, 1000)
            .Select(index => $"BREAD-FULL-DROP-{index}")
            .First(candidate => new RunRandomContext(candidate)
                .CreateStream("consumable-drops", context).NextFloat() < _bread.dropChance);
        _run.StartRunWithSeed(seed);
        Assert.That(_cards.AddConsumable(_bread, CardManager.BACKPACK_CAPACITY), Is.True);
        var node = new PathNode { id = nodeId, mapIndex = mapIndex, kind = MapNodeType.Combat };
        var method = typeof(RunManager).GetMethod("TryGrantConsumableDropForNode",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.That((bool)method.Invoke(_run, new object[] { node }), Is.False);
        Assert.That(_cards.BackpackSlotsUsed, Is.EqualTo(CardManager.BACKPACK_CAPACITY));
        Assert.That((bool)method.Invoke(_run, new object[] { node }), Is.False, "A resolved drop is not rolled twice for the same node.");
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(CardManager.BACKPACK_CAPACITY));
    }

    [Test]
    public void UseBread_HealsUpToMaxAndConsumesOneOnlyAfterAnEffectiveUse()
    {
        Assert.That(_cards.AddConsumable(_bread), Is.True);
        Assert.That(_combat.TakeRunDamage(3), Is.EqualTo(3));
        Assert.That(_cards.CanUseConsumable(_bread, _combat), Is.True);

        Assert.That(_cards.UseConsumable(_bread, _combat), Is.True);

        Assert.That(_combat.player.currentHealth, Is.EqualTo(_combat.player.maxHealth));
        Assert.That(_cards.GetConsumableCount(_bread), Is.Zero);
        Assert.That(_cards.CanUseConsumable(_bread, _combat), Is.False);
        Assert.That(_cards.UseConsumable(_bread, _combat), Is.False);
    }

    [Test]
    public void BreadAtFullHealth_IsUnavailableAndNotConsumed()
    {
        Assert.That(_cards.AddConsumable(_bread), Is.True);

        Assert.That(_cards.CanUseConsumable(_bread, _combat), Is.False);
        Assert.That(_cards.UseConsumable(_bread, _combat), Is.False);
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(1));
    }

    [Test]
    public void ShopCanSellBread_AtAuthoredPriceAndOnlyOncePerVisit()
    {
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        _cards.AddGold(10);
        _run.OnPathChosen(shop.id);
        var offer = _run.GetCurrentShopOffers().First(value => value.kind == ShopOfferKind.Consumable);

        Assert.That(offer.consumable, Is.SameAs(_bread));
        Assert.That(offer.price, Is.EqualTo(5));
        Assert.That(_run.PurchaseShopOffer(offer.StableId), Is.True);
        Assert.That(_cards.gold, Is.EqualTo(5));
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(1));
        Assert.That(_run.PurchaseShopOffer(offer.StableId), Is.False);
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(1));
    }

    [Test]
    public void BreadDrop_UsesSeededChanceAndCannotResolveTwiceForOneNode()
    {
        const int nodeId = 17;
        const int mapIndex = 2;
        int context = unchecked(mapIndex * 7919 ^ nodeId);
        string seed = Enumerable.Range(0, 1000)
            .Select(index => $"BREAD-DROP-{index}")
            .First(candidate => new RunRandomContext(candidate)
                .CreateStream("consumable-drops", context).NextFloat() < _bread.dropChance);
        _run.StartRunWithSeed(seed);
        var node = new PathNode { id = nodeId, mapIndex = mapIndex, kind = MapNodeType.Combat };
        var method = typeof(RunManager).GetMethod("TryGrantConsumableDropForNode",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        Assert.That((bool)method.Invoke(_run, new object[] { node }), Is.True);
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(1));
        Assert.That((bool)method.Invoke(_run, new object[] { node }), Is.False);
        Assert.That(_cards.GetConsumableCount(_bread), Is.EqualTo(1));
    }

    EnemyTypeData Enemy(string name)
    {
        var enemy = EnemyTypeData.Create(name, 10, 2, 2);
        _created.Add(enemy);
        return enemy;
    }

    T Make<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        _created.Add(gameObject);
        return gameObject.AddComponent<T>();
    }
}
