using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class CardView : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum State { Idle, Hover, Click, Drag, Release }

    [Header("References")]
    public Image face;
    public TMP_Text nameLabel;
    public TMP_Text stateLabel;
    public CanvasGroup canvasGroup;

    [Header("State")]
    [NonSerialized] public CardInstance data;
    public Field homeField;
    public int index;

    State _state = State.Idle;
    bool _isSelected;
    bool _isPointerOver;
    bool _isDragging;
    Vector3 _originalPosition;
    Transform _originalParent;

    public State CurrentState => _state;
    public bool IsSelected => _isSelected;

    static readonly Color _idleColor = new Color(0.85f, 0.92f, 0.85f);
    static readonly Color _hoverColor = new Color(0.95f, 0.95f, 1f);
    static readonly Color _clickColor = new Color(1f, 0.85f, 0.6f);
    static readonly Color _dragColor = new Color(0.7f, 0.85f, 1f);

    public void Setup(CardInstance cardData)
    {
        data = cardData;
        if (nameLabel) nameLabel.text = data != null ? data.DisplayName : string.Empty;
        RefreshState();
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        RefreshState();
    }

    void RefreshState()
    {
        State next = _isDragging
            ? State.Drag
            : _isSelected
                ? State.Click
                : _isPointerOver
                    ? State.Hover
                    : State.Idle;

        ChangeState(next);
    }

    void ChangeState(State next)
    {
        if (_state == next)
        {
            ApplyState(next);
            return;
        }

        _state = next;
        ApplyState(next);
    }

    void ApplyState(State state)
    {
        switch (state)
        {
            case State.Idle:
                if (face) face.color = _idleColor;
                if (stateLabel) stateLabel.text = string.Empty;
                break;
            case State.Hover:
                if (face) face.color = _hoverColor;
                if (stateLabel) stateLabel.text = "Hover";
                break;
            case State.Click:
                if (face) face.color = _clickColor;
                if (stateLabel) stateLabel.text = "Selected";
                break;
            case State.Drag:
                if (face) face.color = _dragColor;
                if (stateLabel) stateLabel.text = "Drag";
                break;
            case State.Release:
                break;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;
        RefreshState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOver = false;
        RefreshState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (Singleton<CardManager>.TryGetInstance(out var manager))
            manager.SelectCard(this);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Selection is persistent and is owned by CardManager. A normal release does not deselect.
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || _isDragging) return;

        _originalParent = transform.parent;
        _originalPosition = transform.position;
        index = transform.GetSiblingIndex();
        _isDragging = true;

        if (Singleton<CardManager>.TryGetInstance(out var manager) && manager.dragCanvas != null)
        {
            transform.SetParent(manager.dragCanvas.transform, true);
            transform.SetAsLastSibling();
        }

        if (canvasGroup) canvasGroup.blocksRaycasts = false;
        RefreshState();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_isDragging)
            transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        Field targetField = null;
        CardView leftCard = null;
        CardView rightCard = null;
        var results = new List<RaycastResult>();
        if (EventSystem.current != null)
            EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            if (result.gameObject == gameObject) continue;

            if (targetField == null)
                targetField = result.gameObject.GetComponentInParent<Field>();

            var otherCard = result.gameObject.GetComponentInParent<CardView>();
            if (otherCard == null || otherCard == this || otherCard.homeField != homeField)
                continue;

            targetField ??= otherCard.homeField;
            if (leftCard == null)
                leftCard = otherCard;
            else if (otherCard != leftCard)
            {
                rightCard = otherCard;
                break;
            }
        }

        if (targetField == homeField && homeField != null)
            homeField.RepositionCard(this, leftCard, rightCard);
        else
            RestoreOriginalTransform();

        FinishDrag();
    }

    public void ForceToIdle()
    {
        if (_isDragging)
        {
            RestoreOriginalTransform();
            FinishDrag();
        }

        _isSelected = false;
        _isPointerOver = false;
        RefreshState();
    }

    void RestoreOriginalTransform()
    {
        var parent = _originalParent;
        if (parent == null && homeField != null)
            parent = homeField.cardsHolder;
        if (parent == null) return;

        transform.SetParent(parent, true);
        transform.SetSiblingIndex(Mathf.Clamp(index, 0, parent.childCount - 1));
        transform.position = _originalPosition;
    }

    void FinishDrag()
    {
        _isDragging = false;
        if (canvasGroup) canvasGroup.blocksRaycasts = true;
        _originalParent = null;
        RefreshState();
    }
}
