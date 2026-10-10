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

    [Header("Visual Style Test")]
    public Sprite occupiedSlotSprite;
    public Sprite emptySlotSprite;
    public Sprite selectedSlotSprite;

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
    long _selectedInstanceId;
    bool _mergeSelecting;
    bool _mergeAwaitingConfirmation;
    long _firstMergeId;
    long _secondMergeId;
    int _ownedArtifactCount;
    RelicData _mergePreview;
    PathNode _mergeNode;
    GameState _mergeCombatState;
    Vector2 _lastDetailParentSize;
    bool _hasLastDetailParentSize;

    void OnEnable()
    {
        EnsureMergeControls();
        if (detailCloseButton) detailCloseButton.onClick.AddListener(CloseDetail);
        if (mergeModeButton) mergeModeButton.onClick.AddListener(BeginMergeSelection);
        if (mergeConfirmButton) mergeConfirmButton.onClick.AddListener(ConfirmMerge);
        if (mergeCancelButton) mergeCancelButton.onClick.AddListener(CancelMerge);
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += HandleRunStarted;
            RunManager.Instance.OnHideShop += HandleContextEnded;
            RunManager.Instance.OnShowPathScreen += HandleContextEnded;
            RunManager.Instance.OnHidePathScreen += HandleContextEnded;
        }
        if (CombatManager.Instance != null) CombatManager.Instance.OnEncounterResult += HandleEncounterResult;
        HideTooltip();
        CloseDetail();
        Refresh();
    }

    void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= HandleRunStarted;
            RunManager.Instance.OnHideShop -= HandleContextEnded;
            RunManager.Instance.OnShowPathScreen -= HandleContextEnded;
            RunManager.Instance.OnHidePathScreen -= HandleContextEnded;
        }
        if (CombatManager.Instance != null) CombatManager.Instance.OnEncounterResult -= HandleEncounterResult;
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
        if (_mergeSelecting && (GameplayInputGate.IsBlocked ||
            _subscribedSource != null && _subscribedSource.IsHandEnhancementTargeting ||
            RunManager.Instance?.ActiveNode != _mergeNode ||
            CombatManager.Instance != null && CombatManager.Instance.currentState != _mergeCombatState)) CancelMerge();
        if (_selectedInstanceId != 0 && detailPanel && detailPanel.activeSelf &&
            detailPanel.transform.parent is RectTransform detailParent &&
            (!_hasLastDetailParentSize || (detailParent.rect.size - _lastDetailParentSize).sqrMagnitude > 0.25f))
            PositionDetailBesideSlot(_selectedInstanceId);
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

        if (_mergeSelecting && (!cards || cards.ownedArtifacts.Count < 2 ||
            (_firstMergeId != 0 && cards.GetArtifactInstanceById(_firstMergeId) == null) ||
            (_secondMergeId != 0 && cards.GetArtifactInstanceById(_secondMergeId) == null)))
        {
            _mergeSelecting = false;
            _firstMergeId = _secondMergeId = 0;
            _mergeAwaitingConfirmation = false;
            _mergePreview = null;
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
                    slot.SetSlotSprites(occupiedSlotSprite, emptySlotSprite, selectedSlotSprite);
                    var instanceId = cards.GetArtifactInstanceAt(currentArtifactIndex)?.Id ?? 0;
                    slot.Bind(artifact, ShowTooltip, HideTooltipFor,
                        selectedArtifact => SelectArtifact(instanceId, selectedArtifact),
                        instanceId, SelectForMerge, _firstMergeId == instanceId || _secondMergeId == instanceId,
                        _mergeSelecting, IsMergeEligible(cards, instanceId), _selectedInstanceId == instanceId);
                    slot.gameObject.SetActive(true);
                }
                count++;
            }
        }

        int emptySlotCount = cards ? cards.ArtifactCapacity : CardManager.BASE_ARTIFACT_CAPACITY;
        int visibleSlotCount = Mathf.Max(count, emptySlotCount);
        _ownedArtifactCount = count;
        for (int i = count; i < visibleSlotCount; i++)
        {
            if (i == _slots.Count) _slots.Add(CreateSlot());
            var slot = _slots[i];
            slot.SetSlotSprites(occupiedSlotSprite, emptySlotSprite, selectedSlotSprite);
            slot.BindEmpty();
            slot.gameObject.SetActive(true);
        }
        for (int i = visibleSlotCount; i < _slots.Count; i++)
            _slots[i].gameObject.SetActive(false);

        if (_hovered && (!cards || !cards.ownedArtifacts.Contains(_hovered))) HideTooltip();
        if (_selectedInstanceId != 0 && (!cards || cards.GetArtifactInstanceById(_selectedInstanceId) == null)) CloseDetail();
        UpdateMergeControls();
    }

    void EnsureMergeControls()
    {
        if (mergeModeButton && mergeConfirmButton && mergeCancelButton && mergeStatusText) return;
        var controlsRoot = slotsRoot && slotsRoot.parent ? slotsRoot.parent.parent as RectTransform : null;
        if (!controlsRoot) controlsRoot = transform as RectTransform;
        if (!controlsRoot) return;

        if (!mergeModeButton)
            mergeModeButton = CreateMergeButton(controlsRoot, "Merge Artifacts", "Merge", new Vector2(0f, -64f), new Vector2(88f, 24f));
        if (!mergeConfirmButton)
            mergeConfirmButton = CreateMergeButton(controlsRoot, "Confirm Artifact Merge", "Confirm", new Vector2(-35f, -94f), new Vector2(64f, 24f));
        if (!mergeCancelButton)
            mergeCancelButton = CreateMergeButton(controlsRoot, "Cancel Artifact Merge", "Cancel", new Vector2(35f, -94f), new Vector2(64f, 24f));
        if (!mergeStatusText)
        {
            var statusObject = new GameObject("Artifact Merge Status", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rect = (RectTransform)statusObject.transform;
            rect.SetParent(controlsRoot, false);
            SetTopAnchoredRect(rect, new Vector2(0f, -126f), new Vector2(160f, 28f));
            mergeStatusText = statusObject.GetComponent<TMP_Text>();
            CopyTextStyle(mergeStatusText);
            mergeStatusText.fontSize = 12f;
            mergeStatusText.enableAutoSizing = true;
            mergeStatusText.fontSizeMin = 9f;
            mergeStatusText.fontSizeMax = 12f;
            mergeStatusText.alignment = TextAlignmentOptions.Center;
            mergeStatusText.raycastTarget = false;
        }

        // Reserve a strip above the scrollable artifact icons for the generated controls.
        if (slotsRoot && slotsRoot.parent is RectTransform viewport && viewport.parent == controlsRoot)
        {
            viewport.anchoredPosition += Vector2.down * 58f;
            viewport.sizeDelta += Vector2.down * 92f;
        }
    }

    Button CreateMergeButton(RectTransform parent, string objectName, string label, Vector2 position, Vector2 size)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        SetTopAnchoredRect(rect, position, size);
        var image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.24f, 0.25f, 0.45f);
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
        rect.sizeDelta = new Vector2(44f, 44f);
        var image = go.GetComponent<Image>();
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;

        var textObject = new GameObject("Icon", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = (RectTransform)textObject.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var label = textObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 30f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 30f;
        label.raycastTarget = false;
        return go.GetComponent<ArtifactIconSlotUI>();
    }

    void HandleRunStarted(string _)
    {
        CancelMerge();
        CloseDetail();
    }

    void HandleContextEnded()
    {
        CloseDetail();
        CancelMerge();
    }

    void HandleEncounterResult(EncounterResult _) => CloseDetail();

    bool IsMergeEligible(CardManager cards, long id)
    {
        if (!_mergeSelecting) return true;
        if (id == _firstMergeId || id == _secondMergeId) return true;
        long anchor = _firstMergeId != 0 ? _firstMergeId : _secondMergeId;
        if (anchor != 0) return cards.GetArtifactMergeResult(anchor, id) != null;
        foreach (var candidate in cards.OwnedArtifactInstances)
            if (candidate != null && cards.GetArtifactMergeResult(id, candidate.Id) != null) return true;
        return false;
    }

    void BeginMergeSelection()
    {
        var cards = source ? source : CardManager.Instance;
        if (_ownedArtifactCount < 2 || GameplayInputGate.IsBlocked ||
            cards != null && cards.IsHandEnhancementTargeting) return;
        CloseDetail();
        HideTooltip();
        _mergeNode = RunManager.Instance?.ActiveNode;
        _mergeCombatState = CombatManager.Instance != null ? CombatManager.Instance.currentState : GameState.Idle;
        _mergeSelecting = true;
        _mergeAwaitingConfirmation = false;
        _firstMergeId = _secondMergeId = 0;
        _mergePreview = null;
        Refresh();
    }

    void SelectForMerge(long instanceId, RelicData artifact)
    {
        if (!_mergeSelecting || instanceId == 0 || GameplayInputGate.IsBlocked) return;
        if (_firstMergeId == instanceId) _firstMergeId = 0;
        else if (_secondMergeId == instanceId) _secondMergeId = 0;
        else if (_firstMergeId == 0) _firstMergeId = instanceId;
        else if (_secondMergeId == 0) _secondMergeId = instanceId;
        _mergeAwaitingConfirmation = false;
        _mergePreview = null;
        var cards = source ? source : CardManager.Instance;
        _mergePreview = cards ? cards.GetArtifactMergeResult(_firstMergeId, _secondMergeId) : null;
        _mergeAwaitingConfirmation = _mergePreview != null;
        Refresh();
    }

    void ConfirmMerge()
    {
        if (!_mergeAwaitingConfirmation || !_mergePreview || GameplayInputGate.IsBlocked) return;
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
        bool canMergeMode = _ownedArtifactCount >= 2;
        if (mergeModeButton)
        {
            mergeModeButton.gameObject.SetActive(_mergeSelecting || canMergeMode);
            mergeModeButton.interactable = !_mergeSelecting && canMergeMode;
        }
        if (mergeConfirmButton)
        {
            mergeConfirmButton.gameObject.SetActive(_mergeSelecting);
            mergeConfirmButton.interactable = _mergeAwaitingConfirmation;
        }
        if (mergeCancelButton)
        {
            mergeCancelButton.gameObject.SetActive(_mergeSelecting);
            mergeCancelButton.interactable = _mergeSelecting;
        }
        if (mergeStatusText)
        {
            mergeStatusText.gameObject.SetActive(_mergeSelecting);
            if (!_mergeSelecting) mergeStatusText.text = string.Empty;
            else if (_mergePreview) mergeStatusText.text = $"Merge into {_mergePreview.displayName}? Confirm or Cancel.";
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

    void SelectArtifact(long instanceId, RelicData artifact)
    {
        if (instanceId == 0 || artifact == null) return;
        if (_selectedInstanceId == instanceId)
        {
            CloseDetail();
            Refresh();
            return;
        }
        CardManager.Instance?.ClearSelection();
        FindFirstObjectByType<ActionButtonsUI>()?.ClearSelectedConsumable();
        _selectedInstanceId = instanceId;
        HideTooltip();
        if (detailText) detailText.text = $"<size=115%><b>{artifact.displayName}</b></size>\n<size=85%>{artifact.description}</size>";
        PositionDetailBesideSlot(instanceId);
        if (detailPanel)
        {
            detailPanel.SetActive(true);
            detailPanel.transform.SetAsLastSibling();
        }
        Refresh();
    }

    void PositionDetailBesideSlot(long instanceId)
    {
        if (!detailPanel || !slotsRoot || !(detailPanel.transform is RectTransform detailRect)) return;
        var detailCanvas = detailPanel.GetComponent<Canvas>();
        if (detailCanvas)
        {
            detailCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            detailCanvas.enabled = true;
            detailCanvas.overrideSorting = true;
            detailCanvas.sortingOrder = 60;
        }
        var raycaster = detailPanel.GetComponent<GraphicRaycaster>();
        if (raycaster) raycaster.enabled = true;
        detailRect.anchorMin = detailRect.anchorMax = new Vector2(0.5f, 0.5f);
        detailRect.pivot = new Vector2(0.5f, 0.5f);
        detailRect.sizeDelta = new Vector2(190f, 96f);
        if (detailText)
        {
            var textRect = detailText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 8f);
            textRect.offsetMax = new Vector2(-38f, -8f);
            detailText.fontSize = 18f;
            detailText.enableAutoSizing = true;
            detailText.fontSizeMin = 14f;
            detailText.fontSizeMax = 18f;
            detailText.fontStyle = FontStyles.Normal;
            detailText.alignment = TextAlignmentOptions.TopLeft;
            detailText.overflowMode = TextOverflowModes.Ellipsis;
            float textHeight = detailText.GetPreferredValues(detailText.text,
                Mathf.Max(40f, detailRect.rect.width - 58f), 0f).y;
            detailRect.sizeDelta = new Vector2(190f, Mathf.Clamp(Mathf.Ceil(textHeight + 18f), 96f, 220f));
        }
        var closeRect = detailCloseButton ? detailCloseButton.transform as RectTransform : null;
        if (closeRect)
        {
            closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
            closeRect.pivot = Vector2.one;
            closeRect.sizeDelta = new Vector2(28f, 24f);
            closeRect.anchoredPosition = new Vector2(-4f, -4f);
            var closeLabel = detailCloseButton.GetComponentInChildren<TMP_Text>(true);
            if (closeLabel)
            {
                closeLabel.text = "×";
                closeLabel.fontSize = 18f;
                closeLabel.enableAutoSizing = false;
            }
        }
        var discardText = detailPanel.transform.Find("DiscardPileText");
        if (discardText) discardText.gameObject.SetActive(false);
        var drawViewport = detailPanel.transform.Find("DrawDeckViewport");
        if (drawViewport) drawViewport.gameObject.SetActive(false);
        var panelImage = detailPanel.GetComponent<Image>();
        if (panelImage) panelImage.color = new Color(0.08f, 0.11f, 0.16f, 0.96f);
        var parentRect = detailRect.parent as RectTransform;
        if (parentRect == null) return;
        ArtifactIconSlotUI selectedSlot = null;
        foreach (var slot in _slots)
            if (slot && slot.InstanceId == instanceId) { selectedSlot = slot; break; }
        if (!selectedSlot) return;
        var canvas = parentRect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var slotRect = selectedSlot.transform as RectTransform;
        var corners = new Vector3[4];
        slotRect.GetWorldCorners(corners);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corners[3]);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, camera, out var local)) return;
        float halfWidth = detailRect.rect.width * 0.5f;
        float halfHeight = detailRect.rect.height * 0.5f;
        local.x += halfWidth + 12f;
        if (local.x + halfWidth > parentRect.rect.xMax)
            local.x -= detailRect.rect.width + slotRect.rect.width + 24f;
        local.x = Mathf.Clamp(local.x, parentRect.rect.xMin + halfWidth, parentRect.rect.xMax - halfWidth);
        local.y += slotRect.rect.height * 0.5f;
        local.y = Mathf.Clamp(local.y, parentRect.rect.yMin + halfHeight, parentRect.rect.yMax - halfHeight);
        detailRect.anchoredPosition = local;
        _lastDetailParentSize = parentRect.rect.size;
        _hasLastDetailParentSize = true;
    }

    public void CloseDetail()
    {
        _selectedInstanceId = 0;
        _hasLastDetailParentSize = false;
        if (detailText) detailText.text = string.Empty;
        foreach (var slot in _slots)
            if (slot) slot.SetDetailSelected(false);
        if (detailPanel) detailPanel.SetActive(false);
    }

    static string Describe(RelicData artifact) => artifact
        ? $"{artifact.displayName}\n{artifact.description}" : string.Empty;
}
