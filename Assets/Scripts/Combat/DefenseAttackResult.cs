/// <summary>Authoritative per-hit result from a committed player Block assignment.</summary>
public readonly struct DefenseAttackResult
{
    public EnemyRuntime Attacker { get; }
    public int AttackIndex { get; }
    public int IncomingDamage { get; }
    public int EffectiveBlock { get; }
    public int BlockedDamage { get; }
    public int RemainingDamage { get; }

    public bool IsPerfectBlock => IncomingDamage > 0 &&
        EffectiveBlock == IncomingDamage && BlockedDamage == IncomingDamage && RemainingDamage == 0;

    public DefenseAttackResult(EnemyRuntime attacker, int attackIndex, int incomingDamage,
        int effectiveBlock, int blockedDamage, int remainingDamage)
    {
        Attacker = attacker;
        AttackIndex = attackIndex;
        IncomingDamage = incomingDamage;
        EffectiveBlock = effectiveBlock;
        BlockedDamage = blockedDamage;
        RemainingDamage = remainingDamage;
    }
}
