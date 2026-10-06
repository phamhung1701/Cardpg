using System.Collections.Generic;
using System.Linq;
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
    [Min(0f)] public float horizontalInset = 20f;
    [Min(0f)] public float verticalInset = 30f;
    [Min(1f)] public float laneSpacing = 135f;

    readonly Dictionary<int, Vector2> _nodePositions = new();

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
        BoardPanelTransition.Hide(panel);
        SetMapVisibilityControl(false);
    }

    void HandleRunStarted(string _)
    {
        _pendingMapShow = _pendingRunCompleted = false;
        _mapToggleEnabled = false;
        BoardPanelTransition.Hide(panel);
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
            BoardPanelTransition.Hide(panel);
            SetMapVisibilityControl(false);
            return;
        }
        ShowRunCompleted();
    }

    void ShowRunCompleted()
    {
        _mapToggleEnabled = false;
        if (headerText) headerText.text = "VICTORY!";
        BoardPanelTransition.Show(panel);
        SetMapVisibilityControl(false);
    }

    void Show()
    {
        EnsurePresentationSubscription();
        if (_presentation != null && _presentation.IsBusy)
        {
            _pendingMapShow = true;
            BoardPanelTransition.Hide(panel);
            return;
        }
        ShowImmediately();
    }

    void ShowImmediately()
    {
        _pendingMapShow = false;
        BuildMap();
        BoardPanelTransition.Show(panel);
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
        BoardPanelTransition.Hide(panel);
        SetMapVisibilityControl(false);
    }

    void BuildMap()
    {
        if (mapContainer is not RectTransform bounds || nodeButtonPrefab == null) return;

        foreach (Transform child in mapContainer)
            Destroy(child.gameObject);
        _nodePositions.Clear();

        var run = RunManager.Instance;
        if (run == null || run.currentPath == null || run.currentPath.Count == 0) return;

        Vector2 nodeSize = new Vector2(Mathf.Min(145f, Mathf.Max(110f, bounds.rect.width / 8f)), 68f);
        float halfH = nodeSize.y * 0.5f;
        float usableWidth = Mathf.Max(0f, bounds.rect.width - nodeSize.x - 2f * horizontalInset);
        float xStep = usableWidth / (RunMapGenerator.RouteColumnCount + 1);
        float laneGap = Mathf.Min(laneSpacing, Mathf.Max(0f, bounds.rect.height - 2f * (verticalInset + halfH)) * 0.5f);
        Vector2 center = bounds.rect.center;

        Vector2 PositionFor(int column, int row)
        {
            float x = center.x - usableWidth * 0.5f + column * xStep;
            float y = center.y + (1 - row) * laneGap;
            return new Vector2(x, y);
        }

        Vector2 startPosition = PositionFor(0, 1);
        Vector2 bossPosition = PositionFor(RunMapGenerator.RouteColumnCount + 1, 1);
        foreach (var node in run.currentPath)
            _nodePositions[node.id] = PositionFor(Mathf.Clamp(Mathf.RoundToInt(node.col), 0, RunMapGenerator.RouteColumnCount - 1) + 1,
                Mathf.Clamp(Mathf.RoundToInt(node.row), 0, RunMapGenerator.RowCount - 1));

        foreach (var node in run.currentPath)
        {
            foreach (int nextId in node.next)
            {
                var next = run.currentPath.Find(candidate => candidate.id == nextId);
                if (next == null || next.kind == MapNodeType.Boss || !Mathf.Approximately(next.col, node.col + 1f)) continue;
                CreateConnection(_nodePositions[node.id], _nodePositions[next.id], nodeSize);
            }
        }

        foreach (var node in run.currentPath.Where(value => Mathf.Approximately(value.col, 0f)))
            CreateConnection(startPosition, _nodePositions[node.id], nodeSize);
        var bossNode = run.currentPath.FirstOrDefault(value => value.kind == MapNodeType.Boss);
        if (bossNode != null)
        {
            _nodePositions[bossNode.id] = bossPosition;
            foreach (var predecessor in run.currentPath.Where(value => value.next.Contains(bossNode.id)))
                CreateConnection(_nodePositions[predecessor.id], bossPosition, nodeSize);
        }

        CreateStartEndpoint(startPosition, nodeSize);
        foreach (var node in run.currentPath)
        {
            var buttonObject = Instantiate(nodeButtonPrefab, mapContainer);
            buttonObject.name = $"Node_{node.id}_{node.kind}";
            var nodeButtonUI = buttonObject.GetComponent<PathNodeButtonUI>();
            if (nodeButtonUI == null) nodeButtonUI = buttonObject.AddComponent<PathNodeButtonUI>();
            nodeButtonUI.SetNode(node, run);
            PlaceNode(buttonObject.GetComponent<RectTransform>(), _nodePositions[node.id], nodeSize);

            var button = buttonObject.GetComponent<Button>();
            if (button)
            {
                button.interactable = !node.completed && node.accessible;
                var image = buttonObject.GetComponent<Image>();
                if (image) image.color = GetNodeColor(node);
                int id = node.id;
                button.onClick.AddListener(() => run.OnPathChosen(id));
            }
        }
    }

    void CreateStartEndpoint(Vector2 position, Vector2 nodeSize)
    {
        var endpoint = Instantiate(nodeButtonPrefab, mapContainer);
        endpoint.name = "RouteStart";
        var button = endpoint.GetComponent<Button>();
        if (button) button.interactable = false;
        var image = endpoint.GetComponent<Image>();
        if (image) image.color = new Color(0.28f, 0.34f, 0.44f);
        var text = endpoint.GetComponentInChildren<TMP_Text>();
        if (text) text.text = "START";
        PlaceNode(endpoint.GetComponent<RectTransform>(), position, nodeSize);
    }

    static void PlaceNode(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    void CreateConnection(Vector2 from, Vector2 to, Vector2 nodeSize)
    {
        Vector2 delta = to - from;
        if (delta.sqrMagnitude < 0.001f) return;
        Vector2 direction = delta.normalized;
        float sourceExtent = Mathf.Min(nodeSize.x * 0.5f / Mathf.Max(0.001f, Mathf.Abs(direction.x)),
            nodeSize.y * 0.5f / Mathf.Max(0.001f, Mathf.Abs(direction.y)));
        Vector2 fromEdge = from + direction * sourceExtent;
        Vector2 toEdge = to - direction * sourceExtent;
        delta = toEdge - fromEdge;

        var connection = new GameObject("RouteConnection", typeof(RectTransform), typeof(Image));
        connection.layer = gameObject.layer;
        connection.transform.SetParent(mapContainer, false);
        connection.transform.SetAsFirstSibling();
        var rect = connection.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = (fromEdge + toEdge) * 0.5f;
        rect.sizeDelta = new Vector2(delta.magnitude, 5f);
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

}
