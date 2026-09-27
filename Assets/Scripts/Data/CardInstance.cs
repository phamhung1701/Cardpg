using System;
using UnityEngine;

[Serializable]
public sealed class CardInstance
{
    [SerializeField] CardData definition;
    [SerializeField] CardEnhancementData enhancement;
    [SerializeField] bool hasSuitOverride;
    [SerializeField] CardData.Suit suitOverride;
    [SerializeField] int id;

    public CardInstance(CardData definition, int id)
    {
        this.definition = definition != null
            ? definition
            : throw new ArgumentNullException(nameof(definition));
        this.id = id;
    }

    public CardData Definition => definition;
    public CardEnhancementData Enhancement => enhancement;
    public int Id => id;

    public CardData.Suit Suit => hasSuitOverride ? suitOverride : definition.suit;
    public bool HasSuitOverride => hasSuitOverride;
    public CardData.Rank Rank => definition.rank;

    // Compatibility forwards for existing gameplay code while it moves to the model API.
    public CardData.Suit suit => Suit;
    public CardData.Rank rank => Rank;

    public GameplayEffectState EffectState { get; } = new();
    public int PermanentAttackBonus { get; private set; }

    public bool MatchesSuit(CardData.Suit suit) =>
        Suit == suit || GameplayEffectResolver.HasEnhancementEffect(this, GameplayEffectKind.WildSuit);

    public void GainPermanentAttackBonus(int amount = 1)
    {
        if (amount > 0) PermanentAttackBonus = (int)Math.Min(int.MaxValue, (long)PermanentAttackBonus + amount);
    }

    public int BaseAttackValue => definition.AttackValue;
    public int AttackValue => Mathf.Max(0, BaseAttackValue + GameplayEffectResolver.CardLocalFlat(this, GameplayEffectKind.FlatAttack));
    public int DefenseValue => Mathf.Max(0, BaseAttackValue + GameplayEffectResolver.CardLocalFlat(this, GameplayEffectKind.FlatBlock));
    public string DisplayName => enhancement != null
        ? $"{definition.RankLabel}{SuitSymbol} [{enhancement.displayName}]"
        : $"{definition.RankLabel}{SuitSymbol}";
    public string SuitSymbol => Suit switch
    {
        CardData.Suit.Hearts => "♥",
        CardData.Suit.Diamonds => "♦",
        CardData.Suit.Clubs => "♣",
        CardData.Suit.Spades => "♠",
        _ => ""
    };

    public bool TryChangeSuit(CardData.Suit suit)
    {
        if (!Enum.IsDefined(typeof(CardData.Suit), suit) || Suit == suit) return false;
        hasSuitOverride = suit != definition.suit;
        suitOverride = suit;
        return true;
    }

    public CardInstance ClonePermanentState(int newId)
    {
        var copy = new CardInstance(definition, newId)
        {
            hasSuitOverride = hasSuitOverride,
            suitOverride = suitOverride,
            enhancement = enhancement,
            PermanentAttackBonus = PermanentAttackBonus
        };
        return copy;
    }

    public bool TryApplyEnhancement(CardEnhancementData value)
    {
        if (value == null || enhancement != null) return false;
        enhancement = value;
        return true;
    }
}
