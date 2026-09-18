using UnityEngine;

public enum EnemyAbilityEffect
{
    IncreaseAttackOnEncounterStart,
    IncreaseAttackAfterPlayerCard,
    HealAfterPlayerCard,
    ReduceIncomingCardDamage
}

[CreateAssetMenu(fileName = "EnemyAbility", menuName = "Game/Enemy Ability")]
public class EnemyAbility : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea] public string description;
    public EnemyAbilityEffect effect;
    [Min(0)] public int amount = 1;

    public virtual int ModifyIncomingCardDamage(EnemyRuntime enemy, CombatManager context, int damage)
    {
        return effect == EnemyAbilityEffect.ReduceIncomingCardDamage
            ? Mathf.Max(0, damage - amount)
            : damage;
    }

    public virtual void OnEncounterStarted(EnemyRuntime enemy, CombatManager context)
    {
        if (effect == EnemyAbilityEffect.IncreaseAttackOnEncounterStart)
            enemy.IncreaseAttack(amount);
    }

    public virtual void OnPlayerCardResolved(EnemyRuntime enemy, CombatManager context)
    {
        if (enemy == null || enemy.IsDefeated) return;

        if (effect == EnemyAbilityEffect.IncreaseAttackAfterPlayerCard)
            enemy.IncreaseAttack(amount);
        else if (effect == EnemyAbilityEffect.HealAfterPlayerCard)
            enemy.Heal(amount);
    }
}
