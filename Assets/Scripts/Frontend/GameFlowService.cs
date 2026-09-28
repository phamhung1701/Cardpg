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
        DevModeRuntime.Disable();
        LoadRun(seed);
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
        DevModeRuntime.Disable();
        RestoreGlobalRuntimeState();
        FrontendLaunchContext.RequestMainMenu(destination);
        SceneManager.LoadScene(MainMenuSceneName);
    }

    public void Quit()
    {
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
