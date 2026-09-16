using System;

public sealed class PlayerRuntime
{
    public const int DefaultMaxHealth = 30;

    public int maxHealth { get; private set; }
    public int currentHealth { get; private set; }

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
    }

    public int TakeDamage(int amount)
    {
        if (amount <= 0 || IsDefeated)
            return 0;

        int previousHealth = currentHealth;
        currentHealth = Math.Max(0, currentHealth - amount);
        return previousHealth - currentHealth;
    }

    static void ValidateMaxHealth(int maxHealth)
    {
        if (maxHealth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHealth), "Maximum health must be greater than zero.");
    }
}
