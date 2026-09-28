#if UNITY_EDITOR
using UnityEditor;

[InitializeOnLoad]
static class DevModeEditorLifecycle
{
    static DevModeEditorLifecycle()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            DevModeRuntime.Disable();
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            DevModeRuntime.Disable();
    }
}
#endif
