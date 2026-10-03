using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RunResultUI : MonoBehaviour
{
    [Header("Encounter Feedback")]
    public GameObject rewardBanner;
    public TMP_Text rewardLabel;

    [Header("Reusable Results Screen")]
    public GameplayMenuCoordinator coordinator;
    public GameObject resultPanel;
    public TMP_Text resultTitleLabel;
    public TMP_Text resultBodyLabel;
    public Button retrySameSeedButton;
    public Button newRunButton;
    public Button mainMenuButton;

    string _lastBossRewardName;
    AttackCardPresentationUI _presentation;
    bool _pendingVictoryResult;

    public static bool IsShowingResult { get; private set; }

    void OnEnable()
    {
        EnsurePresentationSubscription();
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnEncounterResult += HandleEncounterResult;
            CombatManager.Instance.OnEnemyChanged += HandleEnemyChanged;
        }
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnBossRewardGranted += HandleBossRewardGranted;
            RunManager.Instance.OnRunCompleted += HandleRunCompleted;
            RunManager.Instance.OnRunStarted += HandleRunStarted;
        }
        if (retrySameSeedButton) retrySameSeedButton.onClick.AddListener(RetrySameSeed);
        if (newRunButton) newRunButton.onClick.AddListener(NewRun);
        if (mainMenuButton) mainMenuButton.onClick.AddListener(MainMenu);
        HideAll();
    }

    void OnDisable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnEncounterResult -= HandleEncounterResult;
            CombatManager.Instance.OnEnemyChanged -= HandleEnemyChanged;
        }
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnBossRewardGranted -= HandleBossRewardGranted;
            RunManager.Instance.OnRunCompleted -= HandleRunCompleted;
            RunManager.Instance.OnRunStarted -= HandleRunStarted;
        }
        if (retrySameSeedButton) retrySameSeedButton.onClick.RemoveListener(RetrySameSeed);
        if (newRunButton) newRunButton.onClick.RemoveListener(NewRun);
        if (mainMenuButton) mainMenuButton.onClick.RemoveListener(MainMenu);
        UnsubscribeFromPresentation();
        HideAll();
    }

    void EnsurePresentationSubscription()
    {
        var current = AttackCardPresentationUI.Instance;
        if (_presentation == current) return;
        UnsubscribeFromPresentation();
        _presentation = current;
        if (_presentation != null)
        {
            _presentation.OnPresentationCompleted += HandlePresentationCompleted;
            _presentation.OnPresentationCancelled += HandlePresentationCancelled;
        }
    }

    void UnsubscribeFromPresentation()
    {
        if (_presentation == null) return;
        _presentation.OnPresentationCompleted -= HandlePresentationCompleted;
        _presentation.OnPresentationCancelled -= HandlePresentationCancelled;
        _presentation = null;
    }

    void HandleEncounterResult(EncounterResult result)
    {
        if (result == EncounterResult.Defeat)
            ShowResult(false);
    }

    void HandleBossRewardGranted(CardInstance reward)
    {
        _lastBossRewardName = reward != null ? reward.DisplayName : null;
        if (rewardBanner) rewardBanner.SetActive(reward != null);
        if (rewardLabel)
            rewardLabel.text = reward != null
                ? $"BOSS REWARD  •  {reward.DisplayName} added to your draw pile"
                : string.Empty;
    }

    void HandleRunCompleted()
    {
        EnsurePresentationSubscription();
        if (_presentation != null && _presentation.IsBusy)
        {
            _pendingVictoryResult = true;
            return;
        }
        ShowResult(true);
    }

    void HandlePresentationCompleted()
    {
        if (!_pendingVictoryResult) return;
        _pendingVictoryResult = false;
        ShowResult(true);
    }

    void HandlePresentationCancelled() => _pendingVictoryResult = false;

    void HandleEnemyChanged()
    {
        if (CombatManager.Instance != null && CombatManager.Instance.currentEnemy != null && rewardBanner)
            rewardBanner.SetActive(false);
    }

    void HandleRunStarted(string _)
    {
        _pendingVictoryResult = false;
        HideAll();
    }

    public void RetrySameSeed()
    {
        var run = RunManager.Instance;
        bool continueInfinite = run != null && run.CanContinueInfiniteMode;
        HideAll();
        if (continueInfinite) run.ContinueInfiniteMode();
        else run?.RestartRun();
    }

    public void NewRun()
    {
        HideAll();
        RunManager.Instance?.AbandonRun();
        GameFlowService.Instance?.ShowMainMenu(MainMenuDestination.RunSetup);
    }

    public void MainMenu()
    {
        HideAll();
        RunManager.Instance?.AbandonRun();
        GameFlowService.Instance?.ShowMainMenu(MainMenuDestination.Home);
    }

    void ShowResult(bool victory)
    {
        var run = RunManager.Instance;
        var cards = CardManager.Instance;
        int totalBosses = run != null ? run.BossTotal : 12;
        int bossesDefeated = run != null ? Mathf.Clamp(run.bossIndex, 0, totalBosses) : 0;
        int mapReached = victory ? totalBosses : run != null ? Mathf.Clamp(run.CurrentCycle, 1, totalBosses) : 0;
        string seed = run != null ? run.RunSeed : string.Empty;
        string artifacts = cards == null || cards.ownedArtifacts.Count == 0
            ? "None"
            : string.Join(", ", cards.ownedArtifacts.Where(value => value != null).Select(value => value.displayName).Take(6));
        int enhanced = cards != null ? cards.ownedCards.Count(card => card.Enhancement != null) : 0;
        string goldDisplay = cards == null ? "0" : cards.HasInfiniteMoney ? "∞" : cards.gold.ToString();
        string rewardLine = victory && !string.IsNullOrEmpty(_lastBossRewardName)
            ? $"\nFinal reward: {_lastBossRewardName}"
            : string.Empty;

        string timingSummary = BuildTimingSummary(run?.Timing, victory);

        if (resultTitleLabel) resultTitleLabel.text = victory ? "RUN VICTORY" : "RUN DEFEAT";
        if (retrySameSeedButton)
        {
            var retryLabel = retrySameSeedButton.GetComponentInChildren<TMP_Text>();
            if (retryLabel) retryLabel.text = run != null && run.CanContinueInfiniteMode
                ? "CONTINUE INFINITE MODE" : "RETRY SAME SEED";
        }
        if (resultBodyLabel)
        {
            resultBodyLabel.text =
                $"Seed  {seed}\n" +
                $"Progress  Map {mapReached}/{totalBosses}\n" +
                $"Bosses Defeated  {bossesDefeated}/{totalBosses}\n" +
                $"Gold  {goldDisplay}\n" +
                $"Artifacts  {artifacts}\n" +
                $"Enhanced Cards  {enhanced}{rewardLine}\n\n" +
                timingSummary;
        }

        IsShowingResult = true;
        if (coordinator != null)
            coordinator.Push(this, resultPanel, retrySameSeedButton, GameplayInputBlockReason.RunResult);
        else if (resultPanel)
            resultPanel.SetActive(true);
    }

    static string BuildTimingSummary(RunTimingStatistics timing, bool victory)
    {
        if (timing == null) return "Total Run Time  —";

        var summary = new StringBuilder();
        summary.Append("Total Run Time  ")
            .Append(RunDurationFormatter.Format(timing.TotalElapsedSeconds));
        for (int i = 0; i < timing.CompletedSplits.Count; i++)
        {
            var split = timing.CompletedSplits[i];
            summary.Append(i % 2 == 0 ? "\n" : "    ")
                .Append("Map ")
                .Append(split.MapNumber)
                .Append("  ")
                .Append(RunDurationFormatter.Format(split.ElapsedSeconds));
        }

        if (!victory)
        {
            var incomplete = timing.CurrentIncompleteSplit;
            summary.Append("\nMap ")
                .Append(incomplete.MapNumber)
                .Append("  ")
                .Append(RunDurationFormatter.Format(incomplete.ElapsedSeconds))
                .Append(" (incomplete)");
        }
        return summary.ToString();
    }

    void HideAll()
    {
        _pendingVictoryResult = false;
        _lastBossRewardName = null;
        if (rewardBanner) rewardBanner.SetActive(false);
        if (coordinator != null && coordinator.IsTop(this)) coordinator.Pop(this);
        else if (resultPanel) resultPanel.SetActive(false);
        IsShowingResult = false;
        GameplayInputGate.Set(GameplayInputBlockReason.RunResult, false);
    }
}
