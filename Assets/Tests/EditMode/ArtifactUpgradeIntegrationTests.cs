using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ArtifactUpgradeIntegrationTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    RelicData[] _catalog;
    RelicData Load(string id) => _catalog.Single(x => x.canonicalId == id);

    [SetUp]
    public void SetUp()
    {
        var controller = Object.FindFirstObjectByType<GameController>();
        _catalog = controller != null && controller.relicCatalog != null
            ? controller.relicCatalog
            : AssetDatabase.FindAssets("t:RelicData", new[] { "Assets/Data/Relics" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<RelicData>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
        _cards = Make<CardManager>("Artifact Upgrade Cards");
        _cards.Configure(null, null, null, _catalog);
        _cards.BuildDeck();
        _combat = Make<CombatManager>("Artifact Upgrade Combat");
        _combat.ConfigurePlayer(30);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created.AsEnumerable().Reverse()) if (obj) Object.DestroyImmediate(obj);
        _created.Clear();
    }

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _created.Add(go);
        return go.AddComponent<T>();
    }

    [Test]
    public void CatalogContainsEveryApprovedM2TierWithValidParentsAndSpecialRules()
    {
        Assert.That(ArtifactUpgradeResolver.ValidateCatalog(_catalog), Is.Empty);
        var expected = new (string id, string parent, int tier, ArtifactSpecialRule rule)[] {
            ("rel_025","rel_001",2,ArtifactSpecialRule.RareClub),
            ("rel_026","rel_002",2,ArtifactSpecialRule.RareHeart),
            ("rel_027","rel_003",2,ArtifactSpecialRule.RareDiamond),
            ("rel_029","rel_028",2,ArtifactSpecialRule.RareArsenal),
            ("rel_031","",1,ArtifactSpecialRule.GlassCommon),
            ("rel_032","rel_031",2,ArtifactSpecialRule.GlassRare),
            ("rel_033","rel_032",3,ArtifactSpecialRule.GlassEpic),
            ("rel_022","rel_016",2,ArtifactSpecialRule.RareRetaliation)
        };
        foreach (var e in expected)
        {
            var item = Load(e.id);
            Assert.That(item.upgradeFromId, Is.EqualTo(e.parent), e.id);
            Assert.That(item.tier, Is.EqualTo(e.tier), e.id);
            Assert.That(item.specialRule, Is.EqualTo(e.rule), e.id);
            Assert.That(item.OccupiesCapacitySlot, Is.True, e.id);
            Assert.That(item.description, Is.Not.Empty, e.id);
        }
    }

    [Test]
    public void DuplicateArtifactsHaveIndependentRuntimeStateAndEffectsApplyPerCopy()
    {
        var glass = _catalog.Single(item => item != null &&
            item.specialRule == ArtifactSpecialRule.GlassCommon && item.tier == 1);
        Assert.That(_cards.BuyArtifact(glass, 0), Is.True);
        Assert.That(_cards.BuyArtifact(glass, 0), Is.True);
        var first = _cards.GetArtifactInstanceAt(0);
        var second = _cards.GetArtifactInstanceAt(1);
        Assert.That(first.Id, Is.Not.EqualTo(second.Id));
        first.State.SetCounter(91, 7);
        Assert.That(second.State.GetCounter(91), Is.Zero);
        var card = _cards.ownedCards.First(item => item != null);
        Assert.That(GameplayEffectResolver.CalculateAttack(card, _cards),
            Is.EqualTo(card.BaseAttackValue + 10));
        Assert.That(_cards.GetArtifactInstance(glass), Is.Null,
            "Definition-based lookup is ambiguous when duplicate instances are owned.");
    }

    [Test]
    public void ManualMergeConsumesTwoExactCopiesAndCreatesFreshNextTierInstance()
    {
        var common = Load("rel_001");
        var rare = Load("rel_025");
        Assert.That(_cards.BuyArtifact(common, 0), Is.True);
        Assert.That(_cards.BuyArtifact(common, 0), Is.True);
        var first = _cards.GetArtifactInstanceAt(0);
        var second = _cards.GetArtifactInstanceAt(1);
        Assert.That(first.Id, Is.Not.EqualTo(second.Id));
        first.State.SetCounter(4, 10);
        second.State.SetCounter(4, 20);
        int goldBefore = _cards.gold;
        Assert.That(_cards.MergeArtifacts(first.Id, second.Id), Is.True);
        Assert.That(_cards.gold, Is.EqualTo(goldBefore));
        Assert.That(_cards.ownedArtifacts, Is.EqualTo(new[] { rare }));
        Assert.That(_cards.ArtifactSlotsUsed, Is.EqualTo(1));
        Assert.That(_cards.GetArtifactInstanceAt(0).State.GetCounter(4), Is.Zero);
        Assert.That(_cards.GetArtifactInstance(common), Is.Null);
    }

    [Test]
    public void ExactInstanceRemovalPreservesOtherCopyState()
    {
        var glass = _catalog.Single(item => item != null &&
            item.specialRule == ArtifactSpecialRule.GlassCommon && item.tier == 1);
        Assert.That(_cards.BuyArtifact(glass, 0), Is.True);
        Assert.That(_cards.BuyArtifact(glass, 0), Is.True);
        var first = _cards.GetArtifactInstanceAt(0);
        var second = _cards.GetArtifactInstanceAt(1);
        second.State.SetCounter(92, 4);
        Assert.That(_cards.RemoveArtifactInstance(first.Id), Is.True);
        Assert.That(_cards.OwnedArtifactInstances.Count, Is.EqualTo(1));
        Assert.That(_cards.GetArtifactInstanceAt(0).Id, Is.EqualTo(second.Id));
        Assert.That(_cards.GetArtifactInstanceAt(0).State.GetCounter(92), Is.EqualTo(4));
        Assert.That(_cards.GetArtifactInstance(glass), Is.SameAs(second));
    }

    [Test]
    public void ManualMergeRejectsSameInstanceAndLeavesInventoryUnchanged()
    {
        var common = Load("rel_001");
        Assert.That(_cards.BuyArtifact(common, 0), Is.True);
        var instance = _cards.GetArtifactInstanceAt(0);
        Assert.That(_cards.MergeArtifacts(instance.Id, instance.Id), Is.False);
        Assert.That(_cards.ownedArtifacts, Is.EqualTo(new[] { common }));
    }

    [Test]
    public void UpgradeReplacesPredecessorInPlaceWithoutUsingAnotherSlotOrRetainingState()
    {
        var predecessor = Load("rel_001");
        var upgrade = Load("rel_025");
        Assert.That(_cards.GetArtifactAcquisitionUnavailableReason(upgrade), Is.EqualTo("Requires immediate predecessor"));
        Assert.That(_cards.BuyArtifact(predecessor, 0), Is.True);
        var oldRuntime = _cards.GetArtifactInstance(predecessor);
        oldRuntime.State.SetCounter(4, 9);
        int slots = _cards.ArtifactSlotsUsed;
        Assert.That(_cards.BuyArtifact(upgrade, 0), Is.True);
        Assert.That(_cards.ArtifactSlotsUsed, Is.EqualTo(slots));
        Assert.That(_cards.ownedArtifacts.Count(x => x != null && x.canonicalId == "rel_025"), Is.EqualTo(1));
        Assert.That(_cards.ownedArtifacts.Any(x => x != null && x.canonicalId == "rel_001"), Is.False);
        Assert.That(_cards.GetArtifactInstance(upgrade).State.GetCounter(4), Is.Zero);
    }

    [Test]
    public void RareClubDoublesFinalCardAttackWithoutChangingSharedCardDefinition()
    {
        var common = Load("rel_001");
        var rare = Load("rel_025");
        Assert.That(_cards.BuyArtifact(common, 0), Is.True);
        Assert.That(_cards.BuyArtifact(rare, 0), Is.True);
        var card = _cards.ownedCards.Single(x => x.Suit == CardData.Suit.Clubs && x.Rank == CardData.Rank.Seven);
        int baseDamage = card.BaseAttackValue;
        Assert.That(GameplayEffectResolver.CalculateAttack(card, _cards), Is.EqualTo(baseDamage * 2));
        Assert.That(card.Definition.suit, Is.EqualTo(CardData.Suit.Clubs));
    }

    [Test]
    public void GlassTiersReplaceRatherThanStackAndEpicOnlyTriplesAtExactlyOneHp()
    {
        var common = Load("rel_031");
        var rare = Load("rel_032");
        var epic = Load("rel_033");
        var card = _cards.ownedCards.First(x => x.Rank == CardData.Rank.Seven);
        int baseDamage = card.BaseAttackValue;
        Assert.That(_cards.BuyArtifact(common, 0), Is.True);
        Assert.That(GameplayEffectResolver.CalculateAttack(card, _cards), Is.EqualTo(baseDamage + 5));
        Assert.That(_cards.BuyArtifact(rare, 0), Is.True);
        Assert.That(GameplayEffectResolver.CalculateAttack(card, _cards), Is.EqualTo(baseDamage * 2));
        _combat.player.TakeDamage(29);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(1));
        Assert.That(_cards.BuyArtifact(epic, 0), Is.True);
        Assert.That(GameplayEffectResolver.CalculateAttack(card, _cards), Is.EqualTo(baseDamage * 3));
        _combat.player.TakeDamage(1);
        Assert.That(GameplayEffectResolver.CalculateAttack(card, _cards), Is.EqualTo(baseDamage));
    }

    [Test]
    public void ArsenalAndGlassIncomingModifiersAffectOnlyEnemyAttacksAndRoundUpOnce()
    {
        var arsenal = Load("rel_029");
        Assert.That(_cards.BuyArtifact(Load("rel_028"), 0), Is.True);
        Assert.That(_cards.BuyArtifact(arsenal, 0), Is.True);
        var action = new CombatActionContext(1, CombatActionOrigin.EnemyRetaliation);
        var attack = new DamageRequest(action, 0, CombatDamageOrigin.EnemyAggregate, null, _combat.player, 3);
        Assert.That(GameplayEffectResolver.ModifyIncomingCombatDamage(
            new IncomingDamageEffectContext(attack, _combat, _cards, 3)), Is.EqualTo(5));
        var nonAttack = new DamageRequest(action, 0, CombatDamageOrigin.Legacy, null, _combat.player, 3);
        Assert.That(GameplayEffectResolver.ModifyIncomingCombatDamage(
            new IncomingDamageEffectContext(nonAttack, _combat, _cards, 3)), Is.EqualTo(3));
    }

    [Test]
    public void RareHeartChecksHealthBeforeHealingAndDiamondDrawsOnCardCommit()
    {
        Assert.That(_cards.BuyArtifact(Load("rel_002"), 0), Is.True);
        Assert.That(_cards.BuyArtifact(Load("rel_026"), 0), Is.True);
        var heart = _cards.ownedCards.First(x => x.Suit == CardData.Suit.Hearts && x.Rank == CardData.Rank.Seven);
        typeof(PlayerRuntime).GetProperty("currentHealth").GetSetMethod(true).Invoke(_combat.player, new object[] { 14 });
        var action = new CombatActionContext(41, CombatActionOrigin.PlayerCard, _combat.player, card: heart);
        var queue = new CombatReactionQueue();
        GameplayEffectResolver.EnqueueCardCommitted(new CardCommittedEffectContext(action, heart, null, _combat, _cards, 0), queue);
        Assert.That(queue.ProcessPhase(CombatReactionPhase.CardCommitted), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(17));
        Assert.That(_cards.gold, Is.EqualTo(3));

        Assert.That(_cards.BuyArtifact(Load("rel_003"), 0), Is.True);
        Assert.That(_cards.BuyArtifact(Load("rel_027"), 0), Is.True);
        _cards.DrawToHand(3);
        int before = _cards.HandCount;
        var diamond = _cards.ownedCards.First(x => x.Suit == CardData.Suit.Diamonds && x.Rank == CardData.Rank.Seven);
        var diamondAction = new CombatActionContext(42, CombatActionOrigin.PlayerCard, _combat.player, card: diamond);
        var diamondQueue = new CombatReactionQueue();
        GameplayEffectResolver.EnqueueCardCommitted(new CardCommittedEffectContext(diamondAction, diamond, null, _combat, _cards, 0), diamondQueue);
        Assert.That(diamondQueue.ProcessPhase(CombatReactionPhase.CardCommitted), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(before + 2));
    }

    [TestCase(2, 3, 2)]
    [TestCase(3, 2, 2)]
    [TestCase(4, 2, 4)]
    [TestCase(0, 3, 0)]
    [TestCase(8, 0, 0)]
    public void RareRetaliationUsesActuallyBlockedDamageAndStrict150PercentThreshold(int block, int attack, int expected)
    {
        Assert.That(GameplayEffectResolver.CalculateRetaliationDamage(block, attack), Is.EqualTo(expected));
    }

    [Test]
    public void ShopEligibilityRejectsStandaloneHigherTiersAndAllowsOnlyImmediateUpgrade()
    {
        var common = Load("rel_031");
        var rare = Load("rel_032");
        var epic = Load("rel_033");
        Assert.That(_cards.GetArtifactAcquisitionUnavailableReason(rare), Is.EqualTo("Requires immediate predecessor"));
        Assert.That(_cards.BuyArtifact(common, 0), Is.True);
        Assert.That(_cards.GetArtifactAcquisitionUnavailableReason(rare), Is.Empty);
        Assert.That(_cards.GetArtifactAcquisitionUnavailableReason(epic), Is.EqualTo("Requires immediate predecessor"));
        Assert.That(_cards.BuyArtifact(rare, 0), Is.True);
        Assert.That(_cards.GetArtifactAcquisitionUnavailableReason(epic), Is.Empty);
    }
}
