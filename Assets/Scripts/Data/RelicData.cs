using UnityEngine;

public enum ArtifactCapacityCategory
{
    Persistent = 0,
    NonSlot = 1
}

public enum ArtifactSpecialRule
{
    None = 0,
    RareClub = 1,
    RareHeart = 2,
    RareDiamond = 3,
    RareArsenal = 4,
    GlassCommon = 5,
    GlassRare = 6,
    GlassEpic = 7,
    RareRetaliation = 8,
    RareHands = 9,
    RoyalFamilyHeirloom = 10,
    OverflowCommon = 11,
    OverflowRare = 12
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
    public ArtifactSpecialRule specialRule;

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
        if (restrictToSuit && !card.MatchesSuit(affectedSuit)) return false;
        return !requiresEnhancedCard || card.Enhancement != null;
    }

    public int ModifyAttack(CardInstance card, int currentDamage) =>
        GameplayEffectResolver.ModifyWithSingleArtifact(this, card, currentDamage, false);

    public int ModifyDefense(CardInstance card, int currentDefense) =>
        GameplayEffectResolver.ModifyWithSingleArtifact(this, card, currentDefense, true);
}
