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

    void OnEnable()
    {
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnGoldChanged += HandleGoldChanged;
            CardManager.Instance.OnDeckChanged += Refresh;
            CardManager.Instance.OnBuildChanged += Refresh;
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
            goldLabel.text = cards != null ? $"Gold  {cards.gold}" : "Gold  —";

        RefreshArtifacts(cards);

        if (pileSummaryLabel)
        {
            if (cards == null)
            {
                pileSummaryLabel.text = "Draw  —    Discard  —    Hand  —";
            }
            else
            {
                int enhancedCount = 0;
                foreach (var card in cards.ownedCards)
                    if (card.Enhancement != null) enhancedCount++;
                pileSummaryLabel.text =
                    $"Draw  {cards.deck.Count}    Discard  {cards.discardPile.Count}    Hand  {cards.HandCount}/{cards.HandCapacity}    Artifacts  {cards.ArtifactSlotsUsed}/{cards.ArtifactCapacity}    Enhanced  {enhancedCount}";
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
            runProgressLabel.text = run.IsRunCompleted
                ? "Run Complete"
                : $"Map {Mathf.Clamp(run.CurrentCycle, 1, run.BossTotal)}/{run.BossTotal}  •  Route {completedNodes}/{run.currentPath.Count}";
        }

        if (bossProgressLabel)
        {
            string bossName = run.CurrentBoss != null ? run.CurrentBoss.enemyName : "Complete";
            bossProgressLabel.text = $"Boss {Mathf.Min(run.bossIndex + 1, run.BossTotal)}/{run.BossTotal}  •  {bossName}";
        }

        RefreshTiming();
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
