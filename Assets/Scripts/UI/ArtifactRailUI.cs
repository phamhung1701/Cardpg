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

    [Header("Manual merge")]
    public Button mergeModeButton;
    public Button mergeConfirmButton;
    public Button mergeCancelButton;
    public TMP_Text mergeStatusText;

    public GameObject detailPanel;
    public TMP_Text detailText;
    public Button detailCloseButton;

    readonly List<ArtifactIconSlotUI> _slots = new();
    CardManager _subscribedSource;
    RelicData _hovered;
    RelicData _selected;
    bool _mergeSelecting;
    bool _mergeAwaitingConfirmation;
    long _firstMergeId;
    long _secondMergeId;
    RelicData _mergePreview;

    void OnEnable()
    {
        EnsureMergeControls();
        if (detailCloseButton) detailCloseButton.onClick.AddListener(CloseDetail);
        if (mergeModeButton) mergeModeButton.onClick.AddListener(BeginMergeSelection);
        if (mergeConfirmButton) mergeConfirmButton.onClick.AddListener(ConfirmMerge);
        if (mergeCancelButton) mergeCancelButton.onClick.AddListener(CancelMerge);
        HideTooltip();
        CloseDetail();
        Refresh();
    }

    void OnDisable()
    {
        if (_subscribedSource) _subscribedSource.OnBuildChanged -= Refresh;
        _subscribedSource = null;
        if (detailCloseButton) detailCloseButton.onClick.RemoveListener(CloseDetail);
        if (mergeModeButton) mergeModeButton.onClick.RemoveListener(BeginMergeSelection);
        if (mergeConfirmButton) mergeConfirmButton.onClick.RemoveListener(ConfirmMerge);
        if (mergeCancelButton) mergeCancelButton.onClick.RemoveListener(CancelMerge);
        HideTooltip();
        CloseDetail();
        _mergeSelecting = false;
        _mergeAwaitingConfirmation = false;
        _firstMergeId = _secondMergeId = 0;
        _mergePreview = null;
        UpdateMergeControls();
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
        int artifactIndex = 0;
        if (cards && cards.ownedArtifacts != null)
        {
            foreach (var artifact in cards.ownedArtifacts)
            {
                int currentArtifactIndex = artifactIndex++;
                if (!artifact) continue;
                if (slotsRoot)
                {
                    if (count == _slots.Count) _slots.Add(CreateSlot());
                    var slot = _slots[count];
                    var instanceId = cards.GetArtifactInstanceAt(currentArtifactIndex)?.Id ?? 0;
                    slot.Bind(artifact, ShowTooltip, HideTooltipFor, ShowDetail,
                        instanceId, SelectForMerge, _firstMergeId == instanceId || _secondMergeId == instanceId);
                    slot.gameObject.SetActive(true);
                }
                count++;
            }
        }

        for (int i = count; i < _slots.Count; i++)
            _slots[i].gameObject.SetActive(false);

        if (_hovered && (!cards || !cards.ownedArtifacts.Contains(_hovered))) HideTooltip();
        if (_selected && (!cards || !cards.ownedArtifacts.Contains(_selected))) CloseDetail();
        if (_mergeSelecting && (!cards ||
            (_firstMergeId != 0 && cards.GetArtifactInstanceById(_firstMergeId) == null) ||
            (_secondMergeId != 0 && cards.GetArtifactInstanceById(_secondMergeId) == null)))
        {
            _firstMergeId = _secondMergeId = 0;
            _mergeAwaitingConfirmation = false;
            _mergePreview = null;
        }
        UpdateMergeControls();
    }

    void EnsureMergeControls()
    {
        if (mergeModeButton && mergeConfirmButton && mergeCancelButton && mergeStatusText) return;
        var controlsRoot = slotsRoot && slotsRoot.parent ? slotsRoot.parent.parent as RectTransform : null;
        if (!controlsRoot) controlsRoot = transform as RectTransform;
        if (!controlsRoot) return;

        if (!mergeModeButton)
            mergeModeButton = CreateMergeButton(controlsRoot, "Merge Artifacts", "Merge Artifacts", new Vector2(0f, -130f), new Vector2(148f, 34f));
        if (!mergeConfirmButton)
            mergeConfirmButton = CreateMergeButton(controlsRoot, "Confirm Artifact Merge", "Merge", new Vector2(-39f, -171f), new Vector2(72f, 34f));
        if (!mergeCancelButton)
            mergeCancelButton = CreateMergeButton(controlsRoot, "Cancel Artifact Merge", "Cancel", new Vector2(39f, -171f), new Vector2(72f, 34f));
        if (!mergeStatusText)
        {
            var statusObject = new GameObject("Artifact Merge Status", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rect = (RectTransform)statusObject.transform;
            rect.SetParent(controlsRoot, false);
            SetTopAnchoredRect(rect, new Vector2(0f, -207f), new Vector2(160f, 34f));
            mergeStatusText = statusObject.GetComponent<TMP_Text>();
            CopyTextStyle(mergeStatusText);
            mergeStatusText.fontSize = 13f;
            mergeStatusText.enableAutoSizing = true;
            mergeStatusText.fontSizeMin = 9f;
            mergeStatusText.fontSizeMax = 13f;
            mergeStatusText.alignment = TextAlignmentOptions.Center;
            mergeStatusText.raycastTarget = false;
        }

        // Reserve a strip above the scrollable artifact icons for the generated controls.
        if (slotsRoot && slotsRoot.parent is RectTransform viewport && viewport.parent == controlsRoot)
        {
            viewport.anchoredPosition += Vector2.down * 125f;
            viewport.sizeDelta += Vector2.down * 188f;
        }
    }

    Button CreateMergeButton(RectTransform parent, string objectName, string label, Vector2 position, Vector2 size)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        SetTopAnchoredRect(rect, position, size);
        var image = go.GetComponent<Image>();
        image.color = new Color(0.19f, 0.31f, 0.45f, 0.98f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(3f, 1f);
        labelRect.offsetMax = new Vector2(-3f, -1f);
        var text = labelObject.GetComponent<TMP_Text>();
        CopyTextStyle(text);
        text.text = label;
        text.fontSize = 14f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 14f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return button;
    }

    void CopyTextStyle(TMP_Text text)
    {
        if (capacityLabel && capacityLabel.font) text.font = capacityLabel.font;
        text.color = Color.white;
    }

    static void SetTopAnchoredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
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

    void BeginMergeSelection()
    {
        _mergeSelecting = true;
        _mergeAwaitingConfirmation = false;
        _firstMergeId = _secondMergeId = 0;
        _mergePreview = null;
        Refresh();
    }

    void SelectForMerge(long instanceId, RelicData artifact)
    {
        if (!_mergeSelecting || instanceId == 0) return;
        if (_firstMergeId == instanceId) _firstMergeId = 0;
        else if (_secondMergeId == instanceId) _secondMergeId = 0;
        else if (_firstMergeId == 0) _firstMergeId = instanceId;
        else if (_secondMergeId == 0) _secondMergeId = instanceId;
        _mergeAwaitingConfirmation = false;
        _mergePreview = null;
        var cards = source ? source : CardManager.Instance;
        var first = cards ? cards.GetArtifactInstanceById(_firstMergeId) : null;
        var second = cards ? cards.GetArtifactInstanceById(_secondMergeId) : null;
        if (first != null && second != null && first.Definition != null && second.Definition != null &&
            first.Definition.canonicalId == second.Definition.canonicalId &&
            first.Definition.tier == second.Definition.tier && first.Definition.tier >= 1)
        {
            RelicData nextCandidate = null;
            int nextCandidateCount = 0;
            foreach (var value in cards.relicCatalog)
            {
                if (!value || value.tier != first.Definition.tier + 1 ||
                    value.upgradeFromId != first.Definition.canonicalId) continue;
                nextCandidate = value;
                nextCandidateCount++;
            }
            if (nextCandidateCount == 1) _mergePreview = nextCandidate;
        }
        _mergeAwaitingConfirmation = _mergePreview != null;
        Refresh();
    }

    void ConfirmMerge()
    {
        if (!_mergeAwaitingConfirmation || !_mergePreview) return;
        var cards = source ? source : CardManager.Instance;
        if (!cards || cards.GetArtifactInstanceById(_firstMergeId) == null ||
            cards.GetArtifactInstanceById(_secondMergeId) == null || _firstMergeId == _secondMergeId ||
            !cards.MergeArtifacts(_firstMergeId, _secondMergeId))
        {
            CancelMerge();
            return;
        }
        CancelMerge();
    }

    void CancelMerge()
    {
        _mergeSelecting = false;
        _mergeAwaitingConfirmation = false;
        _firstMergeId = _secondMergeId = 0;
        _mergePreview = null;
        UpdateMergeControls();
        Refresh();
    }

    void UpdateMergeControls()
    {
        if (mergeModeButton) mergeModeButton.interactable = !_mergeSelecting;
        if (mergeConfirmButton) mergeConfirmButton.interactable = _mergeAwaitingConfirmation;
        if (mergeCancelButton) mergeCancelButton.interactable = _mergeSelecting;
        if (mergeStatusText)
        {
            if (!_mergeSelecting) mergeStatusText.text = string.Empty;
            else if (_mergePreview) mergeStatusText.text = $"Merge into {_mergePreview.displayName}? Select Merge to confirm.";
            else if (_firstMergeId != 0 && _secondMergeId != 0)
                mergeStatusText.text = "These copies cannot be merged. Select a matching pair.";
            else mergeStatusText.text = $"Select two matching artifact copies ({(_firstMergeId != 0 ? 1 : 0) + (_secondMergeId != 0 ? 1 : 0)}/2).";
        }
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
