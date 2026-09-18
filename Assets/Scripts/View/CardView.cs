using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class CardView : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
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
    int _selectionOrder;
    Vector3 _originalPosition;
    Transform _originalParent;
    Field _dragHomeField;
    Vector3 _pointerOffset;

    public State CurrentState => _state;
    public bool IsSelected => _isSelected;

    static readonly Color _clubColor = new(0.82f, 0.87f, 0.82f);
    static readonly Color _diamondColor = new(0.92f, 0.82f, 0.72f);
    static readonly Color _heartColor = new(0.92f, 0.76f, 0.76f);
    static readonly Color _spadeColor = new(0.78f, 0.82f, 0.9f);
    static readonly Color _selectedColor = new(1f, 0.78f, 0.32f);
    static readonly Color _dragColor = new(0.66f, 0.82f, 1f);

    Color _baseColor = new(0.85f, 0.88f, 0.9f);

    public void Setup(CardInstance cardData)
    {
        data = cardData;
        if (nameLabel)
        {
            if (data == null)
            {
                nameLabel.text = string.Empty;
            }
            else
            {
                string enhancementLine = data.Enhancement != null
                    ? $"\n<size=48%>{data.Enhancement.icon} {data.Enhancement.displayName}</size>"
                    : string.Empty;
                nameLabel.text = $"<b>{data.Definition.RankLabel}</b>\n<size=70%>{data.SuitSymbol}  {data.Definition.SuitName}</size>\n<size=52%>ATK {data.AttackValue}  •  DEF {data.DefenseValue}</size>{enhancementLine}";
            }
        }
        _baseColor = data == null ? new Color(0.85f, 0.88f, 0.9f) : data.Suit switch
        {
            CardData.Suit.Clubs => _clubColor,
            CardData.Suit.Diamonds => _diamondColor,
            CardData.Suit.Hearts => _heartColor,
            _ => _spadeColor
        };
        RefreshState();
    }

    public void SetSelected(bool selected) => SetSelectionOrder(selected ? 1 : 0);

    public void SetSelectionOrder(int selectionOrder)
    {
        _selectionOrder = Mathf.Max(0, selectionOrder);
        _isSelected = _selectionOrder > 0;
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
        Color color = _baseColor;
        Vector3 scale = Vector3.one;

        switch (state)
        {
            case State.Hover:
                color = Color.Lerp(_baseColor, Color.white, 0.35f);
                scale = Vector3.one * 1.04f;
                break;
            case State.Click:
                color = _selectedColor;
                scale = Vector3.one * 1.07f;
                break;
            case State.Drag:
                color = _dragColor;
                scale = Vector3.one * 1.07f;
                break;
        }

        if (face) face.color = color;
        if (stateLabel) stateLabel.text = state == State.Click ? $"SELECTED {_selectionOrder}" : string.Empty;
        transform.localScale = scale;
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
        // Click selection is handled by OnPointerClick so beginning a drag does not toggle
        // an already-selected card out of the intended action set.
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (GameplayInputGate.IsBlocked || eventData.button != PointerEventData.InputButton.Left) return;
        CardManager.Instance?.ToggleCardSelection(this);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Selection is persistent and is owned by CardManager. A normal release does not deselect.
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (GameplayInputGate.IsBlocked || eventData.button != PointerEventData.InputButton.Left || _isDragging) return;
        var manager = CardManager.Instance;
        if (manager == null || manager.dragCanvas == null || homeField == null)
            return;

        if (!manager.PrepareDragSelection(this))
            return;
        _originalParent = transform.parent;
        _originalPosition = transform.position;
        index = transform.GetSiblingIndex();
        _dragHomeField = homeField;
        _dragHomeField.BeginVisualDrag(this);
        _isDragging = true;
        manager.BeginCardDrag(this);

        var dragRect = manager.dragCanvas.transform as RectTransform;
        if (dragRect != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                dragRect, eventData.position, eventData.pressEventCamera, out var pointerWorld))
            _pointerOffset = transform.position - pointerWorld;
        else
            _pointerOffset = Vector3.zero;

        transform.SetParent(manager.dragCanvas.transform, true);
        transform.SetAsLastSibling();

        if (canvasGroup) canvasGroup.blocksRaycasts = false;
        RefreshState();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        var dragRect = transform.parent as RectTransform;
        if (dragRect != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                dragRect, eventData.position, eventData.pressEventCamera, out var pointerWorld))
            transform.position = pointerWorld + _pointerOffset;
        else
            transform.position = eventData.position;

        _dragHomeField?.UpdateVisualDrag(this, eventData.position, eventData.pressEventCamera);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        var target = FindDropTarget(eventData);
        bool committed = target != null && target.TryCommit(this);

        if (committed)
        {
            _dragHomeField?.RemoveVisualDragSlot(this);
        }
        else if (_dragHomeField != null)
        {
            bool commitReorder = _dragHomeField.ContainsScreenPoint(eventData.position, eventData.pressEventCamera);
            _dragHomeField.EndVisualDrag(this, commitReorder);
            if (!commitReorder)
                transform.position = _originalPosition;
        }
        else
        {
            RestoreOriginalTransform();
        }

        FinishDrag();
    }

    public void CancelActiveDrag()
    {
        if (!_isDragging) return;
        if (_dragHomeField != null)
        {
            _dragHomeField.EndVisualDrag(this, false);
            transform.position = _originalPosition;
        }
        else
        {
            RestoreOriginalTransform();
        }
        FinishDrag();
    }

    public void ForceToIdle()
    {
        CancelActiveDrag();
        _selectionOrder = 0;
        _isSelected = false;
        _isPointerOver = false;
        RefreshState();
    }

    CardActionDropTarget FindDropTarget(PointerEventData eventData)
    {
        if (EventSystem.current == null) return null;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        foreach (var result in results)
        {
            var behaviours = result.gameObject.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is CardActionDropTarget target)
                    return target;
            }
        }
        return null;
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
        CardManager.Instance?.EndCardDrag(this);
        _originalParent = null;
        _dragHomeField = null;
        _pointerOffset = Vector3.zero;
        RefreshState();
    }
}
