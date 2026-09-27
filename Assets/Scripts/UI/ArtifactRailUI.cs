using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays the CardManager artifact build as a row of icons, with on-demand text.</summary>
public sealed class ArtifactRailUI : MonoBehaviour
{
    [Header("Source and icon rail")]
    public CardManager source;
    public RectTransform slotsRoot;
    public TMP_Text capacityLabel;

    [Header("Hover tooltip")]
    public GameObject tooltipPanel;
    public TMP_Text tooltipText;

    [Header("Click detail")]
    public GameObject detailPanel;
    public TMP_Text detailText;
    public Button detailCloseButton;

    readonly List<ArtifactIconSlotUI> _slots = new();
    CardManager _subscribedSource;
    RelicData _hovered;
    RelicData _selected;

    void OnEnable()
    {
        if (detailCloseButton) detailCloseButton.onClick.AddListener(CloseDetail);
        HideTooltip();
        CloseDetail();
        Refresh();
    }

    void OnDisable()
    {
        if (_subscribedSource) _subscribedSource.OnBuildChanged -= Refresh;
        _subscribedSource = null;
        if (detailCloseButton) detailCloseButton.onClick.RemoveListener(CloseDetail);
        HideTooltip();
        CloseDetail();
    }

    // A rail enabled before CardManager is created can still attach as soon as it appears.
    void Update()
    {
        if (!_subscribedSource && (source || CardManager.Instance)) Refresh();
    }

    public void Refresh()
    {
        var cards = source ? source : CardManager.Instance;
        if (_subscribedSource != cards)
        {
            if (_subscribedSource) _subscribedSource.OnBuildChanged -= Refresh;
            _subscribedSource = cards;
            if (_subscribedSource) _subscribedSource.OnBuildChanged += Refresh;
        }

        if (capacityLabel)
            capacityLabel.text = $"{(cards ? cards.ArtifactSlotsUsed : 0)}/{(cards ? cards.ArtifactCapacity : CardManager.BASE_ARTIFACT_CAPACITY)}";

        int count = 0;
        if (cards && cards.ownedArtifacts != null)
        {
            foreach (var artifact in cards.ownedArtifacts)
            {
                if (!artifact) continue;
                if (slotsRoot)
                {
                    if (count == _slots.Count) _slots.Add(CreateSlot());
                    var slot = _slots[count];
                    slot.Bind(artifact, ShowTooltip, HideTooltipFor, ShowDetail);
                    slot.gameObject.SetActive(true);
                }
                count++;
            }
        }

        for (int i = count; i < _slots.Count; i++)
            _slots[i].gameObject.SetActive(false);

        if (_hovered && (!cards || !cards.ownedArtifacts.Contains(_hovered))) HideTooltip();
        if (_selected && (!cards || !cards.ownedArtifacts.Contains(_selected))) CloseDetail();
    }

    ArtifactIconSlotUI CreateSlot()
    {
        var go = new GameObject("Artifact Icon", typeof(RectTransform), typeof(Image), typeof(ArtifactIconSlotUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(slotsRoot, false);
        rect.sizeDelta = new Vector2(64f, 64f);
        go.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.22f, 0.85f);

        var textObject = new GameObject("Icon", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = (RectTransform)textObject.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var label = textObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 42f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 42f;
        label.raycastTarget = false;
        return go.GetComponent<ArtifactIconSlotUI>();
    }

    void ShowTooltip(RelicData artifact)
    {
        _hovered = artifact;
        if (tooltipText) tooltipText.text = Describe(artifact);
        if (tooltipPanel) tooltipPanel.SetActive(true);
    }

    void HideTooltipFor(RelicData artifact)
    {
        if (_hovered == artifact) HideTooltip();
    }

    void HideTooltip()
    {
        _hovered = null;
        if (tooltipPanel) tooltipPanel.SetActive(false);
    }

    void ShowDetail(RelicData artifact)
    {
        _selected = artifact;
        HideTooltip();
        if (detailText) detailText.text = Describe(artifact);
        if (detailPanel)
        {
            detailPanel.SetActive(true);
            detailPanel.transform.SetAsLastSibling();
        }
    }

    public void CloseDetail()
    {
        _selected = null;
        if (detailPanel) detailPanel.SetActive(false);
    }

    static string Describe(RelicData artifact) => artifact
        ? $"{artifact.displayName}\n{artifact.description}" : string.Empty;
}
