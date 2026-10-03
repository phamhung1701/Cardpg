using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase8RitualAndQuestTests
{
    GameObject _cardsObject;
    GameObject _combatObject;
    GameObject _runObject;
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    readonly List<RelicData> _artifacts = new();
    readonly List<RunEventDefinition> _events = new();

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _cardsObject = new GameObject("Phase8 CardManager");
        _cards = _cardsObject.AddComponent<CardManager>();
        _cards.Configure(null, null, null, System.Array.Empty<RelicData>(), System.Array.Empty<CardEnhancementData>());
        _combatObject = new GameObject("Phase8 CombatManager");
        _combat = _combatObject.AddComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
        _runObject = new GameObject("Phase8 RunManager");
        _run = _runObject.AddComponent<RunManager>();
        SetPrivate(_run, "_randomContext", new RunRandomContext("phase8-tests"));
        SetPrivate(_run, "runSeed", "phase8-tests");
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        if (_runObject != null) Object.DestroyImmediate(_runObject);
        if (_combatObject != null) Object.DestroyImmediate(_combatObject);
        if (_cardsObject != null) Object.DestroyImmediate(_cardsObject);
        foreach (var item in _events) if (item != null) Object.DestroyImmediate(item);
        foreach (var item in _artifacts) if (item != null) Object.DestroyImmediate(item);
    }

    [Test]
    public void EverythingSetsMaxHealthToFiveClampsWithoutHealingAndGrantsLegendary()
    {
        var legendary = Artifact("legendary", "Legendary", 1);
        ConfigureArtifacts(legendary);
        _combat.player.TakeDamage(22);
        BeginRitual();

        Assert.That(_run.CanChooseEventOption(0), Is.True);
        Assert.That(_run.ChooseEventOption(0), Is.True);
        Assert.That(_combat.player.maxHealth, Is.EqualTo(5));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(5));
        Assert.That(_cards.ownedArtifacts, Does.Contain(legendary));
        Assert.That(_run.ChooseEventOption(0), Is.False, "A repeated callback cannot grant the ritual reward again.");
        Assert.That(_cards.ownedArtifacts.Count, Is.EqualTo(1));
    }

    [Test]
    public void PoolRequiresMoreThanFifteenHpAndChargesOnlyAfterEpicGrant()
    {
        var epic = Artifact("epic", "Epic", 1);
        ConfigureArtifacts(epic);
        BeginRitual();
        Assert.That(_run.ChooseEventOption(1), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(15));
        Assert.That(_cards.ownedArtifacts, Does.Contain(epic));

        BeginRitual();
        Assert.That(_run.CanChooseEventOption(1), Is.False);
        Assert.That(_run.GetEventOptionUnavailableReason(1), Does.Contain("more than 15"));
    }

    [Test]
    public void DropUpgradesOneOwnedFamilyAndChargesExactlyFiveHp()
    {
        var common = Artifact("family-common", "Common", 1);
        var rare = Artifact("family-rare", "Rare", 2, common.canonicalId);
        ConfigureArtifacts(common, rare);
        Assert.That(_cards.GrantArtifactReward(common), Is.True);
        _combat.player.TakeDamage(24);
        BeginRitual();

        Assert.That(_run.ChooseEventOption(2), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(1), "Five HP is the largest nonlethal ritual payment.");
        Assert.That(_cards.ownedArtifacts, Does.Contain(rare));
        Assert.That(System.Linq.Enumerable.Contains(_cards.ownedArtifacts, common), Is.False);
    }

    [Test]
    public void FullInventoryRequiresReplacementChoiceAndCancelPaysNothing()
    {
        var full = new List<RelicData>();
        for (int i = 0; i < CardManager.BASE_ARTIFACT_CAPACITY; i++)
            full.Add(Artifact($"common-{i}", "Common", 1));
        var epic = Artifact("epic", "Epic", 1);
        full.Add(epic);
        ConfigureArtifacts(full.ToArray());
        foreach (var item in full.GetRange(0, CardManager.BASE_ARTIFACT_CAPACITY))
            Assert.That(_cards.GrantArtifactReward(item), Is.True);
        BeginRitual();

        RunEventDefinition shown = null;
        _run.OnShowEvent += definition => shown = definition;
        Assert.That(_run.ChooseEventOption(1), Is.True);
        Assert.That(shown, Is.Not.Null);
        Assert.That(shown.choices.Length, Is.EqualTo(CardManager.BASE_ARTIFACT_CAPACITY + 1));
        Assert.That(_run.ChooseEventOption(shown.choices.Length - 1), Is.True);
        Assert.That(_cards.ownedArtifacts.Count, Is.EqualTo(CardManager.BASE_ARTIFACT_CAPACITY));
        Assert.That(System.Linq.Enumerable.Contains(_cards.ownedArtifacts, epic), Is.False);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30));
        Assert.That(_combat.player.maxHealth, Is.EqualTo(30));
    }

    [Test]
    public void FullInventoryReplacementAppliesArtifactAndCostOnlyAfterExplicitChoice()
    {
        var full = new List<RelicData>();
        for (int i = 0; i < CardManager.BASE_ARTIFACT_CAPACITY; i++)
            full.Add(Artifact($"replacement-common-{i}", "Common", 1));
        var epic = Artifact("replacement-epic", "Epic", 1);
        full.Add(epic);
        ConfigureArtifacts(full.ToArray());
        foreach (var item in full.GetRange(0, CardManager.BASE_ARTIFACT_CAPACITY))
            Assert.That(_cards.GrantArtifactReward(item), Is.True);
        BeginRitual();
        RunEventDefinition shown = null;
        _run.OnShowEvent += definition => shown = definition;

        Assert.That(_run.ChooseEventOption(1), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30), "The HP payment waits until replacement succeeds.");
        Assert.That(_cards.ownedArtifacts.Count, Is.EqualTo(CardManager.BASE_ARTIFACT_CAPACITY));
        Assert.That(_run.ChooseEventOption(0), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(15));
        Assert.That(_cards.ownedArtifacts.Count, Is.EqualTo(CardManager.BASE_ARTIFACT_CAPACITY));
        Assert.That(System.Linq.Enumerable.Contains(_cards.ownedArtifacts, epic), Is.True);
        Assert.That(System.Linq.Enumerable.Contains(_cards.ownedArtifacts, full[0]), Is.False);
    }

    [Test]
    public void InfiniteRewardGoldSaturatesAtTheIntegerLimit()
    {
        _cards.gold = int.MaxValue - 1;
        _cards.AddGold(10);
        Assert.That(_cards.gold, Is.EqualTo(int.MaxValue));
        _cards.AddGold(1);
        Assert.That(_cards.gold, Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void EliteContractAtFullArtifactCapacityAwardsTheFiveHpFallback()
    {
        var full = new System.Collections.Generic.List<RelicData>();
        for (int i = 0; i < CardManager.BASE_ARTIFACT_CAPACITY; i++)
        {
            var item = Artifact($"quest-full-{i}", "Common", 1);
            full.Add(item);
        }
        ConfigureArtifacts(full.ToArray());
        foreach (var item in full) Assert.That(_cards.GrantArtifactReward(item), Is.True);
        _combat.player.TakeDamage(5);
        SetPrivate(_combat, "_eliteKillCount", 1);
        SetPrivate(_run, "_activeContract", new RunContractState
        {
            objective = RunContractObjective.DefeatEliteThisMap,
            startedAtMapIndex = 0,
            startedAtNodeId = 5,
            deadlineMapExclusive = 12,
            stableId = "elite-contract-full"
        });

        InvokePrivate(_run, "ProgressContractForVictory", new PathNode { id = 9, mapIndex = 0, kind = MapNodeType.Elite }, 0);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30));
        Assert.That(_cards.ownedArtifacts.Count, Is.EqualTo(CardManager.BASE_ARTIFACT_CAPACITY));
        Assert.That(System.Linq.Enumerable.All(_cards.ownedArtifacts, item => item.rarity == "Common"), Is.True);
        Assert.That(_run.ActiveContract, Is.Null);
    }

    [Test]
    public void ContractCompletesAfterTwoWinsExactlyOnceAndExpiryPenaltyIsNonlethal()
    {
        SetPrivate(_run, "_activeContract", new RunContractState
        {
            objective = RunContractObjective.WinTwoEncounters,
            startedAtMapIndex = 0,
            deadlineMapExclusive = 12,
            stableId = "contract-test"
        });
        InvokePrivate(_run, "ProgressContractForVictory", new PathNode { kind = MapNodeType.Combat }, 0);
        Assert.That(_cards.gold, Is.Zero);
        InvokePrivate(_run, "ProgressContractForVictory", new PathNode { kind = MapNodeType.Elite }, 0);
        Assert.That(_cards.gold, Is.EqualTo(6));
        Assert.That(_run.ActiveContract, Is.Null);
        InvokePrivate(_run, "ProgressContractForVictory", new PathNode { kind = MapNodeType.Boss }, 0);
        Assert.That(_cards.gold, Is.EqualTo(6));

        _combat.player.TakeDamage(27);
        SetPrivate(_run, "_activeContract", new RunContractState
        {
            objective = RunContractObjective.WinTwoEncounters,
            startedAtMapIndex = 0,
            deadlineMapExclusive = 1,
            stableId = "expiry-test"
        });
        InvokePrivate(_run, "SettleContractAtMapExit");
        Assert.That(_combat.player.currentHealth, Is.EqualTo(1));
        Assert.That(_run.ActiveContract, Is.Null);
    }

    void BeginRitual()
    {
        var eventDefinition = ScriptableObject.CreateInstance<RunEventDefinition>();
        eventDefinition.id = "evt_100";
        eventDefinition.category = MapNodeType.Risk;
        eventDefinition.choices = new[]
        {
            new RunEventChoice { interaction = RunEventChoiceInteraction.Immediate },
            new RunEventChoice { interaction = RunEventChoiceInteraction.Immediate },
            new RunEventChoice { interaction = RunEventChoiceInteraction.Immediate }
        };
        _events.Add(eventDefinition);
        SetPrivate(_run, "_activeEvent", eventDefinition);
        SetPrivate(_run, "_activeNode", new PathNode { id = 7, mapIndex = 0, kind = MapNodeType.Risk });
    }

    RelicData Artifact(string id, string rarity, int tier, string parent = null)
    {
        var item = ScriptableObject.CreateInstance<RelicData>();
        item.id = id;
        item.canonicalId = id;
        item.displayName = id;
        item.rarity = rarity;
        item.tier = tier;
        item.upgradeFromId = parent;
        _artifacts.Add(item);
        return item;
    }

    void ConfigureArtifacts(params RelicData[] artifacts) =>
        _cards.Configure(null, null, null, artifacts, System.Array.Empty<CardEnhancementData>());

    static void SetPrivate(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    static void InvokePrivate(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}
