using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase8QuestOfferTests
{
    GameObject _cardsObject;
    GameObject _combatObject;
    GameObject _runObject;
    CardManager _cards;
    RunManager _run;
    readonly List<RunEventDefinition> _events = new();

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _cardsObject = new GameObject("Phase8 Quest CardManager");
        _cards = _cardsObject.AddComponent<CardManager>();
        _cards.Configure(null, null, null, System.Array.Empty<RelicData>(), System.Array.Empty<CardEnhancementData>());
        _combatObject = new GameObject("Phase8 Quest CombatManager");
        _combatObject.AddComponent<CombatManager>().ConfigurePlayer(30);
        _runObject = new GameObject("Phase8 Quest RunManager");
        _run = _runObject.AddComponent<RunManager>();
        SetPrivate(_run, "_randomContext", new RunRandomContext("phase8-quest-shop"));
        SetPrivate(_run, "runSeed", "phase8-quest-shop");
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        if (_runObject != null) Object.DestroyImmediate(_runObject);
        if (_combatObject != null) Object.DestroyImmediate(_combatObject);
        if (_cardsObject != null) Object.DestroyImmediate(_cardsObject);
        foreach (var item in _events) if (item != null) Object.DestroyImmediate(item);
    }

    [Test]
    public void ShopContractCostsTwentyAndOffersReachableEliteObjective()
    {
        _cards.gold = 50;
        var shop = new PathNode { id = 1, mapIndex = 0, kind = MapNodeType.Shop };
        shop.next.Add(2);
        var elite = new PathNode { id = 2, mapIndex = 0, kind = MapNodeType.Elite };
        _run.currentPath.AddRange(new[] { shop, elite });
        SetPrivate(_run, "_activeNode", shop);
        InvokePrivate(_run, "PrepareShopOffers", 4);
        var contract = System.Linq.Enumerable.First(_run.GetCurrentShopOffers(), offer => offer.kind == ShopOfferKind.Contract);
        Assert.That(contract.price, Is.EqualTo(20));
        Assert.That(_run.GetShopOfferUnavailableReason(contract.StableId), Is.EqualTo("Purchase another Shop item first"));
        var investment = System.Linq.Enumerable.First(_run.GetCurrentShopOffers(), offer => offer.kind == ShopOfferKind.Investment);
        Assert.That(_run.PurchaseShopOffer(investment.StableId), Is.True);
        Assert.That(_cards.gold, Is.EqualTo(45));
        Assert.That(_run.GetShopOfferUnavailableReason(contract.StableId), Is.Empty);

        RunEventDefinition shown = null;
        _run.OnShowEvent += definition => shown = definition;
        Assert.That(_run.PurchaseShopOffer(contract.StableId), Is.True);
        Assert.That(_cards.gold, Is.EqualTo(45), "The fee is not charged while choosing an objective.");
        Assert.That(shown, Is.Not.Null);
        Assert.That(_run.GetEventOptionUnavailableReason(1), Is.Empty);
        Assert.That(_run.ChooseEventOption(1), Is.True);
        Assert.That(_cards.gold, Is.EqualTo(25));
        Assert.That(_run.ActiveContract.objective, Is.EqualTo(RunContractObjective.DefeatEliteThisMap));
        Assert.That(_run.ActiveContract.startedAtNodeId, Is.EqualTo(shop.id));
    }

    [Test]
    public void FailedAndFreeShopPurchasesDoNotUnlockContractOffer()
    {
        _cards.gold = 50;
        var shop = new PathNode { id = 1, mapIndex = 0, kind = MapNodeType.Shop };
        _run.currentPath.Add(shop);
        SetPrivate(_run, "_activeNode", shop);
        InvokePrivate(_run, "PrepareShopOffers", 4);
        var contract = System.Linq.Enumerable.First(_run.GetCurrentShopOffers(), offer => offer.kind == ShopOfferKind.Contract);
        var investment = System.Linq.Enumerable.First(_run.GetCurrentShopOffers(), offer => offer.kind == ShopOfferKind.Investment);
        Assert.That(_run.PurchaseShopOffer("missing-offer"), Is.False);
        investment.price = 0;
        Assert.That(_run.PurchaseShopOffer(investment.StableId), Is.True);
        Assert.That(_cards.gold, Is.EqualTo(50));
        Assert.That(_run.GetShopOfferUnavailableReason(contract.StableId), Is.EqualTo("Purchase another Shop item first"));
    }

    [Test]
    public void ContractSelectionCancelKeepsGoldAndReturnsToCurrentShop()
    {
        _cards.gold = 50;
        var shop = new PathNode { id = 1, mapIndex = 0, kind = MapNodeType.Shop };
        _run.currentPath.Add(shop);
        SetPrivate(_run, "_activeNode", shop);
        InvokePrivate(_run, "PrepareShopOffers", 4);
        var investment = System.Linq.Enumerable.First(_run.GetCurrentShopOffers(), offer => offer.kind == ShopOfferKind.Investment);
        var contract = System.Linq.Enumerable.First(_run.GetCurrentShopOffers(), offer => offer.kind == ShopOfferKind.Contract);
        Assert.That(_run.PurchaseShopOffer(investment.StableId), Is.True);
        Assert.That(_cards.gold, Is.EqualTo(45));

        int showShopCount = 0;
        _run.OnShowEvent += _ => { };
        _run.OnShowShop += () => showShopCount++;
        Assert.That(_run.PurchaseShopOffer(contract.StableId), Is.True);
        Assert.That(_cards.gold, Is.EqualTo(45));
        Assert.That(_run.ChooseEventOption(3), Is.True);
        Assert.That(_cards.gold, Is.EqualTo(45));
        Assert.That(_run.ActiveContract, Is.Null);
        Assert.That(_run.ActiveEvent, Is.Null);
        Assert.That(_run.ActiveNode, Is.SameAs(shop));
        Assert.That(showShopCount, Is.EqualTo(1));
    }

    [Test]
    public void EliteObjectiveIsDisabledWhenNoRemainingEliteIsReachable()
    {
        var shop = new PathNode { id = 1, mapIndex = 0, kind = MapNodeType.Shop };
        _cards.gold = 20;
        _run.currentPath.Add(shop);
        SetPrivate(_run, "_activeNode", shop);
        _run.OnShowEvent += _ => { };
        Assert.That(InvokePrivateResult<bool>(_run, "ShowContractSelection", 20), Is.True);
        Assert.That(_run.CanChooseEventOption(1), Is.False);
        Assert.That(_run.GetEventOptionUnavailableReason(1), Is.EqualTo("No reachable Elite remains on this map"));
    }

    static void SetPrivate(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    static void InvokePrivate(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    static T InvokePrivateResult<T>(object target, string method, params object[] args) =>
        (T)target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}
