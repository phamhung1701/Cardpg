using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PathScreenUI : MonoBehaviour
{
    [Header("Layout")]
    public GameObject panel;
    public Transform mapContainer;
    public GameObject nodeButtonPrefab;
    public TMP_Text headerText;

    AttackCardPresentationUI _presentation;
    bool _pendingMapShow;
    bool _pendingRunCompleted;
    bool _mapToggleEnabled;
    Button _mapVisibilityButton;
    TMP_Text _mapVisibilityLabel;

    [Header("Map Settings")]
    public float colSpacing = 200f;
    public float rowSpacing = 120f;
    public Vector2 mapOffset = new(180f, 120f);

    void OnEnable()
    {
        EnsurePresentationSubscription();
        EnsureMapVisibilityControl();
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowPathScreen += Show;
            RunManager.Instance.OnHidePathScreen += Hide;
            RunManager.Instance.OnRunCompleted += HandleRunCompleted;
            RunManager.Instance.OnRunStarted += HandleRunStarted;
            RunManager.Instance.OnCycleStarted += HandleCycleStarted;
            RunManager.Instance.OnMapRevealChanged += BuildMap;
        }
    }

    void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowPathScreen -= Show;
            RunManager.Instance.OnHidePathScreen -= Hide;
            RunManager.Instance.OnRunCompleted -= HandleRunCompleted;
            RunManager.Instance.OnRunStarted -= HandleRunStarted;
            RunManager.Instance.OnCycleStarted -= HandleCycleStarted;
            RunManager.Instance.OnMapRevealChanged -= BuildMap;
        }
        UnsubscribeFromPresentation();
        _pendingMapShow = _pendingRunCompleted = false;
        _mapToggleEnabled = false;
        if (_mapVisibilityButton != null) _mapVisibilityButton.gameObject.SetActive(false);
    }

    void EnsurePresentationSubscription()
    {
        var current = AttackCardPresentationUI.Instance;
        if (_presentation == current) return;
        UnsubscribeFromPresentation();
        _presentation = current;
        if (_presentation != null)
        {
            _presentation.OnPresentationCompleted += HandlePresentationCompleted;
            _presentation.OnPresentationCancelled += HandlePresentationCancelled;
        }
    }

    void UnsubscribeFromPresentation()
    {
        if (_presentation != null)
        {
            _presentation.OnPresentationCompleted -= HandlePresentationCompleted;
            _presentation.OnPresentationCancelled -= HandlePresentationCancelled;
            _presentation = null;
        }
    }

    void OnDestroy()
    {
        if (_mapVisibilityButton == null) return;
        if (Application.isPlaying) Destroy(_mapVisibilityButton.gameObject);
        else DestroyImmediate(_mapVisibilityButton.gameObject);
        _mapVisibilityButton = null;
        _mapVisibilityLabel = null;
    }

    void HandlePresentationCompleted()
    {
        if (_pendingRunCompleted)
        {
            _pendingRunCompleted = false;
            ShowRunCompleted();
        }
        else if (_pendingMapShow)
        {
            _pendingMapShow = false;
            ShowImmediately();
        }
    }

    void HandlePresentationCancelled()
    {
        _pendingMapShow = _pendingRunCompleted = false;
        _mapToggleEnabled = false;
        if (panel) panel.SetActive(false);
        SetMapVisibilityControl(false);
    }

    void HandleRunStarted(string _)
    {
        _pendingMapShow = _pendingRunCompleted = false;
        _mapToggleEnabled = false;
        if (panel) panel.SetActive(false);
        SetMapVisibilityControl(false);
    }

    void HandleCycleStarted(string header)
    {
        var run = RunManager.Instance;
        string bossDescription = run != null ? run.CurrentBossDescription : string.Empty;
        if (headerText) headerText.text = string.IsNullOrEmpty(bossDescription)
            ? header
            : $"{header}\n<size=80%>{bossDescription}</size>";
        BuildMap();
    }

    void HandleRunCompleted()
    {
        if (headerText) headerText.text = "VICTORY!";
        EnsurePresentationSubscription();
        if (_presentation != null && _presentation.IsBusy)
        {
            _pendingRunCompleted = true;
            _pendingMapShow = false;
            _mapToggleEnabled = false;
            if (panel) panel.SetActive(false);
            SetMapVisibilityControl(false);
            return;
        }
        ShowRunCompleted();
    }

    void ShowRunCompleted()
    {
        _mapToggleEnabled = false;
        if (headerText) headerText.text = "VICTORY!";
        if (panel) panel.SetActive(true);
        SetMapVisibilityControl(false);
    }

    void Show()
    {
        EnsurePresentationSubscription();
        if (_presentation != null && _presentation.IsBusy)
        {
            _pendingMapShow = true;
            if (panel) panel.SetActive(false);
            return;
        }
        ShowImmediately();
    }

    void ShowImmediately()
    {
        _pendingMapShow = false;
        BuildMap();
        if (panel) panel.SetActive(true);
        var run = RunManager.Instance;
        // A reward modal may temporarily own the input gate while the route is shown.
        // Keep route availability separate from whether a click is allowed this frame.
        _mapToggleEnabled = run != null && !run.IsRunCompleted && run.ActiveNode == null;
        SetMapVisibilityControl(_mapToggleEnabled);
    }

    void Hide()
    {
        _pendingMapShow = _pendingRunCompleted = false;
        _mapToggleEnabled = false;
        if (panel) panel.SetActive(false);
        SetMapVisibilityControl(false);
    }

    void BuildMap()
    {
        if (mapContainer == null || nodeButtonPrefab == null) return;

        foreach (Transform child in mapContainer)
            Destroy(child.gameObject);

        var run = RunManager.Instance;
        if (run == null) return;

        foreach (var node in run.currentPath)
        {
            foreach (int nextId in node.next)
            {
                var next = run.currentPath.Find(candidate => candidate.id == nextId);
                if (next != null)
                    CreateConnection(GridToLocal(node.col, node.row), GridToLocal(next.col, next.row));
            }
        }

        foreach (var node in run.currentPath)
        {
            var buttonObject = Instantiate(nodeButtonPrefab, mapContainer);
            buttonObject.name = $"Node_{node.id}_{node.kind}";
            var nodeButtonUI = buttonObject.GetComponent<PathNodeButtonUI>();
            if (nodeButtonUI == null) nodeButtonUI = buttonObject.AddComponent<PathNodeButtonUI>();
            nodeButtonUI.SetNode(node, run);

            buttonObject.transform.localPosition = GridToLocal(node.col, node.row);

            var button = buttonObject.GetComponent<Button>();
            if (button)
            {
                button.interactable = !node.completed && node.accessible;
                var image = buttonObject.GetComponent<Image>();
                if (image)
                    image.color = GetNodeColor(node);
                int id = node.id;
                button.onClick.AddListener(() => run.OnPathChosen(id));
            }
        }
    }

    void CreateConnection(Vector3 from, Vector3 to)
    {
        var connection = new GameObject("RouteConnection", typeof(RectTransform), typeof(Image));
        connection.layer = gameObject.layer;
        connection.transform.SetParent(mapContainer, false);
        connection.transform.SetAsFirstSibling();

        var rect = connection.GetComponent<RectTransform>();
        Vector2 delta = (Vector2)(to - from);
        rect.anchoredPosition = ((Vector2)from + (Vector2)to) * 0.5f;
        rect.sizeDelta = new Vector2(delta.magnitude, 7f);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        var image = connection.GetComponent<Image>();
        image.color = new Color(0.52f, 0.62f, 0.78f, 1f);
        image.raycastTarget = false;
    }

    void EnsureMapVisibilityControl()
    {
        if (_mapVisibilityButton != null || panel == null || panel.transform.parent == null) return;

        var control = new GameObject("MapVisibilityButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Canvas), typeof(GraphicRaycaster));
        control.transform.SetParent(panel.transform.parent, false);
        control.transform.SetAsLastSibling();
        var rect = control.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -100f);
        rect.sizeDelta = new Vector2(150f, 42f);

        var image = control.GetComponent<Image>();
        image.color = new Color(0.12f, 0.17f, 0.25f, 0.96f);
        var controlCanvas = control.GetComponent<Canvas>();
        controlCanvas.overrideSorting = true;
        controlCanvas.sortingOrder = 45;
        _mapVisibilityButton = control.GetComponent<Button>();
        _mapVisibilityButton.targetGraphic = image;
        _mapVisibilityButton.onClick.AddListener(ToggleMapVisibility);

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(control.transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(5f, 2f);
        labelRect.offsetMax = new Vector2(-5f, -2f);
        _mapVisibilityLabel = labelObject.GetComponent<TextMeshProUGUI>();
        _mapVisibilityLabel.alignment = TextAlignmentOptions.Center;
        _mapVisibilityLabel.raycastTarget = false;
        _mapVisibilityLabel.fontSize = 18f;
        _mapVisibilityLabel.text = "CLOSE MAP";
        control.SetActive(false);
    }

    void SetMapVisibilityControl(bool visible)
    {
        if (_mapVisibilityButton == null) EnsureMapVisibilityControl();
        if (_mapVisibilityButton == null) return;
        _mapVisibilityButton.gameObject.SetActive(visible);
        if (_mapVisibilityLabel != null) _mapVisibilityLabel.text = panel != null && panel.activeSelf ? "CLOSE MAP" : "OPEN MAP";
    }

    bool CanToggleMap()
    {
        var run = RunManager.Instance;
        return run != null && !run.IsRunCompleted && run.ActiveNode == null && !GameplayInputGate.IsBlocked;
    }

    void ToggleMapVisibility()
    {
        if (panel == null || !_mapToggleEnabled || !CanToggleMap()) return;
        panel.SetActive(!panel.activeSelf);
        SetMapVisibilityControl(true);
    }

    static string GetNodeHeading(PathNode node, RunManager run)
    {
        string label = run.GetNodeLabel(node);
        return node.kind == MapNodeType.Combat || node.kind == MapNodeType.Elite
            ? label
            : $"{GetNodeSymbol(node.kind)}  {label}";
    }

    static string GetNodeSymbol(MapNodeType kind) => kind switch
    {
        MapNodeType.Combat => "FIGHT",
        MapNodeType.Elite => "ELITE",
        MapNodeType.Shop => "SHOP",
        MapNodeType.Event => "EVENT",
        MapNodeType.Upgrade => "UP",
        MapNodeType.Risk => "RISK",
        MapNodeType.Boss => "BOSS",
        _ => "NODE"
    };

    static Color GetNodeColor(PathNode node)
    {
        if (node.completed) return new Color(0.24f, 0.52f, 0.34f);
        if (!node.accessible && node.kind != MapNodeType.Elite) return new Color(0.18f, 0.21f, 0.27f);
        if (!node.revealed) return new Color(0.36f, 0.3f, 0.48f);

        return node.kind switch
        {
            MapNodeType.Elite => new Color(0.64f, 0.3f, 0.26f),
            MapNodeType.Shop => new Color(0.5f, 0.4f, 0.18f),
            MapNodeType.Event => new Color(0.28f, 0.45f, 0.58f),
            MapNodeType.Upgrade => new Color(0.28f, 0.56f, 0.42f),
            MapNodeType.Risk => new Color(0.54f, 0.28f, 0.48f),
            MapNodeType.Boss => new Color(0.64f, 0.24f, 0.28f),
            _ => new Color(0.26f, 0.43f, 0.68f)
        };
    }

    Vector3 GridToLocal(float col, float row)
    {
        return new Vector3(col * colSpacing + mapOffset.x, -row * rowSpacing + mapOffset.y, 0);
    }
}
