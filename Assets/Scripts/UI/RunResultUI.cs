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
    bool _pendingResult;
    bool _pendingResultVictory;
    ScrollRect _summaryScroll;

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
        if (retrySameSeedButton) { retrySameSeedButton.onClick.AddListener(RetrySameSeed); KeyButtonBorderStyle.Apply(retrySameSeedButton); }
        if (newRunButton) { newRunButton.onClick.AddListener(NewRun); KeyButtonBorderStyle.Apply(newRunButton); }
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
        if (result == EncounterResult.Defeat) ShowWhenPresentationIdle(false);
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

    void HandleRunCompleted() => ShowWhenPresentationIdle(true);

    void ShowWhenPresentationIdle(bool victory)
    {
        if (IsShowingResult || _pendingResult) return;
        EnsurePresentationSubscription();
        if (_presentation != null && _presentation.IsBusy)
        {
            _pendingResult = true;
            _pendingResultVictory = victory;
            return;
        }
        ShowResult(victory);
    }

    void HandlePresentationCompleted() => ShowPendingResult();
    void HandlePresentationCancelled() => ShowPendingResult();

    void ShowPendingResult()
    {
        if (!_pendingResult) return;
        bool victory = _pendingResultVictory;
        _pendingResult = false;
        _pendingResultVictory = false;
        ShowResult(victory);
    }

    void HandleEnemyChanged()
    {
        if (CombatManager.Instance != null && CombatManager.Instance.currentEnemy != null && rewardBanner)
            rewardBanner.SetActive(false);
    }

    void HandleRunStarted(string _)
    {
        _pendingResult = false;
        _pendingResultVictory = false;
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
        if (IsShowingResult) return;
        var run = RunManager.Instance;
        var cards = CardManager.Instance;
        int totalBosses = run != null ? run.BossTotal : 12;
        int bossesDefeated = run != null ? Mathf.Clamp(run.bossIndex, 0, totalBosses) : 0;
        int mapReached = victory ? totalBosses : run != null ? Mathf.Clamp(run.CurrentCycle, 1, totalBosses) : 0;
        string seed = run != null ? run.RunSeed : string.Empty;
        string artifacts = cards == null || cards.ownedArtifacts.Count == 0
            ? "None"
            : string.Join(", ", cards.ownedArtifacts.Where(value => value != null).Select(value => value.displayName));
        int enhanced = cards != null ? cards.ownedCards.Count(card => card.Enhancement != null) : 0;
        string goldDisplay = cards == null ? "0" : cards.HasInfiniteMoney ? "∞" : cards.gold.ToString();

        string timingSummary = BuildTimingSummary(run?.Timing, victory);

        if (resultTitleLabel)
        {
            resultTitleLabel.text = victory ? "RUN VICTORY" : "RUN DEFEAT";
            resultTitleLabel.color = victory ? new Color(0.46f, 0.88f, 0.63f, 1f) : new Color(1f, 0.50f, 0.43f, 1f);
        }
        if (retrySameSeedButton)
        {
            var retryLabel = retrySameSeedButton.GetComponentInChildren<TMP_Text>();
            if (retryLabel) retryLabel.text = run != null && run.CanContinueInfiniteMode
                ? "CONTINUE INFINITE MODE" : "RETRY SAME SEED";
        }
        if (resultBodyLabel)
        {
            resultBodyLabel.alignment = TextAlignmentOptions.TopLeft;
            resultBodyLabel.overflowMode = TextOverflowModes.Overflow;
            resultBodyLabel.text =
                $"<size=85%><b><color=#F2B95E>SEED</color></b></size>\n{EscapeRichText(seed)}\n\n" +
                $"<size=85%><b><color=#F2B95E>PROGRESS</color></b></size>\nDistrict {mapReached}/{totalBosses}  ·  Headliners defeated {bossesDefeated}/{totalBosses}\n\n" +
                $"<size=85%><b><color=#F2B95E>RUN TOTALS</color></b></size>\nCredits  {goldDisplay}\nGear  {EscapeRichText(artifacts)}\nMods applied  {enhanced}" +
                (victory && !string.IsNullOrEmpty(_lastBossRewardName)
                    ? $"\nFinal reward  {EscapeRichText(_lastBossRewardName)}" : string.Empty) +
                $"\n\n<size=85%><b><color=#F2B95E>TIMING</color></b></size>\n{timingSummary}";
        }

        IsShowingResult = true;
        if (coordinator != null)
            coordinator.Push(this, resultPanel, retrySameSeedButton, GameplayInputBlockReason.RunResult);
        else if (resultPanel)
            resultPanel.SetActive(true);
        RefreshSummaryScroll();
    }

    static string EscapeRichText(string value) => string.IsNullOrEmpty(value)
        ? string.Empty
        : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    void RefreshSummaryScroll()
    {
        if (resultBodyLabel == null) return;
        _summaryScroll = resultBodyLabel.GetComponentInParent<ScrollRect>(true);
        if (_summaryScroll == null) return;
        _summaryScroll.content = resultBodyLabel.rectTransform;
        Canvas.ForceUpdateCanvases();
        _summaryScroll.verticalNormalizedPosition = 1f;
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
                .Append("District ")
                .Append(split.MapNumber)
                .Append("  ")
                .Append(RunDurationFormatter.Format(split.ElapsedSeconds));
        }

        if (!victory)
        {
            var incomplete = timing.CurrentIncompleteSplit;
            summary.Append("\nDistrict ")
                .Append(incomplete.MapNumber)
                .Append("  ")
                .Append(RunDurationFormatter.Format(incomplete.ElapsedSeconds))
                .Append(" (incomplete)");
        }
        return summary.ToString();
    }

    void HideAll()
    {
        _pendingResult = false;
        _pendingResultVictory = false;
        _lastBossRewardName = null;
        if (rewardBanner) rewardBanner.SetActive(false);
        if (coordinator != null && coordinator.IsTop(this)) coordinator.Pop(this);
        else if (resultPanel) resultPanel.SetActive(false);
        IsShowingResult = false;
        GameplayInputGate.Set(GameplayInputBlockReason.RunResult, false);
    }
}
