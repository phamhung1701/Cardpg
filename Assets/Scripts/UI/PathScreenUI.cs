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

        var rm = RunManager.Instance;
        foreach (var node in rm.currentPath)
        {
            var btn = Instantiate(nodeButtonPrefab, mapContainer);
            var text = btn.GetComponentInChildren<TMP_Text>();
            if (text)
            {
                text.text = node.revealed
                    ? $"{rm.GetNodeLabel(node)}\n{rm.GetNodeDesc(node)}"
                    : "?\n???";
            }

            btn.transform.localPosition = GridToLocal(node.col, node.row);

            var button = btn.GetComponent<Button>();
            if (button)
            {
                button.interactable = !node.completed && node.accessible;
                var img = btn.GetComponent<Image>();
                if (img)
                {
                    img.color = node.completed ? new Color(0.6f, 1f, 0.6f)
                              : !node.accessible ? new Color(0.5f, 0.5f, 0.5f)
                              : Color.white;
                }
                int id = node.id;
                button.onClick.AddListener(() => rm.OnPathChosen(id));
            }
        }
    }

    Vector3 GridToLocal(float col, float row)
    {
        return new Vector3(col * colSpacing + mapOffset.x, -row * rowSpacing + mapOffset.y, 0);
    }
}
