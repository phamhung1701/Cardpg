using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

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
    public CardData data;
    public Field homeField;
    public int index;

    State _state = State.Idle;
    Vector3 _originalPosition;
    Transform _originalParent;

    public State CurrentState => _state;

    static readonly Color _idleColor = new Color(0.85f, 0.92f, 0.85f);
    static readonly Color _hoverColor = new Color(0.95f, 0.95f, 1f);
    static readonly Color _clickColor = new Color(1f, 0.85f, 0.6f);
    static readonly Color _dragColor = new Color(0.7f, 0.85f, 1f);

    public void Setup(CardData cardData)
    {
        data = cardData;
        if (nameLabel) nameLabel.text = data.DisplayName;
        if (face) face.color = _idleColor;
    }

    void ChangeState(State next)
    {
        if (_state == next) return;
        ExitState(_state);
        _state = next;
        EnterState(next);
    }

    void EnterState(State s)
    {
        switch (s)
        {
            case State.Idle:
                if (face) face.color = _idleColor;
                if (stateLabel) stateLabel.text = "";
                break;
            case State.Hover:
                if (face) face.color = _hoverColor;
                if (stateLabel) stateLabel.text = "Hover";
                break;
            case State.Click:
                if (face) face.color = _clickColor;
                if (stateLabel) stateLabel.text = "Selected";
                CardManager.Instance.SelectCard(this);
                break;
            case State.Drag:
                if (face) face.color = _dragColor;
                if (stateLabel) stateLabel.text = "Drag";
                index = transform.GetSiblingIndex();
                _originalPosition = transform.position;
                _originalParent = transform.parent;
                var dragCanvas = CardManager.Instance.dragCanvas;
                if (dragCanvas != null)
                {
                    transform.SetParent(dragCanvas.transform);
                    transform.SetAsLastSibling();
                }
                if (canvasGroup) canvasGroup.blocksRaycasts = false;
                break;
            case State.Release:
                break;
        }
    }

    void ExitState(State s)
    {
        switch (s)
        {
            case State.Drag:
                if (canvasGroup) canvasGroup.blocksRaycasts = true;
                break;
        }
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (_state == State.Idle) ChangeState(State.Hover);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (_state == State.Hover) ChangeState(State.Idle);
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        if (_state == State.Hover) ChangeState(State.Click);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        if (_state == State.Click)
        {
            CardManager.Instance.DeselectCard();
            ChangeState(State.Hover);
        }
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        if (_state == State.Click) ChangeState(State.Drag);
    }

    public void OnDrag(PointerEventData e)
    {
        if (_state == State.Drag)
            transform.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (_state != State.Drag) return;

        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(e, results);

        Field targetField = null;
        CardView leftCard = null;
        CardView rightCard = null;

        foreach (var r in results)
        {
            if (r.gameObject == gameObject) continue;
            if (targetField == null && r.gameObject.TryGetComponent<Field>(out var f))
                targetField = f;
            if (r.gameObject.TryGetComponent<CardView>(out var cv) && cv != this && cv.homeField != null)
            {
                if (leftCard == null)
                    leftCard = cv;
                else
                    rightCard = cv;
            }
        }

        if (targetField == null)
            targetField = homeField;

        if (targetField != null)
        {
            if (targetField == homeField)
                targetField.RepositionCard(this, leftCard, rightCard);
            else
                targetField.SetNewCard(this, leftCard, rightCard);
        }
        else
        {
            transform.SetParent(_originalParent);
            transform.SetSiblingIndex(index);
            transform.position = _originalPosition;
        }

        CardManager.Instance.DeselectCard();
        ChangeState(State.Idle);
    }

    public void ForceToIdle()
    {
        if (_state != State.Idle) ChangeState(State.Idle);
    }
}
