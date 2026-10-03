using System.Text;
using TMPro;
using UnityEngine;

public class RunStatusUI : MonoBehaviour
{
    [Header("Run Resources")]
    public TMP_Text goldLabel;
    public TMP_Text pileSummaryLabel;
    public TMP_Text runProgressLabel;
    public TMP_Text bossProgressLabel;
    public TMP_Text timingLabel;
    public TMP_Text ownedArtifactsLabel;
    public TMP_Text[] backpackSlotLabels = new TMP_Text[CardManager.BACKPACK_CAPACITY];

    void OnEnable()
    {
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnGoldChanged += HandleGoldChanged;
            CardManager.Instance.OnDeckChanged += Refresh;
            CardManager.Instance.OnBuildChanged += Refresh;
            CardManager.Instance.OnConsumablesChanged += Refresh;
        }

        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnCycleStarted += HandleCycleStarted;
            RunManager.Instance.OnShowPathScreen += Refresh;
            RunManager.Instance.OnHidePathScreen += Refresh;
            RunManager.Instance.OnRunCompleted += Refresh;
        }

        if (CombatManager.Instance != null)
            CombatManager.Instance.OnEncounterResult += HandleEncounterResult;

        Refresh();
    }

    void OnDisable()
    {
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnGoldChanged -= HandleGoldChanged;
            CardManager.Instance.OnDeckChanged -= Refresh;
            CardManager.Instance.OnBuildChanged -= Refresh;
            CardManager.Instance.OnConsumablesChanged -= Refresh;
        }

        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnCycleStarted -= HandleCycleStarted;
            RunManager.Instance.OnShowPathScreen -= Refresh;
            RunManager.Instance.OnHidePathScreen -= Refresh;
            RunManager.Instance.OnRunCompleted -= Refresh;
        }

        if (CombatManager.Instance != null)
            CombatManager.Instance.OnEncounterResult -= HandleEncounterResult;
    }

    void HandleGoldChanged(int _) => Refresh();
    void HandleCycleStarted(string _) => Refresh();
    void HandleEncounterResult(EncounterResult _) => Refresh();

    void Update()
    {
        RefreshTiming();
    }

    void Refresh()
    {
        var cards = CardManager.Instance;
        var run = RunManager.Instance;

        if (goldLabel)
            goldLabel.text = cards == null ? "Gold  —" : cards.HasInfiniteMoney ? "Gold  ∞" : $"Gold  {cards.gold}";

        RefreshArtifacts(cards);
        RefreshBackpack(cards);

        if (pileSummaryLabel)
        {
            if (cards == null)
            {
                pileSummaryLabel.text = "Hand  —";
            }
            else
            {
                int enhancedCount = 0;
                foreach (var card in cards.ownedCards)
                    if (card.Enhancement != null) enhancedCount++;
                pileSummaryLabel.text =
                    $"Hand  {cards.HandCount}/{cards.HandCapacity}    Draw  {cards.deck.Count}    Enhanced  {enhancedCount}";
            }
        }

        if (run == null)
        {
            if (runProgressLabel) runProgressLabel.text = "Run —";
            if (bossProgressLabel) bossProgressLabel.text = "Boss —";
            return;
        }

        int completedNodes = 0;
        foreach (var node in run.currentPath)
            if (node.completed) completedNodes++;

        if (runProgressLabel)
        {
            string mapProgress = run.IsInfiniteMode
                ? $"Map {run.CurrentCycle}/∞"
                : $"Map {Mathf.Clamp(run.CurrentCycle, 1, run.BossTotal)}/{run.BossTotal}";
            string routeProgress = run.IsRunCompleted
                ? "Run Complete"
                : $"{mapProgress}  •  Route {completedNodes}/{run.currentPath.Count}";
            runProgressLabel.text = routeProgress;
            if (run.ActiveContract != null)
                runProgressLabel.text += $"\n{FormatContractProgress(run.ActiveContract)}";
        }

        if (bossProgressLabel)
        {
            string bossName = run.CurrentBoss != null ? run.CurrentBoss.enemyName : "Complete";
            bossProgressLabel.text = run.IsInfiniteMode
                ? $"Map {run.CurrentCycle} Boss  •  {bossName}"
                : $"Boss {Mathf.Min(run.bossIndex + 1, run.BossTotal)}/{run.BossTotal}  •  {bossName}";
        }

        RefreshTiming();
    }

    static string FormatContractProgress(RunContractState contract)
    {
        long lastMap = (long)contract.deadlineMapExclusive + 1L;
        return contract.objective switch
        {
            RunContractObjective.WinTwoEncounters =>
                $"Contract • Encounters {contract.encounterWins}/2 • due before Map {lastMap}",
            RunContractObjective.DefeatEliteThisMap =>
                $"Contract • Elite 0/1 • Map {(long)contract.startedAtMapIndex + 1L}",
            RunContractObjective.WinCombatWithoutHpLoss =>
                $"Contract • Flawless 0/1 • due before Map {lastMap}",
            _ => "Contract active"
        };
    }

    void RefreshBackpack(CardManager cards)
    {
        if (backpackSlotLabels == null) return;
        for (int i = 0; i < backpackSlotLabels.Length; i++)
        {
            var label = backpackSlotLabels[i];
            if (!label) continue;
            bool visible = cards != null && i < Mathf.Max(cards.BackpackCapacity, cards.BackpackSlotsUsed);
            label.transform.parent.gameObject.SetActive(visible);
            if (!visible) continue;
            var slot = cards.GetConsumableInstanceAtSlot(i);
            int stackCount = cards.GetConsumableStackCountAtSlot(i);
            label.text = slot == null ? $"EMPTY SLOT {i + 1}" :
                $"{slot.Definition.icon} {slot.Definition.displayName}" +
                (stackCount > 1 ? $" ×{stackCount}" : string.Empty) +
                (slot.Definition.uses > 1
                    ? $" ({slot.RemainingCharges}/{slot.Definition.uses} first)" : string.Empty);
        }
    }

    void RefreshArtifacts(CardManager cards)
    {
        if (!ownedArtifactsLabel) return;
        int used = cards != null ? cards.ArtifactSlotsUsed : 0;
        int capacity = cards != null ? cards.ArtifactCapacity : CardManager.BASE_ARTIFACT_CAPACITY;
        var text = new StringBuilder($"<b>ARTIFACTS  •  {used}/{capacity}</b>\n");
        if (cards == null || cards.ownedArtifacts.Count == 0)
        {
            text.Append("None owned");
        }
        else
        {
            bool first = true;
            foreach (var artifact in cards.ownedArtifacts)
            {
                if (artifact == null) continue;
                if (!first) text.Append("    •    ");
                if (!string.IsNullOrWhiteSpace(artifact.icon)) text.Append(artifact.icon).Append(' ');
                text.Append(artifact.displayName);
                first = false;
            }
        }
        ownedArtifactsLabel.text = text.ToString();
    }

    void RefreshTiming()
    {
        if (!timingLabel) return;
        var timing = RunManager.Instance?.Timing;
        timingLabel.text = timing == null
            ? "Run Time  —\nMap Time  —"
            : $"Run Time  {RunDurationFormatter.Format(timing.TotalElapsedSeconds)}\nMap Time  {RunDurationFormatter.Format(timing.CurrentMapElapsedSeconds)}";
    }
}
