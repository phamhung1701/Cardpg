using System;

public static class RunEffectResolver
{
    public static bool CanApply(RunEventChoice choice, CardManager cards, CombatManager combat)
    {
        return string.IsNullOrEmpty(GetFailureReason(choice, cards, combat));
    }

    public static string GetFailureReason(RunEventChoice choice, CardManager cards, CombatManager combat)
    {
        if (choice == null || cards == null || combat == null) return "Choice unavailable";

        int simulatedGold = cards.gold;
        int simulatedHealth = combat.player.currentHealth;
        int simulatedMaxHealth = combat.player.maxHealth;
        foreach (var effect in choice.effects ?? Array.Empty<RunEffect>())
        {
            switch (effect.type)
            {
                case RunEffectType.GainGold:
                    simulatedGold += effect.amount;
                    break;
                case RunEffectType.LoseGold:
                    if (simulatedGold < effect.amount)
                        return $"Need {effect.amount}g (have {simulatedGold}g)";
                    simulatedGold -= effect.amount;
                    break;
                case RunEffectType.Heal:
                    simulatedHealth = Math.Min(simulatedMaxHealth, simulatedHealth + effect.amount);
                    break;
                case RunEffectType.LoseHealth:
                    if (simulatedHealth <= effect.amount)
                        return $"Requires more than {effect.amount} HP";
                    simulatedHealth -= effect.amount;
                    break;
                case RunEffectType.GainMaxHealth:
                    simulatedMaxHealth += effect.amount;
                    simulatedHealth += effect.amount;
                    break;
            }
        }
        return string.Empty;
    }

    public static bool Apply(RunEventChoice choice, CardManager cards, CombatManager combat)
    {
        if (!CanApply(choice, cards, combat)) return false;

        foreach (var effect in choice.effects ?? Array.Empty<RunEffect>())
        {
            switch (effect.type)
            {
                case RunEffectType.GainGold:
                    cards.AddGold(effect.amount);
                    break;
                case RunEffectType.LoseGold:
                    cards.SpendGold(effect.amount);
                    break;
                case RunEffectType.Heal:
                    combat.HealPlayer(effect.amount);
                    break;
                case RunEffectType.LoseHealth:
                    combat.TakeRunDamage(effect.amount);
                    break;
                case RunEffectType.GainMaxHealth:
                    combat.IncreasePlayerMaxHealth(effect.amount);
                    break;
                case RunEffectType.DrawCards:
                    cards.DrawToHand(effect.amount);
                    break;
            }
        }
        return true;
    }
}
