using UnityEngine;

[CreateAssetMenu(fileName = "Enemy", menuName = "Game/Enemy Type")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "EnemyTypeData")]
public class EnemyTypeData : ScriptableObject
{
    public string enemyName;
    public int maxHp;
    public int baseAttack;
    [UnityEngine.Serialization.FormerlySerializedAs("goldReward")]
    public int creditsReward;
    [Min(1)] public int encounterCount = 1;
    [Min(0)] public int fleeAfterPlayerTurns;
    public CardData sourceCard;
    [Tooltip("Optional authored portrait. Headliner face-card identity continues to use sourceCard.")]
    public Sprite portraitSprite;
    public EnemyAbility[] abilities;

    public static EnemyTypeData Create(string name, int hp, int atk, int credits)
    {
        var data = CreateInstance<EnemyTypeData>();
        data.enemyName = name;
        data.maxHp = hp;
        data.baseAttack = atk;
        data.creditsReward = credits;
        data.encounterCount = 1;
        return data;
    }
}
