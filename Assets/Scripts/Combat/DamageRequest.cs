using System;

public enum CombatDamageOrigin
{
    Card,
    EnemyAggregate,
    Recovery,
    Reactive,
    Legacy,
    Consumable
}

public readonly struct DamageRequest
{
    public CombatActionContext Action { get; }
    public int HitIndex { get; }
    public CombatDamageOrigin Origin { get; }
    public ICombatDamageTarget Source { get; }
    public ICombatDamageTarget Target { get; }
    public int RequestedDamage { get; }
    public bool AllowShield { get; }
    public CardInstance HitCard { get; }
    public bool IsCritical { get; }

    public DamageRequest(
        CombatActionContext action,
        int hitIndex,
        CombatDamageOrigin origin,
        ICombatDamageTarget source,
        ICombatDamageTarget target,
        int requestedDamage,
        bool allowShield = true,
        CardInstance hitCard = null,
        bool isCritical = false)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
        if (hitIndex < 0) throw new ArgumentOutOfRangeException(nameof(hitIndex));
        Origin = origin;
        Source = source;
        Target = target ?? throw new ArgumentNullException(nameof(target));
        HitIndex = hitIndex;
        RequestedDamage = Math.Max(0, requestedDamage);
        AllowShield = allowShield;
        HitCard = hitCard;
        IsCritical = isCritical;
    }
}
