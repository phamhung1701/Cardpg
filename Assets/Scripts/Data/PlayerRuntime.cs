using System;

public sealed class PlayerRuntime : ICombatDamageTarget
{
    public const int DefaultMaxHealth = 30;

    public int maxHealth { get; private set; }
    public int currentHealth { get; private set; }
    public int shieldCharges { get; private set; }

    public string CombatDisplayName => "Player";
    public int CurrentHealth => currentHealth;
    public int ShieldCharges => shieldCharges;
    public bool IsDefeated => currentHealth <= 0;

    public PlayerRuntime(int maxHealth = DefaultMaxHealth)
    {
        ValidateMaxHealth(maxHealth);
        this.maxHealth = maxHealth;
        currentHealth = maxHealth;
    }

    public void Configure(int maxHealth)
    {
        ValidateMaxHealth(maxHealth);
        this.maxHealth = maxHealth;
        currentHealth = Math.Min(currentHealth, maxHealth);
    }

    public void Reset()
    {
        currentHealth = maxHealth;
        ResetShield();
    }

    public int ModifyIncomingCombatDamage(DamageRequest request, CombatManager context, int damage)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevModeRuntime.InfiniteHealth) return 0;
#endif
        // Run/Event HP costs use TakeDamage directly and never enter this combat pipeline.
        if (context == null || request.Origin is not (
                CombatDamageOrigin.Card or CombatDamageOrigin.EnemyAggregate or
                CombatDamageOrigin.Recovery or CombatDamageOrigin.Reactive))
            return Math.Max(0, damage);
        return GameplayEffectResolver.ModifyIncomingCombatDamage(
            new IncomingDamageEffectContext(request, context, CardManager.Instance, damage));
    }

    public int ApplyResolvedCombatDamage(int amount) => TakeDamage(amount);

    public int GainShield(int amount)
    {
        if (amount <= 0 || IsDefeated) return 0;
        shieldCharges += amount;
        return amount;
    }

    public bool TryConsumeShield()
    {
        if (shieldCharges <= 0) return false;
        shieldCharges--;
        return true;
    }

    public void ResetShield()
    {
        shieldCharges = 0;
    }

    public int TakeDamage(int amount)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevModeRuntime.InfiniteHealth) return 0;
#endif
        if (amount <= 0 || IsDefeated)
            return 0;

        int previousHealth = currentHealth;
        currentHealth = Math.Max(0, currentHealth - amount);
        return previousHealth - currentHealth;
    }

    public int Heal(int amount)
    {
        if (amount <= 0 || IsDefeated)
            return 0;

        int previousHealth = currentHealth;
        currentHealth = Math.Min(maxHealth, currentHealth + amount);
        return currentHealth - previousHealth;
    }

    public int IncreaseMaxHealth(int amount, bool healIncrease = true)
    {
        if (amount <= 0) return 0;

        maxHealth += amount;
        if (healIncrease)
            currentHealth += amount;
        return amount;
    }

    static void ValidateMaxHealth(int maxHealth)
    {
        if (maxHealth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHealth), "Maximum health must be greater than zero.");
    }
}
