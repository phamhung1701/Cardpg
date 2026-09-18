using System;
using System.Linq;
using UnityEngine;

public class EnemyRuntime
{
    public EnemyTypeData type;
    public int maxHp;
    public int currentHp;
    public int currentAttack;
    public int PlayerTurnsCompleted { get; private set; }

    readonly int _instanceNumber;
    readonly int _encounterSize;

    public event Action OnHpChanged;
    public event Action OnAttackChanged;

    public EnemyRuntime(EnemyTypeData enemyType, int instanceNumber = 1, int encounterSize = 1)
    {
        type = enemyType != null ? enemyType : throw new ArgumentNullException(nameof(enemyType));
        maxHp = enemyType.maxHp;
        currentHp = enemyType.maxHp;
        currentAttack = enemyType.baseAttack;
        _instanceNumber = Mathf.Max(1, instanceNumber);
        _encounterSize = Mathf.Max(1, encounterSize);
    }

    public string DisplayName => _encounterSize > 1 ? $"{type.enemyName} {_instanceNumber}" : type.enemyName;
    public int GoldReward => type.goldReward;
    public bool IsDefeated => currentHp <= 0;
    public bool ShouldFlee => !IsDefeated && type.fleeAfterPlayerTurns > 0 &&
        PlayerTurnsCompleted >= type.fleeAfterPlayerTurns;
    public string AbilitySummary => type.abilities == null
        ? string.Empty
        : string.Join("  •  ", type.abilities.Where(ability => ability != null).Select(ability => ability.displayName));

    public int TakeCardDamage(int amount, CombatManager context)
    {
        int resolvedDamage = Mathf.Max(0, amount);
        if (type.abilities != null)
        {
            foreach (var ability in type.abilities)
                if (ability != null)
                    resolvedDamage = ability.ModifyIncomingCardDamage(this, context, resolvedDamage);
        }

        int previousHp = currentHp;
        currentHp = Mathf.Max(0, currentHp - resolvedDamage);
        OnHpChanged?.Invoke();
        return previousHp - currentHp;
    }

    public void TakeDamage(int amount)
    {
        currentHp = Mathf.Max(0, currentHp - amount);
        OnHpChanged?.Invoke();
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || IsDefeated) return;
        currentHp = Mathf.Min(maxHp, currentHp + amount);
        OnHpChanged?.Invoke();
    }

    public void ReduceAttack(int amount)
    {
        currentAttack = Mathf.Max(0, currentAttack - amount);
        OnAttackChanged?.Invoke();
    }

    public void IncreaseAttack(int amount)
    {
        if (amount <= 0) return;
        currentAttack += amount;
        OnAttackChanged?.Invoke();
    }

    public void NotifyEncounterStarted(CombatManager context)
    {
        if (type.abilities == null) return;
        foreach (var ability in type.abilities)
            ability?.OnEncounterStarted(this, context);
    }

    public void NotifyPlayerTurnCompleted()
    {
        if (!IsDefeated)
            PlayerTurnsCompleted++;
    }

    public void NotifyPlayerCardResolved(CombatManager context)
    {
        if (type.abilities == null) return;
        foreach (var ability in type.abilities)
            ability?.OnPlayerCardResolved(this, context);
    }
}
