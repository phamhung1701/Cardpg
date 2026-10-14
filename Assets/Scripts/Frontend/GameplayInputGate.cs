using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[Flags]
public enum GameplayInputBlockReason
{
    None = 0,
    FrontendMenu = 1 << 0,
    RunResult = 1 << 1,
    ConsumableReward = 1 << 2,
    GearChoice = 1 << 3
}

public static class GameplayInputGate
{
    sealed class OwnerComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }

    static readonly Dictionary<object, GameplayInputBlockReason> _ownedReasons =
        new(new OwnerComparer());
    static GameplayInputBlockReason _reasons;

    public static bool IsBlocked => Reasons != GameplayInputBlockReason.None;
    public static GameplayInputBlockReason Reasons
    {
        get
        {
            var result = _reasons;
            foreach (var owned in _ownedReasons.Values) result |= owned;
            return result;
        }
    }

    /// <summary>Sets reasons owned by a specific system without clearing another system's reasons.</summary>
    public static void SetOwned(object owner, GameplayInputBlockReason reason, bool blocked)
    {
        if (owner == null || reason == GameplayInputBlockReason.None) return;
        _ownedReasons.TryGetValue(owner, out var current);
        current = blocked ? current | reason : current & ~reason;
        if (current == GameplayInputBlockReason.None) _ownedReasons.Remove(owner);
        else _ownedReasons[owner] = current;
    }

    public static void Set(GameplayInputBlockReason reason, bool blocked)
    {
        if (blocked) _reasons |= reason;
        else _reasons &= ~reason;
    }

    public static void Clear()
    {
        _reasons = GameplayInputBlockReason.None;
        _ownedReasons.Clear();
    }
}
