using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase6RegressionTests
{
    readonly List<ScriptableObject> _assets = new();
    readonly List<GameObject> _objects = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    RelicData _artifact;
    CardEnhancementData _enhancement;

    [SetUp]
    public void SetUp()
    {
        _artifact = CreateArtifact("phase6_artifact", 10);
        _enhancement = CreateEnhancement("phase6_enhancement", 8, attack: 3);

        _cards = CreateObject<CardManager>("Phase6 CardManager");
        _cards.Configure(null, null, null, new[] { _artifact }, new[] { _enhancement });
        _combat = CreateObject<CombatManager>("Phase6 CombatManager");
        _combat.ConfigurePlayer(30);
        _run = CreateObject<RunManager>("Phase6 RunManager");
        _run.thiefType = CreateEnemy("Thief", 10, 4, 5);
        _run.goblinType = CreateEnemy("Goblin", 15, 6, 7);
        _run.knightType = CreateEnemy("Knight", 25, 8, 10);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
        _objects.Clear();

        foreach (var asset in _assets)
            if (asset != null) Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    [Test]
    public void CachedArtifactOffer_ChargesDisplayedPriceEvenIfDefinitionChanges()
    {
        _run.StartRunWithSeed("PHASE6-CACHED-PRICE");
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        _run.OnPathChosen(shop.id);
        var offer = _run.GetCurrentShopOffers().Single(value => value.kind == ShopOfferKind.Artifact);
        int cachedPrice = offer.price;
        offer.artifact.price = cachedPrice + 100;
        _cards.AddGold(cachedPrice);

        Assert.That(_run.PurchaseShopOffer(offer.StableId), Is.True);
        Assert.That(_cards.gold, Is.Zero);
        Assert.That(_cards.HasArtifact(offer.artifact), Is.True);
    }

    [Test]
    public void NegativePrices_CannotIncreaseGoldOrGrantArtifacts()
    {
        _cards.AddGold(5);
        _artifact.price = -10;

        Assert.That(_cards.SpendGold(-1), Is.False);
        Assert.That(_cards.BuyArtifact(_artifact), Is.False);
        Assert.That(_cards.gold, Is.EqualTo(5));
        Assert.That(_cards.ownedArtifacts, Is.Empty);
    }

    [Test]
    public void ArtifactAttackStacking_IsOrderIndependentAndUsesMultiplierThenFlatBonus()
    {
        _run.StartRunWithSeed("PHASE6-STACKING");
        var card = _cards.ownedCards.First(value => value.Suit == CardData.Suit.Clubs && value.Rank == CardData.Rank.Four);
        Assert.That(_cards.ApplyEnhancement(card.Id, _enhancement), Is.True);

        var club = CreateArtifact("club", 0);
        club.restrictToSuit = true;
        club.affectedSuit = CardData.Suit.Clubs;
        club.damageMultiplier = 2;
        var artisan = CreateArtifact("artisan", 0);
        artisan.requiresEnhancedCard = true;
        artisan.flatDamageBonus = 3;

        _cards.ownedArtifacts.Clear();
        _cards.ownedArtifacts.Add(club);
        _cards.ownedArtifacts.Add(artisan);
        int firstOrder = _combat.CalculateCardAttackDamage(card);

        _cards.ownedArtifacts.Clear();
        _cards.ownedArtifacts.Add(artisan);
        _cards.ownedArtifacts.Add(club);
        int secondOrder = _combat.CalculateCardAttackDamage(card);

        Assert.That(card.AttackValue, Is.EqualTo(7));
        Assert.That(firstOrder, Is.EqualTo(17));
        Assert.That(secondOrder, Is.EqualTo(firstOrder));
    }

    [Test]
    public void RunEffectValidation_UsesAuthoredOrderAndExplainsUnavailableCosts()
    {
        var fundedByChoice = new RunEventChoice
        {
            effects = new[]
            {
                new RunEffect { type = RunEffectType.GainGold, amount = 5 },
                new RunEffect { type = RunEffectType.LoseGold, amount = 5 }
            }
        };
        var maxHealthBeforeCost = new RunEventChoice
        {
            effects = new[]
            {
                new RunEffect { type = RunEffectType.GainMaxHealth, amount = 5 },
                new RunEffect { type = RunEffectType.LoseHealth, amount = 32 }
            }
        };
        var lethal = new RunEventChoice
        {
            effects = new[] { new RunEffect { type = RunEffectType.LoseHealth, amount = 30 } }
        };

        Assert.That(RunEffectResolver.CanApply(fundedByChoice, _cards, _combat), Is.True);
        Assert.That(RunEffectResolver.CanApply(maxHealthBeforeCost, _cards, _combat), Is.True);
        Assert.That(RunEffectResolver.GetFailureReason(lethal, _cards, _combat), Does.Contain("more than 30 HP"));
    }

    [Test]
    public void UpgradeWithNoEligibleEnhancements_AutoCompletesAndUnlocksSuccessors()
    {
        _cards.Configure(null, null, null, new[] { _artifact }, System.Array.Empty<CardEnhancementData>());
        _run.StartRunWithSeed("PHASE6-EMPTY-UPGRADE");
        var upgrade = _run.currentPath.First(node => node.kind == MapNodeType.Upgrade);
        upgrade.accessible = true;
        int showUpgradeCount = 0;
        int showPathCount = 0;
        _run.OnShowUpgrade += () => showUpgradeCount++;
        _run.OnShowPathScreen += () => showPathCount++;

        _run.OnPathChosen(upgrade.id);

        Assert.That(showUpgradeCount, Is.Zero);
        Assert.That(showPathCount, Is.EqualTo(1));
        Assert.That(upgrade.completed, Is.True);
        Assert.That(_run.ActiveNode, Is.Null);
        Assert.That(upgrade.next.Select(id => _run.currentPath.First(node => node.id == id)).All(node => node.accessible), Is.True);
    }

    [Test]
    public void EmptyShop_RemainsCompletableExactlyOnce()
    {
        _cards.Configure(null, null, null, System.Array.Empty<RelicData>(), System.Array.Empty<CardEnhancementData>());
        _run.StartRunWithSeed("PHASE6-EMPTY-SHOP");
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        int showPathCount = 0;
        _run.OnShowPathScreen += () => showPathCount++;
        _run.OnPathChosen(shop.id);

        Assert.That(_run.GetCurrentShopOffers(), Is.Empty);
        _run.OnShopDone();
        _run.OnShopDone();

        Assert.That(shop.completed, Is.True);
        Assert.That(_run.ActiveNode, Is.Null);
        Assert.That(showPathCount, Is.EqualTo(1));
    }

    [Test]
    public void RunStart_ClosesResultAndDeckOverlays()
    {
        var resultHost = new GameObject("Phase6 Result Host");
        resultHost.SetActive(false);
        _objects.Add(resultHost);
        var resultPanel = new GameObject("Result Panel");
        resultPanel.transform.SetParent(resultHost.transform);
        var rewardBanner = new GameObject("Reward Banner");
        rewardBanner.transform.SetParent(resultHost.transform);
        var resultUi = resultHost.AddComponent<RunResultUI>();
        resultUi.resultPanel = resultPanel;
        resultUi.rewardBanner = rewardBanner;
        InvokeLifecycle(resultUi, "OnEnable");

        var deckHost = new GameObject("Phase6 Deck Host");
        deckHost.SetActive(false);
        _objects.Add(deckHost);
        var deckPanel = new GameObject("Deck Panel");
        deckPanel.transform.SetParent(deckHost.transform);
        var deckUi = deckHost.AddComponent<DeckViewerUI>();
        deckUi.panel = deckPanel;
        InvokeLifecycle(deckUi, "OnEnable");

        resultPanel.SetActive(true);
        rewardBanner.SetActive(true);
        deckPanel.SetActive(true);
        _run.StartRunWithSeed("PHASE6-OVERLAY-RESET");

        Assert.That(resultPanel.activeSelf, Is.False);
        Assert.That(rewardBanner.activeSelf, Is.False);
        Assert.That(deckPanel.activeSelf, Is.False);

        deckPanel.SetActive(true);
        var combatNode = _run.currentPath.First(node => node.kind == MapNodeType.Combat && node.accessible);
        _run.OnPathChosen(combatNode.id);
        Assert.That(deckPanel.activeSelf, Is.False);

        InvokeLifecycle(resultUi, "OnDisable");
        InvokeLifecycle(deckUi, "OnDisable");
    }

    static void InvokeLifecycle(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(target, null);
    }

    T CreateObject<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        _objects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    RelicData CreateArtifact(string id, int price)
    {
        var artifact = ScriptableObject.CreateInstance<RelicData>();
        artifact.id = id;
        artifact.displayName = id;
        artifact.price = price;
        artifact.damageMultiplier = 1;
        _assets.Add(artifact);
        return artifact;
    }

    CardEnhancementData CreateEnhancement(string id, int price, int attack = 0)
    {
        var enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
        enhancement.id = id;
        enhancement.displayName = id;
        enhancement.price = price;
        enhancement.attackBonus = attack;
        _assets.Add(enhancement);
        return enhancement;
    }

    EnemyTypeData CreateEnemy(string name, int hp, int attack, int gold)
    {
        var enemy = EnemyTypeData.Create(name, hp, attack, gold);
        enemy.name = name;
        _assets.Add(enemy);
        return enemy;
    }
}
