using System;
using System.Collections.Generic;

/// <summary>One backpack slot containing individually charged copies of one identical consumable.</summary>
public sealed class ConsumableStack
{
    readonly List<ConsumableInstance> _items = new();

    public ConsumableData Definition { get; }
    public IReadOnlyList<ConsumableInstance> Items => _items;
    public int Count => _items.Count;

    public ConsumableStack(ConsumableData definition)
    {
        Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
    }

    public void Add(ConsumableInstance item)
    {
        if (item == null || item.Definition != Definition)
            throw new ArgumentException("A consumable stack accepts only identical definitions.", nameof(item));
        _items.Add(item);
    }

    public ConsumableInstance GetPartiallyUsedFirst()
    {
        ConsumableInstance selected = null;
        for (int i = 0; i < _items.Count; i++)
        {
            var candidate = _items[i];
            if (candidate == null || candidate.RemainingCharges <= 0) continue;
            if (selected == null || candidate.RemainingCharges < selected.RemainingCharges)
                selected = candidate;
        }
        return selected;
    }

    public bool Remove(ConsumableInstance item) => item != null && _items.Remove(item);
}
