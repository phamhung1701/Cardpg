using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameplayEffectKind
{
    FlatAttack,
    AttackMultiplier,
    FlatBlock,
    BlockMultiplier,
    Heal,
    Draw,
    BonusGold,
    ReduceAttackByCardValue,
    RecycleDiscardByCardValue,
    DrawByCardValue
}

public enum GameplayEffectTrigger
{
    AttackCalculated,
    DefenseCalculated,
    CardCommitted,
    PlayerTurnStart,
    EncounterWon
}

public enum GameplayEffectCategory
{
    Reactive,
    NumericModifier,
    RuleModifier
}

public enum GameplayConditionKind
{
    CardSuit,
    EnhancedCard
}

[Serializable]
public struct GameplayEffectCondition
{
    public GameplayConditionKind kind;
    public CardData.Suit suit;
    public bool expected;

    public bool Matches(CardInstance card)
    {
        return kind switch
        {
            GameplayConditionKind.CardSuit => card != null && card.Suit == suit,
            GameplayConditionKind.EnhancedCard => (card?.Enhancement != null) == expected,
            _ => false
        };
    }
}

// Rule modifiers deliberately do not pass through numeric/reactive Apply paths.
// Future typed subclasses own selection/turn/map permission behavior.
public abstract class GameplayRuleModifierData : ScriptableObject
{
    public string ruleId;
}

[Serializable]
public struct GameplayEffectDefinition
{
    public GameplayEffectKind kind;
    public GameplayEffectTrigger trigger;
    [Min(0)] public int amount;
    public GameplayEffectCondition[] conditions;

    public GameplayEffectCategory Category => kind switch
    {
        GameplayEffectKind.FlatAttack or GameplayEffectKind.AttackMultiplier or
            GameplayEffectKind.FlatBlock or GameplayEffectKind.BlockMultiplier => GameplayEffectCategory.NumericModifier,
        _ => GameplayEffectCategory.Reactive
    };

    public bool Matches(CardInstance card)
    {
        if (conditions == null) return true;
        for (int i = 0; i < conditions.Length; i++)
            if (!conditions[i].Matches(card)) return false;
        return true;
    }
}

// This state belongs to one run-owned source, never to an authoring asset.
public sealed class GameplayEffectState
{
    readonly Dictionary<int, int> _counters = new();

    public int GetCounter(int effectIndex) => _counters.TryGetValue(effectIndex, out int value) ? value : 0;
    public void SetCounter(int effectIndex, int value) => _counters[effectIndex] = value;
    public void Clear() => _counters.Clear();
}

public sealed class ArtifactRuntimeInstance
{
    public RelicData Definition { get; }
    public GameplayEffectState State { get; } = new();

    public ArtifactRuntimeInstance(RelicData definition) =>
        Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
}

public readonly struct GameplayEffectSource
{
    public ArtifactRuntimeInstance Artifact { get; }
    public CardInstance Card { get; }
    public GameplayEffectState State => Artifact != null ? Artifact.State : Card?.EffectState;
    public string DisplayName => Artifact != null ? Artifact.Definition.displayName : Card?.Enhancement?.displayName;
    public CombatReactionSourceCategory Category => Artifact != null
        ? CombatReactionSourceCategory.Artifact : CombatReactionSourceCategory.Enhancement;

    public GameplayEffectSource(ArtifactRuntimeInstance artifact) { Artifact = artifact; Card = null; }
    public GameplayEffectSource(CardInstance card) { Artifact = null; Card = card; }
}

public readonly struct CardCommittedEffectContext
{
    public CombatActionContext Action { get; }
    public CardInstance Card { get; }
    public EnemyRuntime Target { get; }
    public CombatManager Combat { get; }
    public CardManager Cards { get; }
    public int CardOrder { get; }
    public int HitIndex => -1 - CardOrder; // Once per committed card, never once per hit.
    public int HitCount => Action.HitCount;

    public CardCommittedEffectContext(CombatActionContext action, CardInstance card,
        EnemyRuntime target, CombatManager combat, CardManager cards, int cardOrder)
    {
        Action = action;
        Card = card;
        Target = target;
        Combat = combat;
        Cards = cards;
        CardOrder = cardOrder;
    }
}

public readonly struct PlayerTurnStartEffectContext
{
    public CombatActionContext Action { get; }
    public PlayerRuntime Player { get; }
    public CombatManager Combat { get; }
    public CardManager Cards { get; }
    public int TurnNumber { get; }

    public PlayerTurnStartEffectContext(CombatActionContext action, PlayerRuntime player,
        CombatManager combat, CardManager cards, int turnNumber)
    {
        Action = action;
        Player = player;
        Combat = combat;
        Cards = cards;
        TurnNumber = turnNumber;
    }
}

// The only execution/calculation foundation for Artifact and Enhancement effect sources.
// Legacy fields feed this same foundation only when a definition has no authored effects.
public static class GameplayEffectResolver
{
    public static int EffectCount(GameplayEffectSource source)
    {
        if (source.Artifact != null)
        {
            var definition = source.Artifact.Definition;
            if (definition.effects != null && definition.effects.Length > 0) return definition.effects.Length;
            int count = 0;
            if (definition.damageMultiplier > 1) count++;
            if (definition.flatDamageBonus != 0) count++;
            if (definition.defenseBonus != 0) count++;
            if (definition.reduceEnemyAttackByCardValue) count++;
            if (definition.recycleDiscardByCardValue) count++;
            if (definition.drawByCardValue) count++;
            if (definition.healAfterVictory > 0) count++;
            if (definition.bonusGold > 0) count++;
            return count;
        }
        var enhancement = source.Card?.Enhancement;
        if (enhancement == null) return 0;
        if (enhancement.effects != null && enhancement.effects.Length > 0) return enhancement.effects.Length;
        int legacyCount = 0;
        if (enhancement.attackBonus != 0) legacyCount++;
        if (enhancement.defenseBonus != 0) legacyCount++;
        if (enhancement.healOnPlay > 0) legacyCount++;
        if (enhancement.drawOnPlay > 0) legacyCount++;
        return legacyCount;
    }

    public static GameplayEffectDefinition EffectAt(GameplayEffectSource source, int index)
    {
        if (source.Artifact != null)
        {
            var a = source.Artifact.Definition;
            if (a.effects != null && a.effects.Length > 0) return a.effects[index];
            var effect = new GameplayEffectDefinition { conditions = Array.Empty<GameplayEffectCondition>() };
            if (a.damageMultiplier > 1 && index-- == 0) return With(effect, GameplayEffectKind.AttackMultiplier, GameplayEffectTrigger.AttackCalculated, a.damageMultiplier);
            if (a.flatDamageBonus != 0 && index-- == 0) return With(effect, GameplayEffectKind.FlatAttack, GameplayEffectTrigger.AttackCalculated, a.flatDamageBonus);
            if (a.defenseBonus != 0 && index-- == 0) return With(effect, GameplayEffectKind.FlatBlock, GameplayEffectTrigger.DefenseCalculated, a.defenseBonus);
            if (a.reduceEnemyAttackByCardValue && index-- == 0) return With(effect, GameplayEffectKind.ReduceAttackByCardValue, GameplayEffectTrigger.CardCommitted, 1);
            if (a.recycleDiscardByCardValue && index-- == 0) return With(effect, GameplayEffectKind.RecycleDiscardByCardValue, GameplayEffectTrigger.CardCommitted, 1);
            if (a.drawByCardValue && index-- == 0) return With(effect, GameplayEffectKind.DrawByCardValue, GameplayEffectTrigger.CardCommitted, 1);
            effect.conditions = Array.Empty<GameplayEffectCondition>();
            if (a.healAfterVictory > 0 && index-- == 0) return With(effect, GameplayEffectKind.Heal, GameplayEffectTrigger.EncounterWon, a.healAfterVictory);
            if (a.bonusGold > 0 && index == 0) return With(effect, GameplayEffectKind.BonusGold, GameplayEffectTrigger.EncounterWon, a.bonusGold);
        }
        else if (source.Card?.Enhancement != null)
        {
            var e = source.Card.Enhancement;
            if (e.effects != null && e.effects.Length > 0) return e.effects[index];
            if (e.attackBonus != 0 && index-- == 0) return With(default, GameplayEffectKind.FlatAttack, GameplayEffectTrigger.AttackCalculated, e.attackBonus);
            if (e.defenseBonus != 0 && index-- == 0) return With(default, GameplayEffectKind.FlatBlock, GameplayEffectTrigger.DefenseCalculated, e.defenseBonus);
            if (e.healOnPlay > 0 && index-- == 0) return With(default, GameplayEffectKind.Heal, GameplayEffectTrigger.CardCommitted, e.healOnPlay);
            if (e.drawOnPlay > 0 && index == 0) return With(default, GameplayEffectKind.Draw, GameplayEffectTrigger.CardCommitted, e.drawOnPlay);
        }
        throw new ArgumentOutOfRangeException(nameof(index));
    }

    static GameplayEffectDefinition With(GameplayEffectDefinition effect, GameplayEffectKind kind,
        GameplayEffectTrigger trigger, int amount)
    {
        effect.kind = kind;
        effect.trigger = trigger;
        effect.amount = amount;
        return effect;
    }

    static bool Applies(GameplayEffectSource source, GameplayEffectDefinition effect,
        GameplayEffectTrigger trigger, CardInstance card)
    {
        if (source.Artifact != null &&
            (source.Artifact.Definition.effects == null || source.Artifact.Definition.effects.Length == 0) &&
            trigger != GameplayEffectTrigger.EncounterWon &&
            !source.Artifact.Definition.Matches(card))
            return false;
        return effect.trigger == trigger && effect.Matches(card) &&
            (source.Card == null || ReferenceEquals(source.Card, card));
    }

    public static int CardLocalFlat(CardInstance card, GameplayEffectKind kind)
    {
        if (card?.Enhancement == null) return 0;
        var source = new GameplayEffectSource(card);
        var trigger = kind == GameplayEffectKind.FlatAttack
            ? GameplayEffectTrigger.AttackCalculated : GameplayEffectTrigger.DefenseCalculated;
        int flat = 0;
        for (int i = 0; i < EffectCount(source); i++)
        {
            var effect = EffectAt(source, i);
            if (effect.kind == kind && Applies(source, effect, trigger, card)) flat += effect.amount;
        }
        return flat;
    }

    public static int CalculateAttack(CardInstance card, CardManager cards) =>
        CalculateValue(card, cards, false);

    public static int CalculateBlock(CardInstance card, CardManager cards) =>
        CalculateValue(card, cards, true);

    static int CalculateValue(CardInstance card, CardManager cards, bool defense)
    {
        if (card == null) return 0;
        var trigger = defense ? GameplayEffectTrigger.DefenseCalculated : GameplayEffectTrigger.AttackCalculated;
        var flatKind = defense ? GameplayEffectKind.FlatBlock : GameplayEffectKind.FlatAttack;
        var multiplyKind = defense ? GameplayEffectKind.BlockMultiplier : GameplayEffectKind.AttackMultiplier;
        int localFlat = 0, localMultiplier = 1, artifactFlat = 0, artifactMultiplier = 1;
        if (card.Enhancement != null)
            Accumulate(new GameplayEffectSource(card), card, trigger, flatKind, multiplyKind,
                ref localFlat, ref localMultiplier);
        if (cards != null)
            for (int i = 0; i < cards.ownedArtifacts.Count; i++)
            {
                var artifact = cards.ownedArtifacts[i];
                if (artifact == null) continue;
                Accumulate(new GameplayEffectSource(cards.GetArtifactInstance(artifact)), card,
                    trigger, flatKind, multiplyKind, ref artifactFlat, ref artifactMultiplier);
            }
        // Card-local flats first, then multipliers, then Artifact flats; same path for preview and execution.
        long value = ((long)card.BaseAttackValue + localFlat) * localMultiplier * artifactMultiplier + artifactFlat;
        return (int)Math.Min(Math.Max(value, 0L), int.MaxValue);
    }

    static void Accumulate(GameplayEffectSource source, CardInstance card,
        GameplayEffectTrigger trigger, GameplayEffectKind flatKind, GameplayEffectKind multiplierKind,
        ref int flat, ref int multiplier)
    {
        for (int i = 0; i < EffectCount(source); i++)
        {
            var effect = EffectAt(source, i);
            if (!Applies(source, effect, trigger, card)) continue;
            if (effect.kind == flatKind) flat += effect.amount;
            else if (effect.kind == multiplierKind) multiplier = Mathf.Max(1, multiplier * Mathf.Max(1, effect.amount));
        }
    }

    public static int ModifyWithSingleArtifact(RelicData artifact, CardInstance card, int value, bool defense)
    {
        if (artifact == null) return value;
        int flat = 0, multiplier = 1;
        Accumulate(new GameplayEffectSource(new ArtifactRuntimeInstance(artifact)), card,
            defense ? GameplayEffectTrigger.DefenseCalculated : GameplayEffectTrigger.AttackCalculated,
            defense ? GameplayEffectKind.FlatBlock : GameplayEffectKind.FlatAttack,
            defense ? GameplayEffectKind.BlockMultiplier : GameplayEffectKind.AttackMultiplier,
            ref flat, ref multiplier);
        return Mathf.Max(0, value * multiplier + flat);
    }

    public static void EnqueueCardCommitted(CardCommittedEffectContext context, CombatReactionQueue queue)
    {
        for (int i = 0; i < context.Cards.ownedArtifacts.Count; i++)
        {
            var artifact = context.Cards.ownedArtifacts[i];
            if (artifact != null)
                EnqueueCommittedSource(new GameplayEffectSource(context.Cards.GetArtifactInstance(artifact)),
                    context, queue, i);
        }
        if (context.Card.Enhancement != null)
            EnqueueCommittedSource(new GameplayEffectSource(context.Card), context, queue, 0);
    }

    static void EnqueueCommittedSource(GameplayEffectSource source, CardCommittedEffectContext context,
        CombatReactionQueue queue, int sourceOrder)
    {
        for (int i = 0; i < EffectCount(source); i++)
        {
            var effect = EffectAt(source, i);
            if (!Applies(source, effect, GameplayEffectTrigger.CardCommitted, context.Card)) continue;
            int effectIndex = i;
            queue.Enqueue(context.Action.ActionId, context.HitIndex, CombatReactionPhase.CardCommitted,
                source.Category, sourceOrder, effectIndex,
                () => ApplyReactive(source, effect, context.Combat, context.Cards, context.Card, context.Target));
        }
    }

    public static void EnqueuePlayerTurnStart(PlayerTurnStartEffectContext context, CombatReactionQueue queue)
    {
        for (int i = 0; i < context.Cards.ownedArtifacts.Count; i++)
        {
            var artifact = context.Cards.ownedArtifacts[i];
            if (artifact != null)
                EnqueueTurnSource(new GameplayEffectSource(context.Cards.GetArtifactInstance(artifact)),
                    null, context, queue, i);
        }
        // Snapshot hand order and identity at the authoritative turn-start boundary.
        for (int i = 0; i < context.Cards.hand.Count; i++)
        {
            var card = context.Cards.hand[i];
            if (card?.Enhancement != null)
                EnqueueTurnSource(new GameplayEffectSource(card), card, context, queue, i);
        }
    }

    static void EnqueueTurnSource(GameplayEffectSource source, CardInstance card,
        PlayerTurnStartEffectContext context, CombatReactionQueue queue, int sourceOrder)
    {
        for (int i = 0; i < EffectCount(source); i++)
        {
            var effect = EffectAt(source, i);
            if (!Applies(source, effect, GameplayEffectTrigger.PlayerTurnStart, card)) continue;
            int effectIndex = i;
            queue.Enqueue(context.Action.ActionId, -1, CombatReactionPhase.PlayerTurnStarted,
                source.Category, sourceOrder, effectIndex,
                () => ApplyReactive(source, effect, context.Combat, context.Cards, card, null));
        }
    }

    public static (int bonusGold, int heal) EncounterWon(CardManager cards)
    {
        int gold = 0, heal = 0;
        for (int i = 0; i < cards.ownedArtifacts.Count; i++)
        {
            var artifact = cards.ownedArtifacts[i];
            if (artifact == null) continue;
            var source = new GameplayEffectSource(cards.GetArtifactInstance(artifact));
            for (int j = 0; j < EffectCount(source); j++)
            {
                var effect = EffectAt(source, j);
                if (effect.trigger != GameplayEffectTrigger.EncounterWon || !effect.Matches(null)) continue;
                if (effect.kind == GameplayEffectKind.BonusGold) gold += effect.amount;
                else if (effect.kind == GameplayEffectKind.Heal) heal += effect.amount;
            }
        }
        return (gold, heal);
    }

    static void ApplyReactive(GameplayEffectSource source, GameplayEffectDefinition effect,
        CombatManager combat, CardManager cards, CardInstance card, EnemyRuntime target)
    {
        int amount = Mathf.Max(0, effect.amount);
        switch (effect.kind)
        {
            case GameplayEffectKind.Heal:
                int healed = combat.HealPlayer(amount);
                if (healed > 0) combat.LogEffect($"{source.DisplayName}: Healed {healed} HP.");
                break;
            case GameplayEffectKind.Draw:
                int drawn = cards.DrawToHand(amount);
                if (drawn > 0) combat.LogEffect($"{source.DisplayName}: Drew {drawn} card(s).");
                break;
            case GameplayEffectKind.BonusGold:
                cards.AddGold(amount);
                break;
            case GameplayEffectKind.ReduceAttackByCardValue:
                if (target == null || card == null) break;
                target.ReduceAttack(card.BaseAttackValue);
                combat.LogEffect($"{source.DisplayName}: Enemy ATK reduced by {card.BaseAttackValue} to {target.currentAttack}!");
                break;
            case GameplayEffectKind.RecycleDiscardByCardValue:
                if (card == null) break;
                int returned = cards.ReturnRandomDiscardToDeck(card.BaseAttackValue);
                if (returned > 0) combat.LogEffect($"{source.DisplayName}: {returned} cards returned to deck!");
                break;
            case GameplayEffectKind.DrawByCardValue:
                if (card == null) break;
                int drawnByValue = cards.DrawToHand(card.BaseAttackValue);
                if (drawnByValue > 0) combat.LogEffect($"{source.DisplayName}: Drew {drawnByValue} cards!");
                break;
        }
    }
}
