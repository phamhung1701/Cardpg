using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class InfiniteRunScalingTests
{
    GameObject _cardsObject;
    GameObject _combatObject;
    GameObject _runObject;
    RunManager _run;
    readonly List<Object> _created = new();

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _cardsObject = new GameObject("Phase8 Infinite CardManager");
        _cardsObject.AddComponent<CardManager>().Configure(null, null, null,
            System.Array.Empty<RelicData>(), System.Array.Empty<CardEnhancementData>());
        _combatObject = new GameObject("Phase8 Infinite CombatManager");
        _combatObject.AddComponent<CombatManager>().ConfigurePlayer(30);
        _runObject = new GameObject("Phase8 Infinite RunManager");
        _run = _runObject.AddComponent<RunManager>();
        SetField(_run, "_randomContext", new RunRandomContext("phase8-infinite"));
        SetField(_run, "runSeed", "phase8-infinite");
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        if (_runObject != null) Object.DestroyImmediate(_runObject);
        if (_combatObject != null) Object.DestroyImmediate(_combatObject);
        if (_cardsObject != null) Object.DestroyImmediate(_cardsObject);
        foreach (var item in _created) if (item != null) Object.DestroyImmediate(item);
    }

    [Test]
    public void MapTwelveKeepsAuthoredBaselineAndMapThirteenAppliesOneStep()
    {
        Assert.That(InfiniteRunScaling.Scale(100, 100, 11), Is.EqualTo((320, 177)));
        Assert.That(InfiniteRunScaling.Scale(100, 100, 12), Is.EqualTo((352, 184)));
    }

    [Test]
    public void EachAdditionalMapFloorsAfterItsOwnCompoundedStep()
    {
        Assert.That(InfiniteRunScaling.ScaleStat(101, 12, 1.10m), Is.EqualTo(111));
        Assert.That(InfiniteRunScaling.ScaleStat(101, 13, 1.10m), Is.EqualTo(122));
        Assert.That(InfiniteRunScaling.ScaleStat(101, 14, 1.10m), Is.EqualTo(134));
    }

    [Test]
    public void PlayerGrowthAndHealingClampInsteadOfOverflowing()
    {
        var player = new PlayerRuntime(30);
        Assert.That(player.IncreaseMaxHealth(int.MaxValue), Is.EqualTo(int.MaxValue - 30));
        Assert.That(player.maxHealth, Is.EqualTo(int.MaxValue));
        Assert.That(player.currentHealth, Is.EqualTo(int.MaxValue));
        Assert.That(player.IncreaseMaxHealth(1), Is.Zero);
        Assert.That(player.Heal(int.MaxValue), Is.Zero);
        Assert.That(player.GainShield(int.MaxValue), Is.EqualTo(int.MaxValue));
        Assert.That(player.GainShield(1), Is.Zero);
        Assert.That(player.GainEncounterBlock(int.MaxValue), Is.EqualTo(int.MaxValue));
        Assert.That(player.GainEncounterBlock(1), Is.Zero);
    }

    [Test]
    public void InfiniteModeIsOptInAndResumesTimingIntoMapThirteenWithCachedBoss()
    {
        var deck = new List<EnemyTypeData>();
        for (int i = 0; i < 12; i++)
        {
            var boss = EnemyTypeData.Create($"Boss {i}", 100, 100, 10);
            deck.Add(boss);
            _created.Add(boss);
        }
        _run.bossDeck.AddRange(deck);
        _run.bossIndex = 12;
        SetProperty(_run, "IsRunCompleted", true);
        _run.Timing.StartNewRun();
        for (int map = 0; map < 11; map++) _run.Timing.CompleteCurrentMap();
        _run.Timing.CompleteCurrentMap(false);
        _run.Timing.StopRun();

        Assert.That(_run.CanContinueInfiniteMode, Is.True);
        Assert.That(_run.IsInfiniteMode, Is.False);
        Assert.That(_run.ContinueInfiniteMode(), Is.True);
        Assert.That(_run.IsInfiniteMode, Is.True);
        Assert.That(_run.IsRunCompleted, Is.False);
        Assert.That(_run.CurrentMapIndex, Is.EqualTo(12));
        Assert.That(_run.CurrentBoss, Is.SameAs(deck[0]));
        Assert.That(_run.Timing.IsActive, Is.True);
        Assert.That(_run.Timing.CurrentMapIndex, Is.EqualTo(12));
        Assert.That(_run.Timing.CompletedSplits.Count, Is.EqualTo(12));
        var bossNode = _run.currentPath.Find(node => node.kind == MapNodeType.Boss);
        Assert.That(_run.GetEncounterStats(bossNode), Is.EqualTo((352, 184)));
    }

    [Test]
    public void EnemyAttackHpShieldAndOppressionBudgetRemainSaturated()
    {
        var type = EnemyTypeData.Create("Infinite overflow enemy", int.MaxValue, int.MaxValue, 0);
        var oppression = ScriptableObject.CreateInstance<EnemyAbility>();
        oppression.effect = EnemyAbilityEffect.Oppression;
        type.abilities = new[] { oppression };
        _created.Add(type);
        _created.Add(oppression);
        var enemy = new EnemyRuntime(type);
        enemy.IncreaseAttack(1);
        Assert.That(enemy.EffectiveAttack, Is.EqualTo(int.MaxValue));
        Assert.That(enemy.ResponseAttack, Is.EqualTo(int.MaxValue));
        Assert.That(enemy.PrepareResponseAttack(int.MaxValue), Is.EqualTo(int.MaxValue));
        Assert.That(enemy.GainShield(int.MaxValue), Is.EqualTo(int.MaxValue));
        Assert.That(enemy.GainShield(1), Is.Zero);
        enemy.Heal(int.MaxValue);
        Assert.That(enemy.currentHp, Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void InfiniteTimingKeepsRollingRecentSplitsWithoutGrowingUnbounded()
    {
        var timing = new RunTimingStatistics();
        timing.StartNewRun();
        for (int map = 0; map < 11; map++) timing.CompleteCurrentMap();
        timing.CompleteCurrentMap(false);
        timing.StopRun();
        Assert.That(timing.ResumeAtNextMap(), Is.True);
        for (int map = 0; map < 101; map++) timing.CompleteCurrentMap();
        Assert.That(timing.CompletedSplits.Count, Is.EqualTo(100));
        Assert.That(timing.CurrentMapIndex, Is.EqualTo(113));
        timing.StartNewRun();
        Assert.That(timing.CompletedSplits.Count, Is.Zero);
        Assert.That(timing.CurrentMapIndex, Is.Zero);
    }

    [Test]
    public void InfiniteScalingClampsLargeStatsAndLeavesRewardsUnscaled()
    {
        Assert.That(InfiniteRunScaling.ScaleStat(int.MaxValue, 13, 1.10m), Is.EqualTo(int.MaxValue));
        Assert.That(InfiniteRunScaling.Scale(int.MaxValue, int.MaxValue, 12), Is.EqualTo((int.MaxValue, int.MaxValue)));
        Assert.That(InfiniteRunScaling.ScaleReward(20, 1000), Is.EqualTo(20));
        Assert.That(InfiniteRunScaling.ScaleReward(-2, 1000), Is.Zero);
    }

    static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    static void SetProperty(object target, string name, object value) =>
        target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(target, value);
}
