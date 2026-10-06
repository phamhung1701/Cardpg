using System.Collections;
using UnityEngine;

/// <summary>Small fade/scale transition for the shared central board presentation panels.</summary>
public sealed class BoardPanelTransition : MonoBehaviour
{
    const float Duration = 0.16f;
    CanvasGroup _group;
    RectTransform _rect;
    Vector3 _shownScale;

    public static void Show(GameObject panel)
    {
        if (panel == null) return;
        if (!Application.isPlaying)
        {
            panel.SetActive(true);
            return;
        }
        var transition = panel.GetComponent<BoardPanelTransition>() ?? panel.AddComponent<BoardPanelTransition>();
        transition.ShowPanel();
    }

    public static void Hide(GameObject panel)
    {
        if (panel == null || !panel.activeSelf) return;
        if (!Application.isPlaying)
        {
            panel.SetActive(false);
            return;
        }
        var transition = panel.GetComponent<BoardPanelTransition>() ?? panel.AddComponent<BoardPanelTransition>();
        transition.HidePanel();
    }

    bool _initialized;

    void Awake() => CacheComponents();

    void CacheComponents()
    {
        if (_initialized) return;
        _group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _rect = transform as RectTransform;
        _shownScale = _rect != null ? _rect.localScale : Vector3.one;
        _initialized = true;
    }

    void ShowPanel()
    {
        StopAllCoroutines();
        gameObject.SetActive(true);
        CacheComponents();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;
        if (_rect != null) _rect.localScale = _shownScale * 0.985f;
        if (!Application.isPlaying)
        {
            _group.alpha = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
            if (_rect != null) _rect.localScale = _shownScale;
            return;
        }
        StartCoroutine(Animate(1f, true));
    }

    void HidePanel()
    {
        StopAllCoroutines();
        CacheComponents();
        if (!Application.isPlaying)
        {
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            gameObject.SetActive(false);
            return;
        }
        _group.interactable = false;
        _group.blocksRaycasts = false;
        StartCoroutine(Animate(0f, false));
    }

    IEnumerator Animate(float targetAlpha, bool activateAtEnd)
    {
        float startAlpha = _group.alpha;
        Vector3 startScale = _rect != null ? _rect.localScale : Vector3.one;
        float elapsed = 0f;
        while (elapsed < Duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            float eased = t * t * (3f - 2f * t);
            _group.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            if (_rect != null) _rect.localScale = Vector3.Lerp(startScale, _shownScale, eased);
            yield return null;
        }
        _group.alpha = targetAlpha;
        if (_rect != null) _rect.localScale = _shownScale;
        if (activateAtEnd)
        {
            _group.interactable = true;
            _group.blocksRaycasts = true;
        }
        else gameObject.SetActive(false);
    }
}
