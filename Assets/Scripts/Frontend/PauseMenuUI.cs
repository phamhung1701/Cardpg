using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class PauseMenuUI : MonoBehaviour
{
    public GameplayMenuCoordinator coordinator;
    public GameObject panel;
    public Button pauseButton;
    public Button resumeButton;
    public Button settingsButton;
    public Button restartButton;
    public Button abandonButton;
    public SettingsMenuUI settingsMenu;
    public ConfirmationDialogUI confirmationDialog;

    InputAction _pauseAction;
    int _lastHandledFrame = -1;

    public bool IsOpen => coordinator != null && coordinator.Contains(this);

    void OnEnable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevModePanelUI.InstallGame(this);
#endif
        if (pauseButton) pauseButton.onClick.AddListener(OpenPause);
        if (resumeButton) resumeButton.onClick.AddListener(Resume);
        if (settingsButton) settingsButton.onClick.AddListener(OpenSettings);
        if (restartButton) restartButton.onClick.AddListener(ConfirmRestart);
        if (abandonButton) abandonButton.onClick.AddListener(ConfirmAbandon);
        if (panel) panel.SetActive(false);

        _pauseAction = new InputAction("Pause", InputActionType.Button);
        _pauseAction.AddBinding("<Keyboard>/escape");
        _pauseAction.AddBinding("<Gamepad>/start");
        _pauseAction.performed += HandlePausePerformed;
        _pauseAction.Enable();
    }

    void OnDisable()
    {
        if (pauseButton) pauseButton.onClick.RemoveListener(OpenPause);
        if (resumeButton) resumeButton.onClick.RemoveListener(Resume);
        if (settingsButton) settingsButton.onClick.RemoveListener(OpenSettings);
        if (restartButton) restartButton.onClick.RemoveListener(ConfirmRestart);
        if (abandonButton) abandonButton.onClick.RemoveListener(ConfirmAbandon);
        if (_pauseAction != null)
        {
            _pauseAction.performed -= HandlePausePerformed;
            _pauseAction.Disable();
            _pauseAction.Dispose();
            _pauseAction = null;
        }
        if (coordinator != null && coordinator.Contains(this)) coordinator.ClearAll();
    }

    void HandlePausePerformed(InputAction.CallbackContext _)
    {
        if (_lastHandledFrame == Time.frameCount) return;
        _lastHandledFrame = Time.frameCount;

        if (confirmationDialog != null && confirmationDialog.IsOpen)
        {
            confirmationDialog.Cancel();
            return;
        }
        if (settingsMenu != null && settingsMenu.IsOpen && coordinator != null && coordinator.IsTop(settingsMenu))
        {
            settingsMenu.Back();
            return;
        }
        if (IsOpen && coordinator.IsTop(this))
        {
            Resume();
            return;
        }
        if (coordinator != null && !coordinator.HasBlockingMenu)
            OpenPause();
    }

    public void OpenPause()
    {
        if (coordinator == null || panel == null || coordinator.HasBlockingMenu) return;
        if (RunManager.Instance == null || RunManager.Instance.IsRunCompleted) return;
        if (CombatManager.Instance != null && CombatManager.Instance.currentState == GameState.GameOver) return;
        coordinator.Push(this, panel, resumeButton);
    }

    public void Resume()
    {
        if (coordinator != null && coordinator.IsTop(this)) coordinator.Pop(this);
    }

    public void OpenSettings()
    {
        if (!IsOpen || settingsMenu == null || coordinator == null) return;
        settingsMenu.Open(coordinator, () => { });
    }

    public void ConfirmRestart()
    {
        if (!IsOpen || confirmationDialog == null) return;
        confirmationDialog.Open(
            coordinator,
            "RESTART RUN?",
            "Restart from the beginning using the same seed? Current run progress will be lost.",
            "RESTART SAME SEED",
            RestartSameSeed);
    }

    public void ConfirmAbandon()
    {
        if (!IsOpen || confirmationDialog == null) return;
        confirmationDialog.Open(
            coordinator,
            "ABANDON RUN?",
            "Return to the Main Menu? Current run progress will be lost.",
            "ABANDON RUN",
            AbandonRun);
    }

    void RestartSameSeed()
    {
        if (coordinator != null) coordinator.ClearAll();
        RunManager.Instance?.RestartRun();
    }

    void AbandonRun()
    {
        if (coordinator != null) coordinator.ClearAll();
        RunManager.Instance?.AbandonRun();
        GameFlowService.Instance?.ShowMainMenu(MainMenuDestination.Home);
    }
}
