using System;

[Flags]
public enum GameplayInputBlockReason
{
    None = 0,
    FrontendMenu = 1 << 0,
    RunResult = 1 << 1,
    ConsumableReward = 1 << 2,
    ArtifactChoice = 1 << 3
}

public static class GameplayInputGate
{
    static GameplayInputBlockReason _reasons;

    public static bool IsBlocked => _reasons != GameplayInputBlockReason.None;
    public static GameplayInputBlockReason Reasons => _reasons;

    public static void Set(GameplayInputBlockReason reason, bool blocked)
    {
        if (blocked) _reasons |= reason;
        else _reasons &= ~reason;
    }

    public static void Clear() => _reasons = GameplayInputBlockReason.None;
}
