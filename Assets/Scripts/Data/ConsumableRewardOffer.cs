using System;
using System.Collections.Generic;

/// <summary>A cached, explicit consumable reward that must be claimed, replaced, or declined.</summary>
public sealed class ConsumableRewardOffer
{
    readonly ConsumableData[] _choices;

    public string ContextId { get; }
    public string SourceLabel { get; }
    public IReadOnlyList<ConsumableData> Choices => _choices;

    public ConsumableRewardOffer(string contextId, string sourceLabel, IReadOnlyList<ConsumableData> choices)
    {
        ContextId = string.IsNullOrWhiteSpace(contextId)
            ? throw new ArgumentException("Reward context is required.", nameof(contextId))
            : contextId;
        SourceLabel = string.IsNullOrWhiteSpace(sourceLabel) ? "Consumable Reward" : sourceLabel;
        if (choices == null || choices.Count == 0)
            throw new ArgumentException("At least one reward choice is required.", nameof(choices));
        _choices = new ConsumableData[choices.Count];
        for (int i = 0; i < choices.Count; i++)
            _choices[i] = choices[i] != null
                ? choices[i]
                : throw new ArgumentException("Reward choices cannot contain null.", nameof(choices));
    }
}
