using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase5BuildProgressionTests
{
    readonly List<ScriptableObject> _assets = new();
    GameObject _cardObject;
    GameObject _combatObject;
    GameObject _runObject;
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    RelicData[] _artifacts;
    CardEnhancementData[] _enhancements;

    [SetUp]
    public void SetUp()
    {
        _artifacts = new[]
        {
            CreateArtifact("clubs", 10, artifact =>
            {
                artifact.restrictToSuit = true;
                artifact.affectedSuit = CardData.Suit.Clubs;
                artifact.damageMultiplier = 2;
            }),
            CreateArtifact("artisan", 12, artifact =>
            {
                artifact.requiresEnhancedCard = true;
                artifact.flatDamageBonus = 3;
            }),
            CreateArtifact("victory", 14, artifact => artifact.healAfterVictory = 2)
        };
        _enhancements = new[]
        {
            CreateEnhancement("sharp", 5, attack: 3),
            CreateEnhancement("guard", 6, defense: 3),
            CreateEnhancement("mend", 7, heal: 2)
        };

        _cardObject = new GameObject("Phase5 CardManager");
        _cards = _cardObject.AddComponent<CardManager>();
        _cards.Configure(null, null, null, _artifacts, _enhancements);

        _combatObject = new GameObject("Phase5 CombatManager");
        _combat = _combatObject.AddComponent<CombatManager>();
        _combat.ConfigurePlayer(30);

        _runObject = new GameObject("Phase5 RunManager");
        _run = _runObject.AddComponent<RunManager>();
        _run.thiefType = CreateEnemy("Thief", 10, 4, 5);
        _run.goblinType = CreateEnemy("Goblin", 15, 6, 7);
        _run.knightType = CreateEnemy("Knight", 25, 8, 10);
    }

    [TearDown]
    public void TearDown()
    {
        if (_runObject != null) Object.DestroyImmediate(_runObject);
        if (_combatObject != null) Object.DestroyImmediate(_combatObject);
        if (_cardObject != null) Object.DestroyImmediate(_cardObject);
        foreach (var asset in _assets)
            if (asset != null) Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    [Test]
    public void CardEnhancement_MutatesOnlyTargetInstanceAndPreservesIdentity()
    {
        var definition = CreateCard(CardData.Suit.Clubs, CardData.Rank.Five);
        var first = new CardInstance(definition, 11);
        var second = new CardInstance(definition, 12);

        Assert.That(first.TryApplyEnhancement(_enhancements[0]), Is.True);
        Assert.That(first.Id, Is.EqualTo(11));
        Assert.That(first.AttackValue, Is.EqualTo(8));
        Assert.That(second.AttackValue, Is.EqualTo(5));
        Assert.That(second.Enhancement, Is.Null);
        Assert.That(first.TryApplyEnhancement(_enhancements[1]), Is.False);
    }

    [Test]
    public void ArtifactHooks_AreAuthoredAndRespectSuitAndEnhancementFilters()
    {
        var club = new CardInstance(CreateCard(CardData.Suit.Clubs, CardData.Rank.Four), 1);
        var heart = new CardInstance(CreateCard(CardData.Suit.Hearts, CardData.Rank.Four), 2);

        Assert.That(_artifacts[0].ModifyAttack(club, club.AttackValue), Is.EqualTo(8));
        Assert.That(_artifacts[0].ModifyAttack(heart, heart.AttackValue), Is.EqualTo(4));
        Assert.That(_artifacts[1].ModifyAttack(club, club.AttackValue), Is.EqualTo(4));

        Assert.That(club.TryApplyEnhancement(_enhancements[1]), Is.True);
        Assert.That(_artifacts[1].ModifyAttack(club, club.AttackValue), Is.EqualTo(7));
        Assert.That(_artifacts[1].ModifyDefense(club, club.DefenseValue), Is.EqualTo(7));
    }

    [Test]
    public void MixedShopOffers_AreDeterministicAndIndependentFromCardStreamConsumption()
    {
        _run.StartRunWithSeed("PHASE5-SHOP");
        var firstShop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        firstShop.accessible = true;
        _run.OnPathChosen(firstShop.id);
        string first = string.Join(",", _run.GetCurrentShopOffers().Select(offer => offer.StableId));

        _run.RestartRun();
        for (int i = 0; i < 8; i++) _cards.ShuffleDeck();
        var repeatedShop = _run.currentPath.First(node => node.id == firstShop.id);
        repeatedShop.accessible = true;
        _run.OnPathChosen(repeatedShop.id);
        string repeated = string.Join(",", _run.GetCurrentShopOffers().Select(offer => offer.StableId));

        Assert.That(first, Is.Not.Empty);
        Assert.That(repeated, Is.EqualTo(first));
        Assert.That(_run.GetCurrentShopOffers().Any(offer => offer.kind == ShopOfferKind.Artifact), Is.True);
        Assert.That(_run.GetCurrentShopOffers().Any(offer => offer.kind == ShopOfferKind.Enhancement), Is.True);
    }

    [Test]
    public void ShopPurchases_DeductOnceAndRejectDuplicateOffers()
    {
        _run.StartRunWithSeed("PHASE5-BUY");
        _cards.AddGold(100);
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        _run.OnPathChosen(shop.id);

        var artifactOffer = _run.GetCurrentShopOffers().First(offer => offer.kind == ShopOfferKind.Artifact);
        int beforeArtifact = _cards.gold;
        Assert.That(_run.PurchaseShopOffer(artifactOffer.StableId), Is.True);
        Assert.That(_cards.gold, Is.EqualTo(beforeArtifact - artifactOffer.price));
        Assert.That(_cards.HasArtifact(artifactOffer.artifact), Is.True);
        Assert.That(_run.PurchaseShopOffer(artifactOffer.StableId), Is.False);

        var enhancementOffer = _run.GetCurrentShopOffers().First(offer => offer.kind == ShopOfferKind.Enhancement);
        int beforeEnhancement = _cards.gold;
        var chosenCard = _cards.ownedCards.First(card => card.Enhancement == null);
        Assert.That(_run.PurchaseShopOffer(enhancementOffer.StableId), Is.False, "A target is required");
        Assert.That(_run.PurchaseShopEnhancement(enhancementOffer.StableId, chosenCard.Id), Is.True);
        Assert.That(_cards.gold, Is.EqualTo(beforeEnhancement - enhancementOffer.price));
        Assert.That(_cards.FindOwnedCard(chosenCard.Id).Enhancement, Is.SameAs(enhancementOffer.enhancement));
        Assert.That(_run.PurchaseShopEnhancement(enhancementOffer.StableId, chosenCard.Id), Is.False);
    }

    [Test]
    public void UpgradeChoice_AppliesExactlyOnceAndCompletesNode()
    {
        _run.StartRunWithSeed("PHASE5-UPGRADE");
        var upgrade = _run.currentPath.First(node => node.kind == MapNodeType.Upgrade);
        upgrade.accessible = true;
        _run.OnPathChosen(upgrade.id);
        var offer = _run.GetCurrentUpgradeOffers()[0];

        var chosenCard = _cards.ownedCards.First(card => card.Enhancement == null);
        Assert.That(_run.ChooseUpgradeOffer(0), Is.False, "A target is required");
        Assert.That(_run.ChooseUpgradeOffer(0, chosenCard.Id), Is.True);
        Assert.That(_cards.FindOwnedCard(chosenCard.Id).Enhancement, Is.SameAs(offer.enhancement));
        Assert.That(upgrade.completed, Is.True);
        Assert.That(_run.ActiveNode, Is.Null);
        Assert.That(_run.ChooseUpgradeOffer(0, chosenCard.Id), Is.False);
    }

    [Test]
    public void Restart_ClearsArtifactsEnhancementsAndEconomyWhileConservingBaselineCards()
    {
        _run.StartRunWithSeed("PHASE5-RESET");
        _cards.AddGold(100);
        Assert.That(_cards.BuyArtifact(_artifacts[0]), Is.True);
        Assert.That(_cards.ApplyEnhancement(_cards.ownedCards[0].Id, _enhancements[0]), Is.True);

        _run.RestartRun();

        Assert.That(_cards.gold, Is.Zero);
        Assert.That(_cards.ownedArtifacts, Is.Empty);
        Assert.That(_cards.relics, Is.Empty);
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(40));
        Assert.That(_cards.ownedCards.All(card => card.Enhancement == null), Is.True);
        Assert.That(_cards.ownedCards.Select(card => card.Id).Distinct().Count(), Is.EqualTo(40));
        Assert.That(_cards.deck.Count + _cards.hand.Count + _cards.discardPile.Count, Is.EqualTo(40));
    }

    RelicData CreateArtifact(string id, int price, System.Action<RelicData> configure)
    {
        var artifact = ScriptableObject.CreateInstance<RelicData>();
        artifact.id = id;
        artifact.displayName = id;
        artifact.price = price;
        artifact.damageMultiplier = 1;
        configure?.Invoke(artifact);
        _assets.Add(artifact);
        return artifact;
    }

    CardEnhancementData CreateEnhancement(string id, int price, int attack = 0, int defense = 0, int heal = 0)
    {
        var enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
        enhancement.id = id;
        enhancement.displayName = id;
        enhancement.price = price;
        enhancement.attackBonus = attack;
        enhancement.defenseBonus = defense;
        enhancement.healOnPlay = heal;
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

    CardData CreateCard(CardData.Suit suit, CardData.Rank rank)
    {
        var card = CardData.Create(suit, rank);
        _assets.Add(card);
        return card;
    }
}
