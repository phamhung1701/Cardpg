public readonly struct DamageResult
{
    public DamageRequest Request { get; }
    public int RequestedDamage { get; }
    public int ModifiedDamage { get; }
    public int ModifierPreventedDamage { get; }
    public int ShieldBlockedDamage { get; }
    public int ActualHpLost { get; }
    public int OverkillDamage { get; }
    public bool ShieldConsumed { get; }
    public bool TargetAlreadyDefeated { get; }
    public bool WasLethal { get; }

    public DamageResult(
        DamageRequest request,
        int modifiedDamage,
        int modifierPreventedDamage,
        int shieldBlockedDamage,
        int actualHpLost,
        int overkillDamage,
        bool shieldConsumed,
        bool targetAlreadyDefeated,
        bool wasLethal)
    {
        Request = request;
        RequestedDamage = request.RequestedDamage;
        ModifiedDamage = modifiedDamage;
        ModifierPreventedDamage = modifierPreventedDamage;
        ShieldBlockedDamage = shieldBlockedDamage;
        ActualHpLost = actualHpLost;
        OverkillDamage = overkillDamage;
        ShieldConsumed = shieldConsumed;
        TargetAlreadyDefeated = targetAlreadyDefeated;
        WasLethal = wasLethal;
    }
}
