using UnityEngine;

// Managers and scene references must exist before UI OnEnable subscriptions.
[DefaultExecutionOrder(-50)]
public class GameController : MonoBehaviour
{
    [Header("References")]
    public Field handField;
    public Canvas dragCanvas;
    public CardView cardPrefab;
    public RelicData[] relicCatalog;

    [Header("Enemy Definitions")]
    public EnemyTypeData thiefType;
    public EnemyTypeData goblinType;
    public EnemyTypeData knightType;

    bool _configured;

    void Awake()
    {
        if (handField == null || handField.cardsHolder == null ||
            dragCanvas == null || cardPrefab == null ||
            thiefType == null || goblinType == null || knightType == null)
        {
            Debug.LogError("GameController: assign hand, card, canvas, and all enemy references before starting a run.", this);
            return;
        }

        var cards = GetOrCreate<CardManager>();
        GetOrCreate<CombatManager>();
        var run = GetOrCreate<RunManager>();
        cards.Configure(handField, dragCanvas, cardPrefab, relicCatalog);
        run.thiefType = thiefType;
        run.goblinType = goblinType;
        run.knightType = knightType;
        _configured = true;
    }

    void Start()
    {
        if (_configured)
            RunManager.Instance.StartRun();
    }

    static T GetOrCreate<T>() where T : Singleton<T>
    {
        var manager = Singleton<T>.Instance;
        return manager != null ? manager : new GameObject($"[{typeof(T).Name}]").AddComponent<T>();
    }
}
