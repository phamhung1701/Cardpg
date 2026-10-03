using UnityEngine;

[CreateAssetMenu(fileName = "RunContentCatalog", menuName = "Game/Run Content Catalog")]
public class RunContentCatalog : ScriptableObject
{
    [Header("Encounters")]
    public EnemyTypeData[] normalEnemies;
    [Tooltip("Authored elite bases; do not apply the legacy generic elite bonuses.")]
    public EnemyTypeData[] eliteEnemies;
    public EnemyAbility eliteAbility;

    [Header("Boss Suit Abilities")]
    public EnemyAbility clubsBossAbility;
    public EnemyAbility diamondsBossAbility;
    public EnemyAbility heartsBossAbility;
    public EnemyAbility spadesBossAbility;

    [Header("Route Content")]
    public RunEventDefinition[] events;
    public RunEventDefinition[] upgrades;
    public RunEventDefinition[] risks;

    public EnemyAbility GetBossAbility(CardData.Suit suit) => suit switch
    {
        CardData.Suit.Clubs => clubsBossAbility,
        CardData.Suit.Diamonds => diamondsBossAbility,
        CardData.Suit.Hearts => heartsBossAbility,
        _ => spadesBossAbility
    };

    public RunEventDefinition[] GetEvents(MapNodeType category) => category switch
    {
        MapNodeType.Upgrade => upgrades,
        MapNodeType.Risk => risks,
        _ => events
    };
}
