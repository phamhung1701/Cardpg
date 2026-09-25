using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class Phase7B5CombatPlayModeTests
{
    [UnityTest]
    public IEnumerator ShieldedMultiHit_ResolvesAsIndependentRuntimeHits()
    {
        var type = EnemyTypeData.Create("PlayMode Target", 20, 0, 0);
        var guard = ScriptableObject.CreateInstance<EnemyAbility>();
        guard.effect = EnemyAbilityEffect.ReduceIncomingCardDamage;
        guard.amount = 1;
        type.abilities = new[] { guard };
        var enemy = new EnemyRuntime(type);
        var player = new PlayerRuntime();
        enemy.GainShield(1);
        var action = new CombatActionContext(
            1,
            CombatActionOrigin.PlayerCard,
            sourcePlayer: player,
            targetEnemy: enemy,
            hitCount: 3);
        var resolver = new CombatResolver();

        var first = resolver.Resolve(new DamageRequest(action, 0, CombatDamageOrigin.Card, player, enemy, 5), null);
        var second = resolver.Resolve(new DamageRequest(action, 1, CombatDamageOrigin.Card, player, enemy, 5), null);
        var third = resolver.Resolve(new DamageRequest(action, 2, CombatDamageOrigin.Card, player, enemy, 5), null);

        Assert.That(first.ShieldConsumed, Is.True);
        Assert.That(first.ActualHpLost, Is.Zero);
        Assert.That(second.ActualHpLost, Is.EqualTo(4));
        Assert.That(third.ActualHpLost, Is.EqualTo(4));
        Assert.That(enemy.currentHp, Is.EqualTo(12));
        Assert.That(enemy.ShieldCharges, Is.Zero);

        Object.Destroy(type);
        Object.Destroy(guard);
        yield return null;
    }
}
