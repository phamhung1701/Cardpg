using System;

/// <summary>A single backpack slot; charges belong to the acquired item, not its shared definition.</summary>
public sealed class ConsumableInstance
{
    public ConsumableData Definition { get; }
    public int RemainingCharges { get; private set; }

    public ConsumableInstance(ConsumableData definition)
    {
        Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
        RemainingCharges = Math.Max(1, definition.uses);
    }

    public bool SpendCharge()
    {
        if (RemainingCharges <= 0) return false;
        RemainingCharges--;
        return true;
    }
}
