using UnityEngine;

[CreateAssetMenu(fileName = "Enemy", menuName = "Game/Enemy Type")]
public class EnemyTypeData : ScriptableObject
{
    public string enemyName;
    public int maxHp;
    public int baseAttack;
    public int goldReward;
    public CardData sourceCard;
    public EnemyAbility[] abilities;

    public static EnemyTypeData Create(string name, int hp, int atk, int gold)
    {
        var data = CreateInstance<EnemyTypeData>();
        data.enemyName = name;
        data.maxHp = hp;
        data.baseAttack = atk;
        data.goldReward = gold;
        return data;
    }
}
