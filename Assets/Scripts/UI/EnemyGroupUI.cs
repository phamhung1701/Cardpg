using UnityEngine;

/// <summary>
/// Binds a fixed set of presentation slots to the active encounter enemies.
/// CombatManager remains authoritative; slots are presentation only.
/// </summary>
public class EnemyGroupUI : MonoBehaviour
{
    public EnemyDisplayUI[] slots;
    [Min(1f)] public float horizontalSpacing = 480f;

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
        int visibleCount = Mathf.Min(enemyCount, slots?.Length ?? 0);

        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;

            bool visible = i < visibleCount;
            if (visible)
            {
                slot.gameObject.SetActive(true);
                slot.Bind(enemies[i]);
                PositionSlot(slot.transform as RectTransform, i, visibleCount);
            }
            else
            {
                slot.Bind(null);
                slot.gameObject.SetActive(false);
            }
        }

        if (enemyCount > slots.Length)
            Debug.LogError($"EnemyGroupUI has {slots.Length} slots for {enemyCount} active enemies.", this);
    }

    void PositionSlot(RectTransform slot, int index, int count)
    {
        if (slot == null) return;
        float centerOffset = (count - 1) * 0.5f;
        slot.anchoredPosition = new Vector2((index - centerOffset) * horizontalSpacing, slot.anchoredPosition.y);
    }
}
