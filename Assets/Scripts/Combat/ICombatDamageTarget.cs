public interface ICombatDamageTarget
{
    string CombatDisplayName { get; }
    int CurrentHealth { get; }
    int ShieldCharges { get; }
    bool IsDefeated { get; }

    int ModifyIncomingCombatDamage(DamageRequest request, CombatManager context, int damage);
    int ApplyResolvedCombatDamage(int damage);
    int GainShield(int amount);
    bool TryConsumeShield();
    void ResetShield();
}
