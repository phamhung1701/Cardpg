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

    [Header("Map Settings")]
    public float colSpacing = 200f;
    public float rowSpacing = 120f;
    public Vector2 mapOffset = new(180f, 120f);

    void OnEnable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowPathScreen += Show;
            RunManager.Instance.OnHidePathScreen += Hide;
            RunManager.Instance.OnRunCompleted += HandleRunCompleted;
            RunManager.Instance.OnCycleStarted += HandleCycleStarted;
        }
    }

    void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowPathScreen -= Show;
            RunManager.Instance.OnHidePathScreen -= Hide;
            RunManager.Instance.OnRunCompleted -= HandleRunCompleted;
            RunManager.Instance.OnCycleStarted -= HandleCycleStarted;
        }
    }

    void HandleCycleStarted(string header)
    {
        if (headerText) headerText.text = header;
        BuildMap();
    }

    void HandleRunCompleted()
    {
        if (headerText) headerText.text = "VICTORY!";
        if (panel) panel.SetActive(true);
    }

    void Show()
    {
        BuildMap();
        if (panel) panel.SetActive(true);
    }

    void Hide()
    {
        if (panel) panel.SetActive(false);
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
            var label = buttonObject.GetComponentInChildren<TMP_Text>();
            if (label)
            {
                string heading = GetNodeHeading(node, run);
                label.text = node.revealed || node.completed
                    ? $"{heading}\n<size=70%>{run.GetNodeDesc(node)}</size>"
                    : "?\n<size=70%>Unknown route</size>";
            }

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
        rect.sizeDelta = new Vector2(delta.magnitude, 4f);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        var image = connection.GetComponent<Image>();
        image.color = new Color(0.28f, 0.34f, 0.44f, 0.8f);
        image.raycastTarget = false;
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
