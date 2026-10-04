using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase7EArtifactCapacityTests
{
    readonly List<Object> _assets = new();
    readonly List<GameObject> _objects = new();
    CardManager _cards;
    RunManager _run;
    RelicData[] _artifacts;
    CardEnhancementData _enhancement;

    [SetUp]
    public void SetUp()
    {
        _artifacts = Enumerable.Range(1, 7).Select(i => Artifact($"artifact_{i}", 10 + i)).ToArray();
        _enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
        _enhancement.id = "sharpened";
        _enhancement.price = 9;
        _assets.Add(_enhancement);
        _cards = Make<CardManager>("Cards");
        _cards.Configure(null, null, null, _artifacts, new[] { _enhancement });
        var combat = Make<CombatManager>("Combat");
        combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Run");
        _run.thiefType = Enemy("Thief");
        _run.goblinType = Enemy("Goblin");
        _run.knightType = Enemy("Knight");
        _run.StartRunWithSeed("7E-CAPACITY");
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i]) Object.DestroyImmediate(_objects[i]);
        foreach (var asset in _assets)
            if (asset) Object.DestroyImmediate(asset);
        _objects.Clear();
        _assets.Clear();
    }

    [Test]
    public void BelowFive_FifthSucceeds_SixthFailsWithoutMutationOrGoldLoss()
    {
        Assert.That(_cards.ArtifactCapacity, Is.EqualTo(5));
        _cards.AddGold(100);
        for (int i = 0; i < 5; i++)
        {
            Assert.That(_cards.BuyArtifact(_artifacts[i], 1), Is.True);
            Assert.That(_cards.ArtifactSlotsUsed, Is.EqualTo(i + 1));
        }
        int gold = _cards.gold;
        Assert.That(_cards.ownedArtifacts, Has.Count.EqualTo(5));
        Assert.That(_cards.GetArtifactAcquisitionUnavailableReason(_artifacts[5]), Is.EqualTo("Artifact Capacity Full"));
        Assert.That(_cards.BuyArtifact(_artifacts[5], 1), Is.False);
        Assert.That(_cards.BuyRelic(_artifacts[6]), Is.False, "Legacy non-Shop grant path shares the limit");
        Assert.That(_cards.gold, Is.EqualTo(gold));
        Assert.That(_cards.ownedArtifacts, Has.Count.EqualTo(5));
        Assert.That(_cards.relics, Has.Count.EqualTo(5));
        Assert.That(_cards.HasArtifact(_artifacts[5]), Is.False);
    }

    [Test]
    public void DuplicateRuleStillWinsAtCapacity_AndInvalidPriceDoesNotGrant()
    {
        for (int i = 0; i < 5; i++) Assert.That(_cards.BuyArtifact(_artifacts[i], 0), Is.True);
        Assert.That(_cards.GetArtifactAcquisitionUnavailableReason(_artifacts[0]), Is.EqualTo("Already owned"));
        Assert.That(_cards.BuyArtifact(_artifacts[0], 0), Is.False);
        Assert.That(_cards.BuyArtifact(_artifacts[5], -1), Is.False);
        Assert.That(_cards.ownedArtifacts, Has.Count.EqualTo(5));
        Assert.That(_cards.gold, Is.Zero);
    }

    [Test]
    public void NonSlotArtifact_IsOwnedButDoesNotConsumeCapacity_AndDuplicateStillRejected()
    {
        for (int i = 0; i < 5; i++) Assert.That(_cards.BuyArtifact(_artifacts[i], 0), Is.True);
        var contract = Artifact("contract", 0);
        contract.capacityCategory = ArtifactCapacityCategory.NonSlot;
        Assert.That(_cards.GetArtifactAcquisitionUnavailableReason(contract), Is.Empty);
        Assert.That(_cards.BuyArtifact(contract, 0), Is.True);
        Assert.That(_cards.ownedArtifacts, Has.Count.EqualTo(6));
        Assert.That(_cards.ArtifactSlotsUsed, Is.EqualTo(5));
        Assert.That(_cards.BuyArtifact(contract, 0), Is.False);
        Assert.That(_cards.BuyArtifact(_artifacts[5], 0), Is.False);
        Assert.That(_cards.ownedArtifacts, Does.Contain(contract));
    }

    [Test]
    public void FullShop_KeepsCachedOfferAndPrice_BlocksArtifactButAllowsEnhancement()
    {
        _cards.AddGold(100);
        OpenShop();
        var shopArtifacts = _run.GetCurrentShopOffers().Where(o => o.kind == ShopOfferKind.Artifact).ToArray();
        var shopEnhancement = _run.GetCurrentShopOffers().First(o => o.kind == ShopOfferKind.Enhancement);
        Assert.That(shopArtifacts.Length, Is.EqualTo(2));
        foreach (var artifact in _artifacts.Where(a => shopArtifacts.All(o => o.artifact != a)).Take(5))
            Assert.That(_cards.BuyArtifact(artifact, 0), Is.True);
        var offer = shopArtifacts[0];
        string id = offer.StableId;
        int price = offer.price;
        int gold = _cards.gold;
        Assert.That(_cards.ArtifactSlotsUsed, Is.EqualTo(5));
        Assert.That(_run.GetShopOfferUnavailableReason(id), Is.EqualTo("Artifact Capacity Full"));
        Assert.That(_run.CanPurchaseShopOffer(id), Is.False);
        Assert.That(_run.PurchaseShopOffer(id), Is.False);
        Assert.That(_cards.gold, Is.EqualTo(gold));
        Assert.That(_cards.HasArtifact(offer.artifact), Is.False);
        Assert.That(_run.GetCurrentShopOffers(), Does.Contain(offer));
        Assert.That(offer.StableId, Is.EqualTo(id));
        Assert.That(offer.price, Is.EqualTo(price));

        int cardId = _cards.hand.First(card => card.Enhancement == null).Id;
        Assert.That(_run.GetShopOfferUnavailableReason(shopEnhancement.StableId), Is.Empty);
        Assert.That(_run.PurchaseShopEnhancement(shopEnhancement.StableId, cardId), Is.True);
        Assert.That(_cards.gold, Is.EqualTo(gold - shopEnhancement.price));
        Assert.That(_cards.FindOwnedCard(cardId).Enhancement, Is.SameAs(shopEnhancement.enhancement));
        Assert.That(_cards.ArtifactSlotsUsed, Is.EqualTo(5));
    }

    [Test]
    public void ResetRestartAndNewRun_ClearSlotsAndReturnToBaseCapacity()
    {
        for (int i = 0; i < 5; i++) _cards.BuyArtifact(_artifacts[i], 0);
        _run.RestartRun();
        Assert.That(_cards.ArtifactCapacity, Is.EqualTo(5));
        Assert.That(_cards.ArtifactSlotsUsed, Is.Zero);
        Assert.That(_cards.ownedArtifacts, Is.Empty);
        Assert.That(_cards.ownedCards, Has.Count.EqualTo(40));
        Assert.That(_cards.BuyArtifact(_artifacts[0], 0), Is.True);
        _run.StartRunWithSeed("7E-CAPACITY-NEW");
        Assert.That(_cards.ArtifactCapacity, Is.EqualTo(5));
        Assert.That(_cards.ArtifactSlotsUsed, Is.Zero);
        Assert.That(_cards.ownedArtifacts, Is.Empty);
        _cards.BuyArtifact(_artifacts[1], 0);
        _cards.Reset();
        Assert.That(_cards.ArtifactCapacity, Is.EqualTo(5));
        Assert.That(_cards.ArtifactSlotsUsed, Is.Zero);
    }

    void OpenShop()
    {
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        _run.OnPathChosen(shop.id);
    }

    T Make<T>(string name) where T : Component
    {
        var obj = new GameObject(name);
        _objects.Add(obj);
        return obj.AddComponent<T>();
    }

    RelicData Artifact(string id, int price)
    {
        var artifact = ScriptableObject.CreateInstance<RelicData>();
        artifact.id = id;
        artifact.displayName = id;
        artifact.price = price;
        artifact.damageMultiplier = 1;
        _assets.Add(artifact);
        return artifact;
    }

    EnemyTypeData Enemy(string name)
    {
        var enemy = EnemyTypeData.Create(name, 10, 3, 3);
        enemy.name = name;
        _assets.Add(enemy);
        return enemy;
    }
}
