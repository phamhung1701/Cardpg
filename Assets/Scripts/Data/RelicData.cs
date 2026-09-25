using UnityEngine;

public enum ArtifactCapacityCategory
{
    Persistent = 0,
    NonSlot = 1
}

[CreateAssetMenu(fileName = "Artifact", menuName = "Game/Artifact Data")]
public class RelicData : ScriptableObject
{
    public string id;
    public string displayName;
    public string icon;
    [TextArea] public string description;
    [Min(0)] public int price;

    [Header("Capacity")]
    // Existing authored Artifacts default to Persistent; future temporary/system/quest
    // Artifacts may be marked NonSlot without relying on names or creating a new inventory.
    public ArtifactCapacityCategory capacityCategory = ArtifactCapacityCategory.Persistent;
    public bool OccupiesCapacitySlot => capacityCategory == ArtifactCapacityCategory.Persistent;

    [Header("Canonical Content Metadata")]
    public string canonicalId;
    public string rarity = "Common";
    [Min(1)] public int tier = 1;
    public string upgradeFromId;

    [Header("Typed Gameplay Effects")]
    public GameplayEffectDefinition[] effects = System.Array.Empty<GameplayEffectDefinition>();
    public GameplayRuleModifierData[] ruleModifiers = System.Array.Empty<GameplayRuleModifierData>();

    [Header("Legacy Prototype Hooks (used only without typed effects)")]
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

    public int ModifyAttack(CardInstance card, int currentDamage) =>
        GameplayEffectResolver.ModifyWithSingleArtifact(this, card, currentDamage, false);

    public int ModifyDefense(CardInstance card, int currentDefense) =>
        GameplayEffectResolver.ModifyWithSingleArtifact(this, card, currentDefense, true);
}
