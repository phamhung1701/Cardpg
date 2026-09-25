public enum CombatActionOrigin
{
    PlayerCard,
    EnemyRetaliation,
    Recovery,
    Reactive,
    Legacy
}

public sealed class CombatActionContext
{
    public long ActionId { get; }
    public CombatActionOrigin Origin { get; }
    public PlayerRuntime SourcePlayer { get; }
    public EnemyRuntime SourceEnemy { get; }
    public CardInstance Card { get; }
    public EnemyRuntime TargetEnemy { get; }
    public CardData.Suit? SuitSnapshot { get; }
    public CardData.Rank? RankSnapshot { get; }
    public int HitCount { get; }

    public CombatActionContext(
        long actionId,
        CombatActionOrigin origin,
        PlayerRuntime sourcePlayer = null,
        EnemyRuntime sourceEnemy = null,
        CardInstance card = null,
        EnemyRuntime targetEnemy = null,
        int hitCount = 1)
    {
        ActionId = actionId;
        Origin = origin;
        SourcePlayer = sourcePlayer;
        SourceEnemy = sourceEnemy;
        Card = card;
        TargetEnemy = targetEnemy;
        SuitSnapshot = card != null ? card.Suit : null;
        RankSnapshot = card != null ? card.Rank : null;
        HitCount = UnityEngine.Mathf.Max(1, hitCount);
    }
}
