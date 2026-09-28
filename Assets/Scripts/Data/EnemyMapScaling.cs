using System;

/// <summary>Map 1–12 scaling of authored enemy HP and ATK. Map indices are zero-based.</summary>
public static class EnemyMapScaling
{
    public static (int hp, int attack) Scale(int baseHp, int baseAttack, int mapIndex)
    {
        int index = Math.Min(11, Math.Max(0, mapIndex));
        int hp = (int)decimal.Round(baseHp * (1m + 0.2m * index), 0, MidpointRounding.AwayFromZero);
        int attack = (int)decimal.Round(baseAttack * (1m + 0.07m * index), 0, MidpointRounding.AwayFromZero);
        return (hp, attack);
    }
}
