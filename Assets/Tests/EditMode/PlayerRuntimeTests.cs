using System;
using NUnit.Framework;

public sealed class PlayerRuntimeTests
{
    [Test]
    public void Constructor_DefaultHealth_StartsAtDefaultMaximumAndFullHealth()
    {
        var player = new PlayerRuntime();

        Assert.That(player.maxHealth, Is.EqualTo(PlayerRuntime.DefaultMaxHealth));
        Assert.That(player.currentHealth, Is.EqualTo(PlayerRuntime.DefaultMaxHealth));
        Assert.That(player.IsDefeated, Is.False);
    }

    [Test]
    public void Constructor_ProvidedHealth_StartsAtProvidedMaximumAndFullHealth()
    {
        var player = new PlayerRuntime(42);

        Assert.That(player.maxHealth, Is.EqualTo(42));
        Assert.That(player.currentHealth, Is.EqualTo(42));
        Assert.That(player.IsDefeated, Is.False);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_NonPositiveMaximumHealth_Throws(int maxHealth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerRuntime(maxHealth));
    }

    [Test]
    public void Configure_PositiveMaximumHealth_UpdatesMaximumAndClampsCurrentHealth()
    {
        var player = new PlayerRuntime(30);
        player.TakeDamage(5);

        player.Configure(20);

        Assert.That(player.maxHealth, Is.EqualTo(20));
        Assert.That(player.currentHealth, Is.EqualTo(20));
    }

    [Test]
    public void Configure_IncreasedMaximumHealth_DoesNotHealCurrentHealth()
    {
        var player = new PlayerRuntime(30);
        player.TakeDamage(8);

        player.Configure(40);

        Assert.That(player.maxHealth, Is.EqualTo(40));
        Assert.That(player.currentHealth, Is.EqualTo(22));
    }

    [TestCase(0)]
    [TestCase(-10)]
    public void Configure_NonPositiveMaximumHealth_ThrowsAndPreservesHealth(int maxHealth)
    {
        var player = new PlayerRuntime(30);
        player.TakeDamage(7);

        Assert.Throws<ArgumentOutOfRangeException>(() => player.Configure(maxHealth));
        Assert.That(player.maxHealth, Is.EqualTo(30));
        Assert.That(player.currentHealth, Is.EqualTo(23));
    }

    [Test]
    public void Reset_AfterDamage_FullyHealsToConfiguredMaximum()
    {
        var player = new PlayerRuntime(30);
        player.TakeDamage(12);
        player.Configure(25);

        player.Reset();

        Assert.That(player.currentHealth, Is.EqualTo(25));
        Assert.That(player.IsDefeated, Is.False);
    }

    [Test]
    public void TakeDamage_PartialDamage_ReducesHealthAndReturnsActualDamage()
    {
        var player = new PlayerRuntime(30);

        int actualDamage = player.TakeDamage(8);

        Assert.That(actualDamage, Is.EqualTo(8));
        Assert.That(player.currentHealth, Is.EqualTo(22));
        Assert.That(player.IsDefeated, Is.False);
    }

    [Test]
    public void TakeDamage_ExactDamage_ReachesZeroAndReturnsActualDamage()
    {
        var player = new PlayerRuntime(12);

        int actualDamage = player.TakeDamage(12);

        Assert.That(actualDamage, Is.EqualTo(12));
        Assert.That(player.currentHealth, Is.Zero);
        Assert.That(player.IsDefeated, Is.True);
    }

    [Test]
    public void TakeDamage_LethalOverkill_ClampsAtZeroAndReturnsRemainingHealth()
    {
        var player = new PlayerRuntime(10);
        player.TakeDamage(4);

        int actualDamage = player.TakeDamage(100);

        Assert.That(actualDamage, Is.EqualTo(6));
        Assert.That(player.currentHealth, Is.Zero);
        Assert.That(player.IsDefeated, Is.True);
    }

    [TestCase(0)]
    [TestCase(-5)]
    public void TakeDamage_NonPositiveAmount_IsNoOp(int amount)
    {
        var player = new PlayerRuntime(30);

        int actualDamage = player.TakeDamage(amount);

        Assert.That(actualDamage, Is.Zero);
        Assert.That(player.currentHealth, Is.EqualTo(30));
        Assert.That(player.IsDefeated, Is.False);
    }

    [Test]
    public void TakeDamage_WhenAlreadyDefeated_IsNoOp()
    {
        var player = new PlayerRuntime(5);
        player.TakeDamage(5);

        int actualDamage = player.TakeDamage(3);

        Assert.That(actualDamage, Is.Zero);
        Assert.That(player.currentHealth, Is.Zero);
        Assert.That(player.IsDefeated, Is.True);
    }
}
