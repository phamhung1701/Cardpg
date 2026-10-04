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

    public RelicData Artifact => _artifact;

    public void Bind(RelicData artifact, Action<RelicData> hover, Action<RelicData> leave,
        Action<RelicData> select, long instanceId, Action<long, RelicData> mergeSelect, bool mergeSelected)
    {
        _artifact = artifact;
        _hover = hover;
        _leave = leave;
        _select = select;
        _instanceId = instanceId;
        _mergeSelect = mergeSelect;
        var image = GetComponent<Image>();
        if (image) image.color = mergeSelected ? new Color(0.25f, 0.58f, 0.34f, 0.95f) : new Color(0.12f, 0.16f, 0.22f, 0.85f);
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
        rect.anchorMin = new Vector2(0.1f, 0.1f);
        rect.anchorMax = new Vector2(0.9f, 0.9f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        _authoredIcon = iconObject.GetComponent<Image>();
        _authoredIcon.preserveAspect = true;
        _authoredIcon.raycastTarget = false;
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
        if (_artifact && eventData.button == PointerEventData.InputButton.Left)
        {
            _select?.Invoke(_artifact);
            _mergeSelect?.Invoke(_instanceId, _artifact);
        }
    }

    void OnDisable()
    {
        if (_artifact) _leave?.Invoke(_artifact);
    }
}
