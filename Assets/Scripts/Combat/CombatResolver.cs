using UnityEngine;

public sealed class CombatResolver
{
    public DamageResult Resolve(DamageRequest request, CombatManager context)
    {
        var target = request.Target;
        if (target.IsDefeated)
        {
            return new DamageResult(
                request,
                0,
                0,
                0,
                0,
                0,
                false,
                true,
                false);
        }

        int healthBefore = target.CurrentHealth;
        int modifiedDamage = Mathf.Max(0,
            target.ModifyIncomingCombatDamage(request, context, request.RequestedDamage));
        int modifierPrevented = Mathf.Max(0, request.RequestedDamage - modifiedDamage);

        bool shieldConsumed = request.AllowShield && modifiedDamage > 0 && target.TryConsumeShield();
        int shieldBlocked = shieldConsumed ? modifiedDamage : 0;
        int actualHpLost = shieldConsumed ? 0 : target.ApplyResolvedCombatDamage(modifiedDamage);
        int overkill = shieldConsumed ? 0 : Mathf.Max(0, modifiedDamage - healthBefore);
        bool wasLethal = healthBefore > 0 && target.IsDefeated;

        return new DamageResult(
            request,
            modifiedDamage,
            modifierPrevented,
            shieldBlocked,
            actualHpLost,
            overkill,
            shieldConsumed,
            false,
            wasLethal);
    }
}
