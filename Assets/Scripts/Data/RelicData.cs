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
    OverflowRare = 12,
    ClubEpic = 13,
    OverflowEpic = 14,
    ChallengerCrest = 15,
    BountyLedger = 16,
    GildedBlade = 17,
    TravelerPackCommon = 18,
    TravelerPackRare = 19,
    PreparationManual = 20,
    ScavengersPouch = 21,
    ScavengersSatchel = 22,
    CashbackToken = 23,
    GoldenVault = 24,
    MerchantsBadge = 25,
    MerchantsGift = 26,
    AlchemistsKit = 27,
    FieldMedicsKit = 28,
    RetaliationEpic = 29,
    CrownOfEndurance = 30,
    HuntersLedger = 31,
    TrophyRack = 32,
    DwarfRare = 33,
    DwarfEpic = 34,
    BalancersScale = 35,
    KingslayersMark = 36,
    AceRare = 37,
    AceEpic = 38,
    DiamondEpic = 39,
    Hammer = 40,
    ArsenalEpic = 41,
    HeartEpic = 42,
    MimicEpic = 43,
    HandsEpic = 44,
    SpadeRare = 45,
    SpadeEpic = 46,
    HiddenTrail = 47,
    WanderersBoots = 48,
    WarpathBanner = 49,
    MasterThief = 50
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
