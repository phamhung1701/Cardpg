using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class LowerEffortShopTests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    const string Seed = "LOWER-EFFORT-SHOP-TEST";

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _cards = Make<CardManager>("Shop Feature Cards");
        _cards.Configure(null, null, null, System.Array.Empty<RelicData>());
        _combat = Make<CombatManager>("Shop Feature Combat");
        _combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Shop Feature Run");
        _run.thiefType = Enemy("Thief");
        _run.goblinType = Enemy("Goblin");
        _run.knightType = Enemy("Knight");
        _run.StartRunWithSeed(Seed);
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
    public void ShopOffers_ContainInvestmentAndGuidanceAtFiveGoldEach()
    {
        OpenShop(Shop(1, 0));
        var investment = FindOffer(ShopOfferKind.Investment);
        var guidance = FindOffer(ShopOfferKind.Guidance);
        Assert.That(investment.price, Is.EqualTo(5));
        Assert.That(guidance.price, Is.EqualTo(5));
        Assert.That(investment.Description(_cards), Does.Contain("50%"));
        Assert.That(guidance.Description(_cards), Does.Contain("hidden"));
    }

    [Test]
    public void Investment_ResolvesOnceOnTheNextShopWithSeededFiftyPercentReturn()
    {
        var first = Shop(1, 0, 2);
        var second = Shop(2, 0, 3);
        var third = Shop(3, 0);
        _run.currentPath.Clear();
        _run.currentPath.AddRange(new[] { first, second, third });
        _cards.AddGold(5);
        _run.OnPathChosen(first.id);
        var investment = FindOffer(ShopOfferKind.Investment);

        Assert.That(_run.PurchaseShopOffer(investment.StableId), Is.True);
        Assert.That(_cards.gold, Is.Zero);
        Assert.That(_run.PendingInvestmentCount, Is.EqualTo(1));
        Assert.That(_run.CanPurchaseShopOffer(investment.StableId), Is.False, "Investment is one purchase per shop visit.");
        _run.OnShopDone();
        _run.OnPathChosen(second.id);

        var expectedRandom = new RunRandomContext(Seed).CreateStream("shop-investment", unchecked(second.mapIndex * 7919 ^ second.id));
        int expectedGold = expectedRandom.NextInt(0, 2) == 0 ? 15 : 0;
        Assert.That(_cards.gold, Is.EqualTo(expectedGold));
        Assert.That(_run.PendingInvestmentCount, Is.Zero);
        int resolvedGold = _cards.gold;
        _run.OnShopDone();
        _run.OnPathChosen(third.id);
        Assert.That(_cards.gold, Is.EqualTo(resolvedGold), "A settled investment must not resolve twice.");
        Assert.That(_run.PendingInvestmentCount, Is.Zero);
    }

    [Test]
    public void Guidance_CostsFiveGoldRevealsOneReachableHiddenEventOrRiskAndIsOneTime()
    {
        var shop = Shop(1, 0, 2, 3);
        var farther = new PathNode { id = 2, mapIndex = 0, kind = MapNodeType.Event, hidden = true, col = 2, row = 0 };
        var nearer = new PathNode { id = 3, mapIndex = 0, kind = MapNodeType.Risk, hidden = true, col = 1, row = 0 };
        _run.currentPath.Clear();
        _run.currentPath.AddRange(new[] { shop, farther, nearer });
        _cards.AddGold(5);
        _run.OnPathChosen(shop.id);
        int notifications = 0;
        _run.OnMapRevealChanged += () => notifications++;
        var guidance = FindOffer(ShopOfferKind.Guidance);

        Assert.That(_run.PurchaseShopOffer(guidance.StableId), Is.True);

        Assert.That(_cards.gold, Is.Zero);
        Assert.That(nearer.revealed, Is.True);
        Assert.That(farther.revealed, Is.False);
        Assert.That(notifications, Is.EqualTo(1));
        Assert.That(_run.CanPurchaseShopOffer(guidance.StableId), Is.False);
    }

    [Test]
    public void Guidance_IsUnavailableWithoutReachableEligibleHiddenNode()
    {
        var shop = Shop(1, 0);
        _run.currentPath.Clear();
        _run.currentPath.Add(shop);
        _cards.AddGold(5);
        _run.OnPathChosen(shop.id);
        var guidance = FindOffer(ShopOfferKind.Guidance);

        Assert.That(_run.GetShopOfferUnavailableReason(guidance.StableId), Is.EqualTo("No eligible hidden node to reveal"));
        Assert.That(_run.PurchaseShopOffer(guidance.StableId), Is.False);
        Assert.That(_cards.gold, Is.EqualTo(5));
    }

    void OpenShop(PathNode shop)
    {
        _run.currentPath.Clear();
        _run.currentPath.Add(shop);
        _run.OnPathChosen(shop.id);
    }

    ShopOffer FindOffer(ShopOfferKind kind) =>
        System.Linq.Enumerable.First(_run.GetCurrentShopOffers(), offer => offer.kind == kind);

    PathNode Shop(int id, int mapIndex, params int[] next) => new()
    {
        id = id, mapIndex = mapIndex, kind = MapNodeType.Shop, accessible = true,
        revealed = true, col = id, next = new List<int>(next)
    };

    EnemyTypeData Enemy(string name)
    {
        var enemy = EnemyTypeData.Create(name, 10, 2, 2);
        _created.Add(enemy);
        return enemy;
    }

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _created.Add(go);
        return go.AddComponent<T>();
    }
}
