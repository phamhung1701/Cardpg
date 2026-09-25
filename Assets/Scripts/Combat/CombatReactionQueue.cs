using System;
using System.Collections.Generic;
using UnityEngine;

public enum CombatReactionPhase
{
    EncounterStarted,
    CardCommitted,
    HitResolved,
    CardResolved,
    EnemyDefeated,
    EncounterResolved,
    PlayerTurnStarted
}

public enum CombatReactionSourceCategory
{
    Core,
    Artifact,
    Enhancement,
    EnemyAbility
}

public sealed class CombatReactionQueue
{
    public const int DefaultOperationLimit = 256;

    readonly List<Operation> _operations = new();
    readonly HashSet<OperationKey> _scheduledKeys = new();
    readonly int _operationLimit;
    int _processedCount;

    public CombatReactionQueue(int operationLimit = DefaultOperationLimit)
    {
        _operationLimit = Mathf.Max(1, operationLimit);
    }

    public int PendingCount => _operations.Count;
    public int ProcessedCount => _processedCount;
    public bool RunawayDetected { get; private set; }

    public bool Enqueue(
        long actionId,
        int hitIndex,
        CombatReactionPhase phase,
        CombatReactionSourceCategory sourceCategory,
        int sourceOrder,
        int handlerOrder,
        Action execute,
        int priority = 0)
    {
        if (execute == null) return false;
        var key = new OperationKey(actionId, hitIndex, phase, sourceCategory, sourceOrder, handlerOrder, priority);
        if (!_scheduledKeys.Add(key)) return false;

        _operations.Add(new Operation(key, execute));
        return true;
    }

    public bool ProcessPhase(CombatReactionPhase phase, Func<bool> stopCondition = null)
    {
        while (true)
        {
            if (stopCondition != null && stopCondition())
            {
                _operations.Clear();
                return true;
            }

            if (!TryTakeNext(phase, out var operation))
                break;

            if (_processedCount >= _operationLimit)
            {
                RunawayDetected = true;
                _operations.Clear();
                Debug.LogError($"Combat reaction limit ({_operationLimit}) exceeded. Remaining reactions were discarded.");
                return false;
            }

            _processedCount++;
            operation.Execute();
        }
        return !RunawayDetected;
    }

    public void Clear()
    {
        _operations.Clear();
        _scheduledKeys.Clear();
        _processedCount = 0;
        RunawayDetected = false;
    }

    bool TryTakeNext(CombatReactionPhase phase, out Operation result)
    {
        int bestIndex = -1;
        for (int i = 0; i < _operations.Count; i++)
        {
            if (_operations[i].Key.Phase != phase) continue;
            if (bestIndex < 0 || Compare(_operations[i].Key, _operations[bestIndex].Key) < 0)
                bestIndex = i;
        }

        if (bestIndex < 0)
        {
            result = default;
            return false;
        }

        result = _operations[bestIndex];
        _operations.RemoveAt(bestIndex);
        return true;
    }

    static int Compare(OperationKey left, OperationKey right)
    {
        int value = left.Phase.CompareTo(right.Phase);
        if (value != 0) return value;
        value = left.SourceCategory.CompareTo(right.SourceCategory);
        if (value != 0) return value;
        value = left.SourceOrder.CompareTo(right.SourceOrder);
        if (value != 0) return value;
        value = left.HandlerOrder.CompareTo(right.HandlerOrder);
        if (value != 0) return value;
        // Explicit priority is a tie-breaker within the same authored handler position.
        value = left.Priority.CompareTo(right.Priority);
        if (value != 0) return value;
        value = left.HitIndex.CompareTo(right.HitIndex);
        if (value != 0) return value;
        return left.ActionId.CompareTo(right.ActionId);
    }

    readonly struct Operation
    {
        public OperationKey Key { get; }
        public Action Execute { get; }

        public Operation(OperationKey key, Action execute)
        {
            Key = key;
            Execute = execute;
        }
    }

    readonly struct OperationKey : IEquatable<OperationKey>
    {
        public long ActionId { get; }
        public int HitIndex { get; }
        public CombatReactionPhase Phase { get; }
        public CombatReactionSourceCategory SourceCategory { get; }
        public int SourceOrder { get; }
        public int HandlerOrder { get; }
        public int Priority { get; }

        public OperationKey(
            long actionId,
            int hitIndex,
            CombatReactionPhase phase,
            CombatReactionSourceCategory sourceCategory,
            int sourceOrder,
            int handlerOrder,
            int priority)
        {
            ActionId = actionId;
            HitIndex = hitIndex;
            Phase = phase;
            SourceCategory = sourceCategory;
            SourceOrder = sourceOrder;
            HandlerOrder = handlerOrder;
            Priority = priority;
        }

        public bool Equals(OperationKey other)
        {
            return ActionId == other.ActionId &&
                HitIndex == other.HitIndex &&
                Phase == other.Phase &&
                SourceCategory == other.SourceCategory &&
                SourceOrder == other.SourceOrder &&
                HandlerOrder == other.HandlerOrder;
        }

        public override bool Equals(object obj) => obj is OperationKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ActionId.GetHashCode();
                hash = hash * 31 + HitIndex;
                hash = hash * 31 + (int)Phase;
                hash = hash * 31 + (int)SourceCategory;
                hash = hash * 31 + SourceOrder;
                hash = hash * 31 + HandlerOrder;
                return hash;
            }
        }
    }
}
