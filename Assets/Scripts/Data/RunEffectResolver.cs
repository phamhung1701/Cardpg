using System;

public static class RunEffectResolver
{
    public static bool CanApply(RunEventChoice choice, CardManager cards, CombatManager combat) =>
        CanApply(choice, cards, combat, null);

    public static bool CanApply(RunEventChoice choice, CardManager cards, CombatManager combat, RunManager run)
    {
        return string.IsNullOrEmpty(GetFailureReason(choice, cards, combat, run));
    }

    public static string GetFailureReason(RunEventChoice choice, CardManager cards, CombatManager combat) =>
        GetFailureReason(choice, cards, combat, null);

    public static string GetFailureReason(RunEventChoice choice, CardManager cards, CombatManager combat, RunManager run)
    {
        if (choice == null || cards == null || combat == null) return "Choice unavailable";

        bool infiniteGold = cards.HasInfiniteMoney;
        bool infiniteHealth = combat.HasInfiniteHealthForDev;
        int simulatedGold = infiniteGold ? int.MaxValue : cards.gold;
        int simulatedHealth = infiniteHealth ? int.MaxValue : combat.player.currentHealth;
        int simulatedMaxHealth = infiniteHealth ? int.MaxValue : combat.player.maxHealth;
        foreach (var effect in choice.effects ?? Array.Empty<RunEffect>())
        {
            switch (effect.type)
            {
                case RunEffectType.GainGold:
                    if (!infiniteGold) simulatedGold += effect.amount;
                    break;
                case RunEffectType.LoseGold:
                    if (!infiniteGold && simulatedGold < effect.amount)
                        return $"Need {effect.amount}g (have {simulatedGold}g)";
                    if (!infiniteGold) simulatedGold -= effect.amount;
                    break;
                case RunEffectType.Heal:
                    if (!infiniteHealth) simulatedHealth = Math.Min(simulatedMaxHealth, simulatedHealth + effect.amount);
                    break;
                case RunEffectType.LoseHealth:
                    if (!infiniteHealth && simulatedHealth <= effect.amount)
                        return $"Requires more than {effect.amount} HP";
                    if (!infiniteHealth) simulatedHealth -= effect.amount;
                    break;
                case RunEffectType.GainMaxHealth:
                    if (!infiniteHealth)
                    {
                        simulatedMaxHealth += effect.amount;
                        simulatedHealth += effect.amount;
                    }
                    break;
                case RunEffectType.RevealMapNode:
                    if (run == null || !run.CanRevealEligibleHiddenNode())
                        return "No eligible hidden node to reveal";
                    break;
                case RunEffectType.GainConsumable:
                    var consumable = cards.FindConsumable(effect.consumableId);
                    if (consumable == null || !consumable.eventAvailable) return "Consumable unavailable";
                    int slotCount = Math.Max(1, effect.amount);
                    if (!cards.CanAddConsumable(consumable, slotCount))
                        return "Backpack Full";
                    break;
                case RunEffectType.RandomArtifactEnhancementOrNothing:
                    if (run == null) return "Reward unavailable";
                    break;
            }
        }
        return string.Empty;
    }

    public static bool Apply(RunEventChoice choice, CardManager cards, CombatManager combat) =>
        Apply(choice, cards, combat, null);

    public static bool Apply(RunEventChoice choice, CardManager cards, CombatManager combat, RunManager run)
    {
        if (!CanApply(choice, cards, combat, run)) return false;

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
                case RunEffectType.RevealMapNode:
                    if (run == null || !run.TryRevealHiddenNodeFromEvent()) return false;
                    break;
                case RunEffectType.GainConsumable:
                    var consumable = cards.FindConsumable(effect.consumableId);
                    if (consumable == null || !cards.AddConsumable(consumable, Math.Max(1, effect.amount)))
                        return false;
                    break;
                case RunEffectType.RandomArtifactEnhancementOrNothing:
                    if (run == null || !run.TryGrantRandomSwampReward()) return false;
                    break;
            }
        }
        return true;
    }
}
