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
    public TMP_Text enhancementLabel;
    public TMP_Text stateLabel;
    public CanvasGroup canvasGroup;
    public RectTransform visualRoot;
    public Outline selectionOutline;
    public GameObject statsPlate;
    public GameObject enhancementPlate;
    public GameObject selectionBadge;

    [Header("Card Artwork")]
    [Tooltip("Sprites indexed by CardData.Suit enum order, then Ace through King.")]
    public Sprite[] cardSprites = new Sprite[52];

    [Header("Presentation Motion")]
    [Min(0f)] public float hoverRaise = 10f;
    [Min(0f)] public float selectedRaise = 18f;
    [Min(0f)] public float dragRaise = 6f;
    [Min(0.001f)] public float positionSmoothTime = 0.055f;
    [Min(0.001f)] public float layoutSmoothTime = 0.075f;
    [Min(0.001f)] public float scaleSmoothTime = 0.055f;

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

    Vector2 _layoutOffset;
    Vector2 _layoutVelocity;
    Vector2 _stateOffset;
    Vector2 _stateVelocity;
    float _visualScale = 1f;
    float _scaleVelocity;
    float _targetRaise;
    float _targetScale = 1f;

    public State CurrentState => _state;
    public bool IsSelected => _isSelected;

    static readonly Color _selectedColor = new(1f, 0.72f, 0.2f, 1f);
    static readonly Color _dragColor = new(0.35f, 0.72f, 1f, 1f);

    void OnEnable()
    {
        if (Application.isPlaying && CardManager.Instance != null)
            CardManager.Instance.OnBuildChanged += RefreshContent;
    }

    void OnDisable()
    {
        if (CardManager.TryGetInstance(out var manager))
            manager.OnBuildChanged -= RefreshContent;
    }

    public void Setup(CardInstance cardData)
    {
        data = cardData;
        RefreshContent();
        RefreshState();
    }

    public void RefreshContent()
    {
        Sprite sprite = ResolveCardSprite(data);
        if (face)
        {
            face.sprite = sprite;
            face.preserveAspect = true;
            face.color = Color.white;
        }

        bool hasData = data != null;
        bool hasEnhancement = hasData && data.Enhancement != null;
        if (statsPlate) statsPlate.SetActive(hasData);
        if (enhancementPlate) enhancementPlate.SetActive(hasEnhancement);

        if (nameLabel)
        {
            if (!hasData)
            {
                nameLabel.text = string.Empty;
            }
            else
            {
                var combat = CombatManager.Instance;
                int attack = combat != null ? combat.CalculateCardAttackDamage(data) : data.AttackValue;
                int defense = combat != null ? combat.CalculateCardDefense(data) : data.DefenseValue;
                string identityFallback = sprite == null
                    ? $"{data.Definition.RankLabel}{data.SuitSymbol}  "
                    : string.Empty;
                nameLabel.text = $"{identityFallback}<b>ATK {attack}</b>   DEF {defense}";
            }
        }

        if (enhancementLabel)
            enhancementLabel.text = hasEnhancement
                ? $"{data.Enhancement.icon}  {data.Enhancement.displayName}"
                : string.Empty;
    }

    public Sprite ResolveCardSprite(CardInstance card) => ResolveCardSprite(card?.Definition);

    public Sprite ResolveCardSprite(CardData card)
    {
        if (card == null || cardSprites == null) return null;
        int index = (int)card.suit * 13 + (int)card.rank;
        return index >= 0 && index < cardSprites.Length ? cardSprites[index] : null;
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
        _targetRaise = state switch
        {
            State.Hover => hoverRaise,
            State.Click => selectedRaise,
            State.Drag => 0f,
            _ => 0f
        };
        _targetScale = state switch
        {
            State.Hover => 1.035f,
            State.Click => 1.055f,
            State.Drag => 1.065f,
            _ => 1f
        };

        if (face) face.color = state == State.Idle ? new Color(0.97f, 0.97f, 0.97f, 1f) : Color.white;
        if (selectionOutline)
        {
            selectionOutline.enabled = state == State.Click || state == State.Drag;
            selectionOutline.effectColor = state == State.Drag ? _dragColor : _selectedColor;
        }
        if (selectionBadge) selectionBadge.SetActive(state == State.Click);
        if (stateLabel) stateLabel.text = state == State.Click ? _selectionOrder.ToString() : string.Empty;
    }

    void LateUpdate()
    {
        if (visualRoot == null) return;
        float deltaTime = Time.unscaledDeltaTime;
        _layoutOffset = Vector2.SmoothDamp(
            _layoutOffset, Vector2.zero, ref _layoutVelocity,
            layoutSmoothTime, Mathf.Infinity, deltaTime);
        _stateOffset = Vector2.SmoothDamp(
            _stateOffset, new Vector2(0f, _targetRaise), ref _stateVelocity,
            positionSmoothTime, Mathf.Infinity, deltaTime);
        _visualScale = Mathf.SmoothDamp(
            _visualScale, _targetScale, ref _scaleVelocity,
            scaleSmoothTime, Mathf.Infinity, deltaTime);
        visualRoot.anchoredPosition = _layoutOffset + _stateOffset;
        visualRoot.localScale = Vector3.one * _visualScale;
    }

    public void ApplyLayoutWorldOffset(Vector3 worldOffset)
    {
        Vector3 localOffset = transform.InverseTransformVector(worldOffset);
        _layoutOffset += new Vector2(localOffset.x, localOffset.y);
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

        transform.SetParent(manager.dragCanvas.transform, true);
        transform.SetAsLastSibling();
        _stateOffset = Vector2.zero;
        _stateVelocity = Vector2.zero;
        MoveToPointer(eventData);

        if (canvasGroup) canvasGroup.blocksRaycasts = false;
        RefreshState();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        MoveToPointer(eventData);
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
        _layoutOffset = Vector2.zero;
        _layoutVelocity = Vector2.zero;
        _stateOffset = Vector2.zero;
        _stateVelocity = Vector2.zero;
        _visualScale = 1f;
        _scaleVelocity = 0f;
        RefreshState();
    }

    void MoveToPointer(PointerEventData eventData)
    {
        var dragRect = transform.parent as RectTransform;
        if (dragRect != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                dragRect, eventData.position, eventData.pressEventCamera, out var pointerWorld))
            transform.position = pointerWorld;
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
        RefreshState();
    }
}
