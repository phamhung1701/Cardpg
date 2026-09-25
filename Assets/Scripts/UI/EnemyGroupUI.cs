using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the dynamic enemy presentation for the active encounter.
/// CombatManager remains authoritative; instantiated views bind to individual runtime enemies.
/// </summary>
public class EnemyGroupUI : MonoBehaviour
{
    [Header("Dynamic Enemy Area")]
    public RectTransform enemyContainer;
    public EnemyDisplayUI enemyViewPrefab;

    readonly List<EnemyDisplayUI> _activeViews = new();

    public IReadOnlyList<EnemyDisplayUI> ActiveViews => _activeViews;

    void OnEnable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnEnemiesChanged += Refresh;
            CombatManager.Instance.OnEnemyChanged += Refresh;
        }
        Refresh();
    }

    void OnDisable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnEnemiesChanged -= Refresh;
            CombatManager.Instance.OnEnemyChanged -= Refresh;
        }
    }

    public void Refresh()
    {
        var enemies = CombatManager.Instance?.Enemies;
        int enemyCount = enemies?.Count ?? 0;
        if (enemyContainer == null || enemyViewPrefab == null)
        {
            if (enemyCount > 0)
                Debug.LogError("EnemyGroupUI requires an Enemy Area and Enemy View prefab.", this);
            return;
        }

        EnsureViewCount(enemyCount);
        for (int i = 0; i < enemyCount; i++)
        {
            var view = _activeViews[i];
            view.gameObject.SetActive(true);
            view.Bind(enemies[i]);
            view.transform.SetSiblingIndex(i);
        }
    }

    void EnsureViewCount(int requiredCount)
    {
        while (_activeViews.Count < requiredCount)
        {
            var view = Instantiate(enemyViewPrefab, enemyContainer, false);
            view.name = $"EnemyView {_activeViews.Count + 1}";
            _activeViews.Add(view);
        }

        for (int i = _activeViews.Count - 1; i >= requiredCount; i--)
        {
            var view = _activeViews[i];
            _activeViews.RemoveAt(i);
            if (view == null) continue;
            view.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(view.gameObject);
            else
                DestroyImmediate(view.gameObject);
        }
    }
}
