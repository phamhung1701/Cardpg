using UnityEngine;

[CreateAssetMenu(fileName = "Artifact", menuName = "Game/Artifact Data")]
public class RelicData : ScriptableObject
{
    public string id;
    public string displayName;
    public string icon;
    [TextArea] public string description;
    [Min(0)] public int price;

    [Header("Card Filter")]
    public bool restrictToSuit;
    public CardData.Suit affectedSuit;
    public bool requiresEnhancedCard;

    [Header("Card Hooks")]
    [Min(1)] public int damageMultiplier = 1;
    public int flatDamageBonus;
    public int defenseBonus;
    public bool reduceEnemyAttackByCardValue;
    public bool recycleDiscardByCardValue;
    public bool drawByCardValue;

    [Header("Encounter Hooks")]
    [Min(0)] public int healAfterVictory;
    [Min(0)] public int bonusGold;

    public bool Matches(CardInstance card)
    {
        if (card == null) return false;
        if (restrictToSuit && card.Suit != affectedSuit) return false;
        return !requiresEnhancedCard || card.Enhancement != null;
    }

    public int ModifyAttack(CardInstance card, int currentDamage)
    {
        if (!Matches(card)) return currentDamage;
        return Mathf.Max(0, currentDamage * Mathf.Max(1, damageMultiplier) + flatDamageBonus);
    }

    public int ModifyDefense(CardInstance card, int currentDefense)
    {
        return Matches(card) ? Mathf.Max(0, currentDefense + defenseBonus) : currentDefense;
    }
}
