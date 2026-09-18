using System;
using UnityEngine;

public enum RunEffectType
{
    GainGold,
    LoseGold,
    Heal,
    LoseHealth,
    GainMaxHealth,
    DrawCards
}

[Serializable]
public struct RunEffect
{
    public RunEffectType type;
    [Min(0)] public int amount;
}

[Serializable]
public class RunEventChoice
{
    public string label;
    [TextArea] public string description;
    public RunEffect[] effects;
}

[CreateAssetMenu(fileName = "RunEvent", menuName = "Game/Run Event")]
public class RunEventDefinition : ScriptableObject
{
    public string id;
    public MapNodeType category = MapNodeType.Event;
    public string title;
    [TextArea] public string description;
    public RunEventChoice[] choices;
}
