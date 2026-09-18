using UnityEngine;

[CreateAssetMenu(fileName = "Enemy", menuName = "Game/Enemy Type")]
public class EnemyTypeData : ScriptableObject
{
    public string enemyName;
    public int maxHp;
    public int baseAttack;
    public int goldReward;
    [Min(1)] public int encounterCount = 1;
    [Min(0)] public int fleeAfterPlayerTurns;
    public CardData sourceCard;
    public EnemyAbility[] abilities;

    public static EnemyTypeData Create(string name, int hp, int atk, int gold)
    {
        var data = CreateInstance<EnemyTypeData>();
        data.enemyName = name;
        data.maxHp = hp;
        data.baseAttack = atk;
        data.goldReward = gold;
        data.encounterCount = 1;
        return data;
    }
}
