using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameFlowService : MonoBehaviour
{
    public const string MainMenuSceneName = "MainMenu";
    public const string GameSceneName = "Game";

    public static GameFlowService Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        DisplaySettingsService.LoadAndApply();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void StartRun(string seed)
    {
        if (SavePromptUI.IsOpen) return;
        if (SaveGameService.Store.HasActiveFiles)
        {
            SavePromptUI.AskReplacement(() => StartConfirmedRun(seed));
            return;
        }
        StartConfirmedRun(seed);
    }

    void StartConfirmedRun(string seed)
    {
        if (!SaveGameService.DeleteActiveSave()) { SavePromptUI.ShowError(SaveGameService.LastError); return; }
        DevModeRuntime.Disable();
        LoadRun(seed);
    }

    public bool ContinueRun()
    {
        if (SavePromptUI.IsOpen) return false;
        if (!SaveGameService.TryRead(out var data, out string error))
        { SavePromptUI.ShowError(error); return false; }
        DevModeRuntime.Disable();
        RestoreGlobalRuntimeState();
        FrontendLaunchContext.RequestContinue(data);
        SceneManager.LoadScene(GameSceneName);
        return true;
    }

    public void StartDevRun(string seed, bool infiniteHealth, bool infiniteMoney, bool unlimitedShopOffers)
    {
        DevModeRuntime.Configure(true, infiniteHealth, infiniteMoney, unlimitedShopOffers);
        LoadRun(seed);
    }

    void LoadRun(string seed)
    {
        RestoreGlobalRuntimeState();
        FrontendLaunchContext.RequestRun(seed);
        SceneManager.LoadScene(GameSceneName);
    }

    public void ShowMainMenu(MainMenuDestination destination = MainMenuDestination.Home)
    {
        SaveGameService.Checkpoint(RunManager.Instance, true);
        DevModeRuntime.Disable();
        RestoreGlobalRuntimeState();
        FrontendLaunchContext.RequestMainMenu(destination);
        SceneManager.LoadScene(MainMenuSceneName);
    }

    public void Quit()
    {
        SaveGameService.Checkpoint(RunManager.Instance, true);
        RestoreGlobalRuntimeState();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public static void RestoreGlobalRuntimeState()
    {
        Time.timeScale = 1f;
        GameplayInputGate.Clear();
    }
}
