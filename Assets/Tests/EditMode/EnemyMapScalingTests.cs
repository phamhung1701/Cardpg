using NUnit.Framework;
using UnityEngine;

public sealed class EnemyMapScalingTests
{
    [TestCase(0, 10, 4, 10, 4)]
    [TestCase(1, 10, 4, 12, 4)]
    [TestCase(11, 10, 4, 32, 7)]
    public void Scale_UsesZeroBasedMapIndexAndExpectedMapMilestones(
        int mapIndex, int baseHp, int baseAttack, int expectedHp, int expectedAttack)
    {
        var scaled = EnemyMapScaling.Scale(baseHp, baseAttack, mapIndex);
        Assert.That(scaled.hp, Is.EqualTo(expectedHp));
        Assert.That(scaled.attack, Is.EqualTo(expectedAttack));
    }

    [Test]
    public void Scale_RoundsHalfUp()
    {
        // At map 2, 5 HP becomes 6.0; 50 attack becomes 53.5.
        var tie = EnemyMapScaling.Scale(5, 50, 1);
        Assert.That(tie.hp, Is.EqualTo(6));
        Assert.That(tie.attack, Is.EqualTo(54));

        // At map 6, 5 HP becomes 10.0 and 10 attack becomes 13.5.
        var secondTie = EnemyMapScaling.Scale(5, 10, 5);
        Assert.That(secondTie.hp, Is.EqualTo(10));
        Assert.That(secondTie.attack, Is.EqualTo(14));
    }

    [Test]
    public void Scale_DoesNotMutateSourceOrCompoundAcrossCalls()
    {
        const int authoredHp = 17;
        const int authoredAttack = 9;

        var mapOne = EnemyMapScaling.Scale(authoredHp, authoredAttack, 0);
        var mapTwelve = EnemyMapScaling.Scale(authoredHp, authoredAttack, 11);
        var mapOneAgain = EnemyMapScaling.Scale(authoredHp, authoredAttack, 0);

        Assert.That(mapOne.hp, Is.EqualTo(authoredHp));
        Assert.That(mapOne.attack, Is.EqualTo(authoredAttack));
        Assert.That(mapTwelve.hp, Is.EqualTo(54));
        Assert.That(mapTwelve.attack, Is.EqualTo(16));
        Assert.That(mapOneAgain, Is.EqualTo(mapOne));
        Assert.That(authoredHp, Is.EqualTo(17));
        Assert.That(authoredAttack, Is.EqualTo(9));
    }

    [Test]
    public void EncounterStats_MatchScalingForNormalAndApplyFixedEliteAttackBonus()
    {
        var go = new GameObject("EnemyMapScalingTests RunManager");
        var enemy = EnemyTypeData.Create("Scaling Test", 20, 10, 1);
        enemy.name = "Scaling Test";
        try
        {
            var run = go.AddComponent<RunManager>();
            run.thiefType = enemy;
            var normal = new PathNode { kind = MapNodeType.Combat, contentId = enemy.name, mapIndex = 5 };
            var normalStats = run.GetEncounterStats(normal);
            var expectedNormal = EnemyMapScaling.Scale(20, 10, 5);
            Assert.That(normalStats.hp, Is.EqualTo(expectedNormal.hp));
            Assert.That(normalStats.attack, Is.EqualTo(expectedNormal.attack));

            var elite = new PathNode { kind = MapNodeType.Elite, contentId = enemy.name, mapIndex = 5 };
            var eliteStats = run.GetEncounterStats(elite);
            Assert.That(eliteStats.hp, Is.EqualTo(Mathf.CeilToInt(expectedNormal.hp * 1.5f)));
            Assert.That(eliteStats.attack, Is.EqualTo(expectedNormal.attack + 2),
                "Elite's +2 attack is a fixed bonus after map scaling.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(enemy);
        }
    }
}
