using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One glyph-only artifact slot. The rail supplies its tooltip and detail actions.</summary>
public sealed class ArtifactIconSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] TMP_Text iconLabel;
    Image _authoredIcon;

    RelicData _artifact;
    Action<RelicData> _hover;
    Action<RelicData> _leave;
    Action<RelicData> _select;
    Action<long, RelicData> _mergeSelect;
    long _instanceId;
    Sprite _occupiedSlotSprite;
    Sprite _emptySlotSprite;
    Sprite _selectedSlotSprite;
    bool _mergeSelected;
    bool _mergeMode;
    bool _mergeEligible = true;

    public RelicData Artifact => _artifact;

    public void SetSlotSprites(Sprite occupied, Sprite empty, Sprite selected)
    {
        _occupiedSlotSprite = occupied;
        _emptySlotSprite = empty;
        _selectedSlotSprite = selected;
        RefreshSlotBackground();
    }

    public void BindEmpty()
    {
        _artifact = null;
        _hover = null;
        _leave = null;
        _select = null;
        _mergeSelect = null;
        _instanceId = 0;
        _mergeSelected = false;
        _mergeMode = false;
        _mergeEligible = true;
        if (TryGetComponent<CanvasGroup>(out var group)) group.alpha = 1f;
        if (TryGetComponent<Outline>(out var outline)) outline.enabled = false;
        if (iconLabel) iconLabel.gameObject.SetActive(false);
        if (_authoredIcon) _authoredIcon.gameObject.SetActive(false);
        var image = GetComponent<Image>();
        if (image) image.raycastTarget = false;
        RefreshSlotBackground();
    }

    public void Bind(RelicData artifact, Action<RelicData> hover, Action<RelicData> leave,
        Action<RelicData> select, long instanceId, Action<long, RelicData> mergeSelect, bool mergeSelected,
        bool mergeMode = false, bool mergeEligible = true)
    {
        _artifact = artifact;
        _hover = hover;
        _leave = leave;
        _select = select;
        _instanceId = instanceId;
        _mergeSelect = mergeSelect;
        _mergeSelected = mergeSelected;
        _mergeMode = mergeMode;
        _mergeEligible = mergeEligible;
        var group = GetComponent<CanvasGroup>();
        if (!group) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = mergeMode && !mergeEligible && !mergeSelected ? 0.4f : 1f;
        var outline = GetComponent<Outline>();
        if (!outline) outline = gameObject.AddComponent<Outline>();
        outline.effectColor = mergeSelected ? new Color(1f, 0.72f, 0.2f) : new Color(0.4f, 0.85f, 0.65f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.enabled = mergeMode && (mergeEligible || mergeSelected);
        var image = GetComponent<Image>();
        if (image) image.raycastTarget = true;
        if (image) image.preserveAspect = true;
        RefreshSlotBackground();
        if (!iconLabel) iconLabel = GetComponentInChildren<TMP_Text>();
        bool hasAuthoredIcon = artifact != null && artifact.iconSprite;
        if (hasAuthoredIcon)
        {
            EnsureAuthoredIcon();
            _authoredIcon.sprite = artifact.iconSprite;
            _authoredIcon.gameObject.SetActive(true);
        }
        else if (_authoredIcon)
            _authoredIcon.gameObject.SetActive(false);
        if (iconLabel)
        {
            iconLabel.color = _mergeSelected ? new Color(0.11f, 0.13f, 0.2f) : new Color(0.96f, 0.92f, 0.78f);
            iconLabel.text = artifact != null && !string.IsNullOrWhiteSpace(artifact.icon)
                ? artifact.icon : "?";
            iconLabel.gameObject.SetActive(!hasAuthoredIcon);
        }
    }

    void EnsureAuthoredIcon()
    {
        if (_authoredIcon) return;
        var iconObject = new GameObject("Authored Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)iconObject.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(0.08f, 0.08f);
        rect.anchorMax = new Vector2(0.92f, 0.92f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        _authoredIcon = iconObject.GetComponent<Image>();
        _authoredIcon.preserveAspect = true;
        _authoredIcon.raycastTarget = false;
    }

    void RefreshSlotBackground()
    {
        var image = GetComponent<Image>();
        if (image == null) return;
        var sprite = _artifact == null ? _emptySlotSprite
            : _mergeSelected && _selectedSlotSprite != null ? _selectedSlotSprite : _occupiedSlotSprite;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }
        else
            image.color = _artifact == null ? new Color(0.12f, 0.16f, 0.22f, 0.55f)
                : _mergeSelected ? new Color(0.25f, 0.58f, 0.34f, 0.95f)
                : new Color(0.16f, 0.20f, 0.27f, 0.78f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_artifact) _hover?.Invoke(_artifact);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_artifact) _leave?.Invoke(_artifact);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_artifact && !GameplayInputGate.IsBlocked && eventData.button == PointerEventData.InputButton.Left)
        {
            if (_mergeMode)
            {
                if (_mergeEligible || _mergeSelected) _mergeSelect?.Invoke(_instanceId, _artifact);
            }
            else _select?.Invoke(_artifact);
        }
    }

    void OnDisable()
    {
        if (_artifact) _leave?.Invoke(_artifact);
    }
}
