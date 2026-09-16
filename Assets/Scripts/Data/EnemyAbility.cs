using UnityEngine;

public abstract class EnemyAbility : ScriptableObject
{
    public abstract void Execute(EnemyRuntime enemy, CombatManager context);
}
