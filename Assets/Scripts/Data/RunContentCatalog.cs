using UnityEngine;

[CreateAssetMenu(fileName = "RunContentCatalog", menuName = "Game/Run Content Catalog")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "RunContentCatalog")]
public class RunContentCatalog : ScriptableObject
{
    [Header("Encounters")]
    public EnemyTypeData[] normalEnemies;
    [Tooltip("Authored elite bases; do not apply the legacy generic elite bonuses.")]
    public EnemyTypeData[] eliteEnemies;
    public EnemyAbility eliteAbility;

    [Header("Headliner Suit Abilities")]
    [UnityEngine.Serialization.FormerlySerializedAs("clubsBossAbility")]
    public EnemyAbility clubsHeadlinerAbility;
    [UnityEngine.Serialization.FormerlySerializedAs("diamondsBossAbility")]
    public EnemyAbility diamondsHeadlinerAbility;
    [UnityEngine.Serialization.FormerlySerializedAs("heartsBossAbility")]
    public EnemyAbility heartsHeadlinerAbility;
    [UnityEngine.Serialization.FormerlySerializedAs("spadesBossAbility")]
    public EnemyAbility spadesHeadlinerAbility;

    [Header("Route Content")]
    public RunEventDefinition[] events;
    public RunEventDefinition[] upgrades;
    public RunEventDefinition[] risks;

    public EnemyAbility GetHeadlinerAbility(CardData.Suit suit) => suit switch
    {
        CardData.Suit.Clubs => clubsHeadlinerAbility,
        CardData.Suit.Diamonds => diamondsHeadlinerAbility,
        CardData.Suit.Hearts => heartsHeadlinerAbility,
        _ => spadesHeadlinerAbility
    };

    public RunEventDefinition[] GetEvents(MapNodeType category) => category switch
    {
        MapNodeType.Upgrade => upgrades,
        MapNodeType.Risk => risks,
        _ => events
    };
}
