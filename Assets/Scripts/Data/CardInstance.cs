using System;
using UnityEngine;

[Serializable]
public sealed class CardInstance
{
    [SerializeField] CardData definition;
    [SerializeField] CardEnhancementData enhancement;
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

    public CardData.Suit Suit => definition.suit;
    public CardData.Rank Rank => definition.rank;

    // Compatibility forwards for existing gameplay code while it moves to the model API.
    public CardData.Suit suit => Suit;
    public CardData.Rank rank => Rank;

    public int BaseAttackValue => definition.AttackValue;
    public int AttackValue => Mathf.Max(0, BaseAttackValue + (enhancement != null ? enhancement.attackBonus : 0));
    public int DefenseValue => Mathf.Max(0, BaseAttackValue + (enhancement != null ? enhancement.defenseBonus : 0));
    public string DisplayName => enhancement != null
        ? $"{definition.DisplayName} [{enhancement.displayName}]"
        : definition.DisplayName;
    public string SuitSymbol => definition.SuitSymbol;

    public bool TryApplyEnhancement(CardEnhancementData value)
    {
        if (value == null || enhancement != null) return false;
        enhancement = value;
        return true;
    }
}
