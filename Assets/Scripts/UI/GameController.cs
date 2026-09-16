using System.Collections;
using UnityEngine;

public class GameController : MonoBehaviour
{
    [Header("References")]
    public Field handField;
    public Canvas dragCanvas;
    public CardView cardPrefab;
    public RelicData[] relicCatalog;

    void Start()
    {
        var cm = CardManager.Instance;
        if (handField == null) Debug.LogError("GameController: Hand Field is not assigned.");
        if (dragCanvas == null) Debug.LogError("GameController: Drag Canvas is not assigned.");
        if (cardPrefab == null) Debug.LogError("GameController: Card Prefab is not assigned.");
        if (relicCatalog == null || relicCatalog.Length == 0) Debug.LogWarning("GameController: Relic Catalog is empty.");

        cm.handField = handField;
        cm.dragCanvas = dragCanvas;
        cm.cardPrefab = cardPrefab;
        cm.relicCatalog.Clear();
        if (relicCatalog != null)
            cm.relicCatalog.AddRange(relicCatalog);
        cm.OnGoldChanged += HandleGoldChanged;
        cm.BuildDeck();
        cm.DealHand();
        StartCoroutine(DeferStartRun());
    }

    IEnumerator DeferStartRun()
    {
        yield return null;
        RunManager.Instance.StartRun();
    }

    void HandleGoldChanged(int gold)
    {
        Debug.Log($"Gold: {gold}");
    }

    void OnDestroy()
    {
        if (CardManager.Instance != null)
            CardManager.Instance.OnGoldChanged -= HandleGoldChanged;
    }
}
