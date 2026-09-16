using System;
using UnityEngine;

[Serializable]
public sealed class CardInstance
{
    [SerializeField] CardData definition;
    [SerializeField] int id;

    public CardInstance(CardData definition, int id)
    {
        this.definition = definition != null
            ? definition
            : throw new ArgumentNullException(nameof(definition));
        this.id = id;
    }

    public CardData Definition => definition;
    public int Id => id;

    public CardData.Suit Suit => definition.suit;
    public CardData.Rank Rank => definition.rank;

    // Compatibility forwards for existing gameplay code while it moves to the model API.
    public CardData.Suit suit => Suit;
    public CardData.Rank rank => Rank;

    public int AttackValue => definition.AttackValue;
    public string DisplayName => definition.DisplayName;
    public string SuitSymbol => definition.SuitSymbol;
}
