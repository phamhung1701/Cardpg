using System;

public enum RunContractObjective
{
    WinTwoEncounters,
    DefeatEliteThisMap,
    WinCombatWithoutHpLoss
}

[Serializable]
public sealed class RunContractState
{
    public RunContractObjective objective;
    public int startedAtMapIndex;
    public int startedAtNodeId;
    public int deadlineMapExclusive;
    public int encounterWins;
    public string stableId;

    public string ObjectiveDescription => objective switch
    {
        RunContractObjective.WinTwoEncounters => "Win 2 encounters within 12 maps.",
        RunContractObjective.DefeatEliteThisMap => "Defeat an Elite before leaving this map.",
        RunContractObjective.WinCombatWithoutHpLoss => "Win 1 encounter without losing HP.",
        _ => "Complete the contract objective."
    };
}
