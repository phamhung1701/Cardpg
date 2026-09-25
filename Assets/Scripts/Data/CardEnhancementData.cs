using UnityEngine;

[CreateAssetMenu(fileName = "CardEnhancement", menuName = "Game/Card Enhancement")]
public sealed class CardEnhancementData : ScriptableObject
{
    public string id;
    public string displayName;
    public string icon;
    [TextArea] public string description;
    [Min(0)] public int price;

    [Header("Canonical Content Metadata")]
    public string canonicalId;
    public string rarity = "Common";
    [Min(1)] public int tier = 1;
    public string upgradeFromId;

    [Header("Typed Gameplay Effects")]
    public GameplayEffectDefinition[] effects = System.Array.Empty<GameplayEffectDefinition>();
    public GameplayRuleModifierData[] ruleModifiers = System.Array.Empty<GameplayRuleModifierData>();

    [Header("Legacy Prototype Hooks (used only without typed effects)")]
    public int attackBonus;
    public int defenseBonus;
    [Min(0)] public int healOnPlay;
    [Min(0)] public int drawOnPlay;
}
