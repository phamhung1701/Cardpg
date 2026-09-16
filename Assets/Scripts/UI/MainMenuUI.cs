using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [Header("Settings")]
    public string gameSceneName = "Game";

    public void OnPlayClicked()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
