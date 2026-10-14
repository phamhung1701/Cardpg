using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class GameplayBalanceTelemetryTests
{
    GameObject cardsObject;
    bool previousEnabled;

    [SetUp]
    public void SetUp()
    {
        previousEnabled = GameplayBalanceTelemetry.Enabled;
        GameplayBalanceTelemetry.Enabled = true;
        GameplayBalanceTelemetry.Begin("telemetry-test");
    }

    [TearDown]
    public void TearDown()
    {
        GameplayBalanceTelemetry.Finish("test");
        string path = GameplayBalanceTelemetry.LastExportPath;
        if (path != null && File.Exists(path)) File.Delete(path);
        GameplayBalanceTelemetry.Enabled = previousEnabled;
        if (cardsObject != null) Object.DestroyImmediate(cardsObject);
    }

    [Test]
    public void Credits_RecordActualClampedDeltasAndIgnoreFailedSpend()
    {
        cardsObject = new GameObject("Telemetry Cards");
        var cards = cardsObject.AddComponent<CardManager>();
        if (cards.HasInfiniteMoney) Assert.Ignore("Normal economy requires development infinite money disabled.");
        cards.AddCredits(12);
        Assert.That(cards.SpendCredits(5), Is.True);
        Assert.That(cards.SpendCredits(100), Is.False);
        cards.AddCredits(-100);
        var events = GameplayBalanceTelemetry.Current.events.Where(e => e.kind == "credits").ToArray();
        Assert.That(events.Select(e => e.a), Is.EqualTo(new[] { 12, -5, -7 }));
        Assert.That(cards.credits, Is.Zero);
    }

    [Test]
    public void DamageRecordsActualLossSeparatelyFromShieldAndOverkill()
    {
        var player = new PlayerRuntime();
        player.Configure(10);
        player.Reset();
        var action = new CombatActionContext(1, CombatActionOrigin.EnemyRetaliation);
        var request = new DamageRequest(action, 0, CombatDamageOrigin.EnemyAggregate, null, player, 50);
        GameplayBalanceTelemetry.Damage(new DamageResult(request, 50, 0, 50, 0, 0, true, false, false));
        GameplayBalanceTelemetry.Damage(new DamageResult(request, 50, 0, 0, 10, 40, false, false, true));
        var damage = GameplayBalanceTelemetry.Current.events.Where(e => e.kind == "damage_received").ToArray();
        Assert.That(damage.Sum(e => e.a), Is.EqualTo(10));
        Assert.That(damage.Sum(e => e.c), Is.EqualTo(50));
        Assert.That(GameplayBalanceTelemetry.Current.events.Single(e => e.kind == "lethal").detail,
            Is.EqualTo("EnemyAggregate"));
    }

    [Test]
    public void JsonRoundTrip_PreservesEscapedSeedAndIntegers()
    {
        var journal = new BalanceJournal { seed = "quote\" comma, newline\n" };
        journal.Add(new BalanceEvent { kind = "credits", a = int.MaxValue });
        var restored = JsonUtility.FromJson<BalanceJournal>(journal.ToJson());
        Assert.That(restored.seed, Is.EqualTo(journal.seed));
        Assert.That(restored.events.Single().a, Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void JournalIsBoundedAndMarksTruncation()
    {
        var journal = new BalanceJournal();
        var entry = new BalanceEvent { kind = "hp" };
        for (int i = 0; i <= BalanceJournal.EventLimit; i++) journal.Add(entry);
        Assert.That(journal.events.Count, Is.EqualTo(BalanceJournal.EventLimit));
        Assert.That(journal.truncated, Is.True);
    }

    [Test]
    public void FinishIsIdempotentAndLateCommittedTrickKeepsEncounterIdentity()
    {
        GameplayBalanceTelemetry.EncounterStarted(System.Array.Empty<EnemyRuntime>(), MapNodeType.Combat);
        GameplayBalanceTelemetry.EncounterEnded(EncounterResult.Defeat);
        GameplayBalanceTelemetry.TrickUsed("trick-test", true);
        GameplayBalanceTelemetry.Finish("completed");
        var journal = GameplayBalanceTelemetry.Current;
        Assert.That(journal.outcome, Is.EqualTo("defeat"));
        Assert.That(journal.events.Count(e => e.kind == "run_end"), Is.EqualTo(1));
        Assert.That(journal.events.Single(e => e.kind == "trick_used").encounter, Is.EqualTo(1));
        Assert.That(File.Exists(GameplayBalanceTelemetry.LastExportPath), Is.True);
    }

    [Test]
    public void DisabledNewRunDoesNotRecord()
    {
        GameplayBalanceTelemetry.Finish("test");
        GameplayBalanceTelemetry.Enabled = false;
        GameplayBalanceTelemetry.Begin("disabled");
        GameplayBalanceTelemetry.Record("hp", "", 5);
        Assert.That(GameplayBalanceTelemetry.Current, Is.Null);
    }
}
