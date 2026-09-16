using System;
using UnityEngine;

public class EnemyRuntime
{
    public EnemyTypeData type;
    public int maxHp;
    public int currentHp;
    public int currentAttack;

    public event Action OnHpChanged;
    public event Action OnAttackChanged;

    public EnemyRuntime(EnemyTypeData enemyType)
    {
        type = enemyType;
        maxHp = enemyType.maxHp;
        currentHp = enemyType.maxHp;
        currentAttack = enemyType.baseAttack;
    }

    public string DisplayName => type.enemyName;
    public int GoldReward => type.goldReward;
    public bool IsDefeated => currentHp <= 0;

    public void TakeDamage(int amount)
    {
        currentHp = Mathf.Max(0, currentHp - amount);
        OnHpChanged?.Invoke();
    }

    public void ReduceAttack(int amount)
    {
        currentAttack = Mathf.Max(0, currentAttack - amount);
        OnAttackChanged?.Invoke();
    }
}
