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
    readonly HashSet<EnemyRuntime> _defeating = new();
    CombatManager _combat;
    RunManager _run;

    public IReadOnlyList<EnemyDisplayUI> ActiveViews => _activeViews;
    public bool IsDefeatPresentationBusy => _defeating.Count > 0;
    public event System.Action OnDefeatPresentationCompleted;

    void OnEnable()
    {
        _combat = CombatManager.Instance;
        _run = RunManager.Instance;
        if (_combat != null)
        {
            _combat.OnEnemiesChanged += Refresh;
            _combat.OnEnemyChanged += Refresh;
            _combat.OnDamageResolved += HandleDamageResolved;
        }
        if (_run != null) _run.OnRunStarted += HandleRunStarted;
        Refresh();
    }

    void OnDisable()
    {
        if (_combat != null)
        {
            _combat.OnEnemiesChanged -= Refresh;
            _combat.OnEnemyChanged -= Refresh;
            _combat.OnDamageResolved -= HandleDamageResolved;
        }
        if (_run != null) _run.OnRunStarted -= HandleRunStarted;
        CancelDefeatPresentations(false);
        _combat = null;
        _run = null;
    }

    void HandleRunStarted(string _) => CancelDefeatPresentations();

    void CancelDefeatPresentations(bool refresh = true)
    {
        bool wasBusy = _defeating.Count > 0;
        StopAllCoroutines();
        foreach (var view in _activeViews)
            if (view != null) view.CancelDefeatCue();
        _defeating.Clear();
        if (refresh) Refresh();
        if (wasBusy) OnDefeatPresentationCompleted?.Invoke();
    }

    void HandleDamageResolved(DamageResult result)
    {
        if (!result.WasLethal || result.TargetAlreadyDefeated || !(result.Request.Target is EnemyRuntime enemy) ||
            _defeating.Contains(enemy)) return;
        for (int i = 0; i < _activeViews.Count; i++)
        {
            var view = _activeViews[i];
            if (view == null || !ReferenceEquals(view.DisplayedEnemy, enemy)) continue;
            _defeating.Add(enemy);
            view.PlayDefeatCue();
            StartCoroutine(RemoveDefeatedView(enemy, view));
            return;
        }
    }

    System.Collections.IEnumerator RemoveDefeatedView(EnemyRuntime enemy, EnemyDisplayUI view)
    {
        yield return new WaitForSeconds(0.48f);
        _defeating.Remove(enemy);
        if (view != null)
        {
            _activeViews.Remove(view);
            view.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(view.gameObject);
            else DestroyImmediate(view.gameObject);
        }
        Refresh();
        if (_defeating.Count == 0) OnDefeatPresentationCompleted?.Invoke();
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

        var desired = new List<EnemyRuntime>(enemyCount + _defeating.Count);
        for (int i = 0; i < enemyCount; i++) desired.Add(enemies[i]);
        for (int i = 0; i < _activeViews.Count; i++)
        {
            var view = _activeViews[i];
            if (view != null && _defeating.Contains(view.DisplayedEnemy) && !desired.Contains(view.DisplayedEnemy))
                desired.Add(view.DisplayedEnemy);
        }

        // Reconcile by runtime identity so simultaneous kills never animate the wrong recycled panel.
        var orderedViews = new List<EnemyDisplayUI>(desired.Count);
        var used = new HashSet<EnemyDisplayUI>();
        for (int i = 0; i < desired.Count; i++)
        {
            EnemyDisplayUI view = null;
            for (int j = 0; j < _activeViews.Count; j++)
                if (!used.Contains(_activeViews[j]) && _activeViews[j] != null &&
                    ReferenceEquals(_activeViews[j].DisplayedEnemy, desired[i])) { view = _activeViews[j]; break; }
            if (view == null)
                for (int j = 0; j < _activeViews.Count; j++)
                    if (!used.Contains(_activeViews[j]) && _activeViews[j] != null && !_defeating.Contains(_activeViews[j].DisplayedEnemy)) { view = _activeViews[j]; break; }
            if (view == null) view = Instantiate(enemyViewPrefab, enemyContainer, false);
            used.Add(view);
            orderedViews.Add(view);
            view.gameObject.SetActive(true);
            if (!_defeating.Contains(desired[i])) view.Bind(desired[i]);
            view.transform.SetSiblingIndex(i);
        }
        for (int i = 0; i < _activeViews.Count; i++)
            if (!used.Contains(_activeViews[i]) && _activeViews[i] != null) Destroy(_activeViews[i].gameObject);
        _activeViews.Clear();
        _activeViews.AddRange(orderedViews);
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
