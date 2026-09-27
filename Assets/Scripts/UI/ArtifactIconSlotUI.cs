using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One glyph-only artifact slot. The rail supplies its tooltip and detail actions.</summary>
public sealed class ArtifactIconSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] TMP_Text iconLabel;

    RelicData _artifact;
    Action<RelicData> _hover;
    Action<RelicData> _leave;
    Action<RelicData> _select;

    public RelicData Artifact => _artifact;

    public void Bind(RelicData artifact, Action<RelicData> hover, Action<RelicData> leave,
        Action<RelicData> select)
    {
        _artifact = artifact;
        _hover = hover;
        _leave = leave;
        _select = select;
        if (!iconLabel) iconLabel = GetComponentInChildren<TMP_Text>();
        if (iconLabel) iconLabel.text = artifact != null && !string.IsNullOrWhiteSpace(artifact.icon)
            ? artifact.icon : "?";
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
            _select?.Invoke(_artifact);
    }

    void OnDisable()
    {
        if (_artifact) _leave?.Invoke(_artifact);
    }
}
