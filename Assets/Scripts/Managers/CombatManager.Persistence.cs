using System;

public partial class CombatManager
{
    public bool CanCapturePersistentState => !_isResolvingAction && !_reactionFaulted &&
        !_overflowRootActive && _pendingDiamondDrawChoices == 0 &&
        currentState is GameState.Idle or GameState.GameWon && player != null && !player.IsDefeated;

    internal void CapturePersistentState(RunSaveData data)
    {
        if (!CanCapturePersistentState) throw new InvalidOperationException("Combat is not at a save checkpoint.");
        data.player = new PlayerSaveData { configuredMaxHealth = _configuredPlayerMaxHealth,
            maxHealth = player.maxHealth, health = player.currentHealth, shield = player.ShieldCharges, block = player.EncounterBlock };
        data.criticalRandom = RandomState(_criticalRandom);
        data.duelistRandom = RandomState(_duelistSuitRandom);
        data.eliteKillCount = _eliteKillCount; data.nextActionId = _nextActionId;
        data.criticalChance = criticalChancePercent;
    }

    static uint RandomState(IRandomSource source) => source is DeterministicRandom random
        ? random.State : throw new InvalidOperationException("Unsupported saved random source.");

    internal void RestorePersistentState(RunSaveData data)
    {
        Reset();
        _configuredPlayerMaxHealth = data.player.configuredMaxHealth;
        player.RestorePersistentState(data.player.maxHealth, data.player.health, data.player.shield, data.player.block);
        _criticalRandom = DeterministicRandom.Restore(data.criticalRandom);
        _duelistSuitRandom = DeterministicRandom.Restore(data.duelistRandom);
        _eliteKillCount = data.eliteKillCount; _nextActionId = data.nextActionId;
        criticalChancePercent = data.criticalChance;
        // Encounter-local actions, queues and targets remain reset. Continue resumes the saved node UI, not a turn.
        NotifyPlayerHealthChanged();
    }
}
