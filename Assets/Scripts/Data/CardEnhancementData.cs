using UnityEngine;

[CreateAssetMenu(fileName = "CardEnhancement", menuName = "Game/Card Enhancement")]
public sealed class CardEnhancementData : ScriptableObject
{
    public string id;
    public string displayName;
    public string icon;
    [TextArea] public string description;
    [Min(0)] public int price;

    [Header("Card Hooks")]
    public int attackBonus;
    public int defenseBonus;
    [Min(0)] public int healOnPlay;
    [Min(0)] public int drawOnPlay;
}
