using System;
using System.Linq;
using UnityEngine;

public class EnemyRuntime : ICombatDamageTarget
{
    public EnemyTypeData type;
    public int maxHp;
    public int currentHp;
    public int currentAttack;
    public int ShieldCharges { get; private set; }
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
    public string CombatDisplayName => DisplayName;
    public int CurrentHealth => currentHp;
    public int GoldReward => type.goldReward;
    public bool IsDefeated => currentHp <= 0;
    public bool ShouldFlee => !IsDefeated && type.fleeAfterPlayerTurns > 0 &&
        PlayerTurnsCompleted >= type.fleeAfterPlayerTurns;
    public string AbilitySummary => type.abilities == null
        ? string.Empty
        : string.Join("  •  ", type.abilities.Where(ability => ability != null).Select(ability => ability.displayName));

    public int TakeCardDamage(int amount, CombatManager context)
    {
        var action = new CombatActionContext(
            0,
            CombatActionOrigin.Legacy,
            sourcePlayer: context?.player,
            targetEnemy: this);
        var request = new DamageRequest(
            action,
            0,
            CombatDamageOrigin.Card,
            context?.player,
            this,
            amount);
        return new CombatResolver().Resolve(request, context).ActualHpLost;
    }

    public int ModifyIncomingCombatDamage(DamageRequest request, CombatManager context, int damage)
    {
        int resolvedDamage = Mathf.Max(0, damage);
        if (type.abilities == null) return resolvedDamage;

        for (int i = 0; i < type.abilities.Length; i++)
        {
            var ability = type.abilities[i];
            if (ability != null)
                resolvedDamage = ability.ModifyIncomingDamage(this, context, request, resolvedDamage);
        }
        return Mathf.Max(0, resolvedDamage);
    }

    public int ApplyResolvedCombatDamage(int amount)
    {
        int previousHp = currentHp;
        currentHp = Mathf.Max(0, currentHp - Mathf.Max(0, amount));
        OnHpChanged?.Invoke();
        return previousHp - currentHp;
    }

    public void DefeatInstantly()
    {
        if (IsDefeated) return;
        currentHp = 0;
        OnHpChanged?.Invoke();
    }

    public int GainShield(int amount)
    {
        if (amount <= 0 || IsDefeated) return 0;
        ShieldCharges += amount;
        return amount;
    }

    public bool TryConsumeShield()
    {
        if (ShieldCharges <= 0) return false;
        ShieldCharges--;
        return true;
    }

    public void ResetShield()
    {
        ShieldCharges = 0;
    }

    public void TakeDamage(int amount)
    {
        var action = new CombatActionContext(0, CombatActionOrigin.Legacy, targetEnemy: this);
        var request = new DamageRequest(action, 0, CombatDamageOrigin.Legacy, null, this, amount);
        new CombatResolver().Resolve(request, null);
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
