using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Versioned observation journal. Never consumes gameplay RNG or restores gameplay state.</summary>
[Serializable]
public sealed class BalanceJournal
{
    public int schemaVersion = 1;
    public string id = Guid.NewGuid().ToString("N");
    public string seed, unityVersion, gameVersion, startedUtc;
    public string outcome = "in_progress";
    public bool truncated, developmentModified, invalid;
    public List<BalanceEvent> events = new();
    public const int EventLimit = 100000;

    public void Add(BalanceEvent entry)
    {
        if (events.Count >= EventLimit) { truncated = true; return; }
        events.Add(entry);
    }

    public string ToJson() => JsonUtility.ToJson(this, true);

    public string Export(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, id + ".json");
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, ToJson());
        if (File.Exists(path)) File.Replace(temporary, path, null);
        else File.Move(temporary, path);
        return path;
    }
}

[Serializable]
public sealed class BalanceEvent
{
    public string kind, detail;
    public double seconds;
    public int encounter, map, node, a, b, c, d;
}

public static class GameplayBalanceTelemetry
{
    // Opt-in; no assets, save schemas, or balance configuration are changed.
    public static bool Enabled { get; set; }
    public static BalanceJournal Current { get; private set; }
    public static string LastExportPath { get; private set; }
    static int encounter;
    static bool encounterOpen;
    static double started;
    static int terminalFrame;
    public static string ExportDirectory => Path.Combine(Application.persistentDataPath, "BalanceTelemetry");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        Current = null;
        encounter = 0;
        encounterOpen = false;
        LastExportPath = null;
    }

    public static void Begin(string seed)
    {
        Finish("restarted");
        if (!Enabled) { Current = null; return; }
        Current = new BalanceJournal { seed = seed, unityVersion = Application.unityVersion, gameVersion = Application.version,
            startedUtc = DateTime.UtcNow.ToString("O") };
        started = Time.timeAsDouble;
        encounter = 0;
        encounterOpen = false;
        Record("run_start");
        var combat = CombatManager.Instance;
        if (combat != null) Health(combat.player.currentHealth, combat.player.maxHealth);
    }

    public static void Record(string kind, string detail = "", int a = 0, int b = 0, int c = 0, int d = 0)
    {
        if (Current == null || Current.outcome != "in_progress") return;
        Current.developmentModified |= DevModeRuntime.Enabled;
        var run = RunManager.Instance;
        Current.Add(new BalanceEvent { kind = kind, detail = detail, a = a, b = b, c = c, d = d,
            seconds = Time.timeAsDouble - started, encounter = encounterOpen ? encounter : 0,
            map = run != null ? run.CurrentMapIndex : -1, node = run?.ActiveNode?.id ?? -1 });
    }

    public static void EncounterStarted(IReadOnlyList<EnemyRuntime> enemies, MapNodeType kind)
    {
        if (Current == null || Current.outcome != "in_progress") return;
        encounter++;
        encounterOpen = true;
        Record("encounter_start", kind.ToString());
        foreach (var enemy in enemies)
            Record("enemy", enemy.DisplayName, enemy.currentHp, enemy.EffectiveAttack);
        var cards = CardManager.Instance;
        if (cards != null)
            foreach (var gear in cards.ownedGear)
                if (gear != null) Record("gear_exposure", string.IsNullOrEmpty(gear.canonicalId) ? gear.name : gear.canonicalId);
    }

    public static void Invalidate(string reason)
    {
        if (Current == null || Current.outcome != "in_progress") return;
        Current.invalid = true;
        Record("invalid", reason);
    }

    public static void Damage(DamageResult result)
    {
        bool player = result.Request.Target is PlayerRuntime;
        Record(player ? "damage_received" : "damage_dealt", result.Request.Origin.ToString(),
            result.ActualHpLost, result.RequestedDamage, result.ShieldBlockedDamage, result.ModifierPreventedDamage);
        if (player && result.WasLethal) Record("lethal", result.Request.Origin.ToString());
    }

    public static void TrickUsed(string id, bool wasInEncounter)
    {
        if (Current == null) return;
        // A lethal Trick can finish the encounter/run before its charge is committed.
        if (Current.outcome != "in_progress" &&
            (!wasInEncounter || Time.frameCount != terminalFrame ||
             Current.outcome != "completed" && Current.outcome != "defeat")) return;
        var run = RunManager.Instance;
        Current.Add(new BalanceEvent { kind = "trick_used", detail = id,
            seconds = Time.timeAsDouble - started, encounter = wasInEncounter ? encounter : 0,
            map = run != null ? run.CurrentMapIndex : -1, node = run?.ActiveNode?.id ?? -1 });
        if (Current.outcome != "in_progress") TryExport();
    }

    public static void Health(int hp, int maxHp) => Record("hp", "", hp, maxHp);

    public static void EncounterEnded(EncounterResult result)
    {
        Record("encounter_end", result.ToString());
        encounterOpen = false;
        if (result == EncounterResult.Defeat) Finish("defeat");
    }

    public static void Finish(string outcome)
    {
        if (Current == null || Current.outcome != "in_progress") return;
        Record("run_end", outcome);
        Current.outcome = outcome;
        terminalFrame = Time.frameCount;
        encounterOpen = false;
        TryExport();
    }

    public static bool TryExport()
    {
        if (Current == null) return false;
        try { LastExportPath = Current.Export(ExportDirectory); return true; }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogWarning("Balance telemetry export failed; journal retained in memory: " + exception.Message);
            return false;
        }
    }
}
