using UnityEngine;
using UnityEngine.UI;

public class Field : MonoBehaviour
{
    [Header("References")]
    public RectTransform cardsHolder;

    GameObject _dragPlaceholder;
    CardView _draggedCard;
    int _dragOriginalIndex;

    public int CardCount
    {
        get
        {
            if (cardsHolder == null) return 0;

            int count = 0;
            for (int i = 0; i < cardsHolder.childCount; i++)
            {
                if (cardsHolder.GetChild(i).TryGetComponent<CardView>(out _))
                    count++;
            }
            return count;
        }
    }

    public GameObject BeginVisualDrag(CardView card)
    {
        if (!CanReorder(card) || _draggedCard != null) return null;

        _draggedCard = card;
        _dragOriginalIndex = card.transform.GetSiblingIndex();
        card.index = _dragOriginalIndex;

        _dragPlaceholder = new GameObject($"{card.name} Drag Slot", typeof(RectTransform), typeof(LayoutElement));
        _dragPlaceholder.transform.SetParent(cardsHolder, false);
        _dragPlaceholder.transform.SetSiblingIndex(_dragOriginalIndex);

        var sourceLayout = card.GetComponent<LayoutElement>();
        var slotLayout = _dragPlaceholder.GetComponent<LayoutElement>();
        var sourceRect = card.transform as RectTransform;
        var slotRect = _dragPlaceholder.transform as RectTransform;
        if (sourceRect != null && slotRect != null)
        {
            slotRect.anchorMin = sourceRect.anchorMin;
            slotRect.anchorMax = sourceRect.anchorMax;
            slotRect.pivot = sourceRect.pivot;
            slotRect.sizeDelta = sourceRect.rect.size;
        }
        slotLayout.minWidth = sourceLayout != null && sourceLayout.minWidth >= 0 ? sourceLayout.minWidth : sourceRect.rect.width;
        slotLayout.minHeight = sourceLayout != null && sourceLayout.minHeight >= 0 ? sourceLayout.minHeight : sourceRect.rect.height;
        slotLayout.preferredWidth = sourceLayout != null && sourceLayout.preferredWidth >= 0 ? sourceLayout.preferredWidth : sourceRect.rect.width;
        slotLayout.preferredHeight = sourceLayout != null && sourceLayout.preferredHeight >= 0 ? sourceLayout.preferredHeight : sourceRect.rect.height;
        slotLayout.flexibleWidth = sourceLayout != null ? sourceLayout.flexibleWidth : -1;
        slotLayout.flexibleHeight = sourceLayout != null ? sourceLayout.flexibleHeight : -1;
        slotLayout.layoutPriority = sourceLayout != null ? sourceLayout.layoutPriority : 1;

        LayoutRebuilder.ForceRebuildLayoutImmediate(cardsHolder);
        return _dragPlaceholder;
    }

    public void UpdateVisualDrag(CardView card, Vector2 screenPosition, Camera eventCamera)
    {
        if (card != _draggedCard || _dragPlaceholder == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(cardsHolder, screenPosition, eventCamera))
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(cardsHolder);
        const float crossingThreshold = 10f;
        bool moved;
        do
        {
            moved = false;
            int slotIndex = _dragPlaceholder.transform.GetSiblingIndex();
            var left = FindCardAtOrBefore(slotIndex - 1, -1);
            if (left != null && screenPosition.x < GetScreenCenterX(left.transform as RectTransform, eventCamera) - crossingThreshold)
            {
                _dragPlaceholder.transform.SetSiblingIndex(left.transform.GetSiblingIndex());
                moved = true;
                continue;
            }

            slotIndex = _dragPlaceholder.transform.GetSiblingIndex();
            var right = FindCardAtOrBefore(slotIndex + 1, 1);
            if (right != null && screenPosition.x > GetScreenCenterX(right.transform as RectTransform, eventCamera) + crossingThreshold)
            {
                _dragPlaceholder.transform.SetSiblingIndex(right.transform.GetSiblingIndex());
                moved = true;
            }
        }
        while (moved);

        LayoutRebuilder.ForceRebuildLayoutImmediate(cardsHolder);
    }

    public bool ContainsScreenPoint(Vector2 screenPosition, Camera eventCamera)
    {
        return cardsHolder != null && RectTransformUtility.RectangleContainsScreenPoint(cardsHolder, screenPosition, eventCamera);
    }

    public void EndVisualDrag(CardView card, bool commitReorder)
    {
        if (card != _draggedCard) return;

        int targetIndex = commitReorder && _dragPlaceholder != null
            ? _dragPlaceholder.transform.GetSiblingIndex()
            : _dragOriginalIndex;

        if (card != null && card.homeField == this && cardsHolder != null)
        {
            card.transform.SetParent(cardsHolder, true);
            int maxIndex = Mathf.Max(0, cardsHolder.childCount - 1);
            card.transform.SetSiblingIndex(Mathf.Clamp(targetIndex, 0, maxIndex));
            card.index = card.transform.GetSiblingIndex();
        }

        DestroyPlaceholder();
        _draggedCard = null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardsHolder);
    }

    public void RemoveVisualDragSlot(CardView card)
    {
        if (card != _draggedCard) return;
        DestroyPlaceholder();
        _draggedCard = null;
        if (cardsHolder != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardsHolder);
    }

    public void ReturnCardToStart(CardView card)
    {
        if (!CanReorder(card)) return;

        card.transform.SetParent(cardsHolder, true);
        card.transform.SetSiblingIndex(Mathf.Clamp(card.index, 0, cardsHolder.childCount - 1));
    }

    public void SetNewCard(CardView card, CardView left, CardView right)
    {
        // Cross-field ownership transfers are not supported. New cards enter through AddCard.
        if (!CanReorder(card)) return;
        RepositionCard(card, left, right);
    }

    public void RepositionCard(CardView card, CardView left, CardView right)
    {
        if (!CanReorder(card)) return;

        card.transform.SetParent(cardsHolder, true);

        if (!IsCardInThisField(left)) left = null;
        if (!IsCardInThisField(right)) right = null;

        int targetIndex;
        if (left != null && right != null)
        {
            targetIndex = (left.transform.GetSiblingIndex() + right.transform.GetSiblingIndex()) / 2 + 1;
        }
        else if (left != null)
        {
            targetIndex = left.transform.GetSiblingIndex() + 1;
        }
        else if (right != null)
        {
            targetIndex = right.transform.GetSiblingIndex();
        }
        else
        {
            targetIndex = cardsHolder.childCount - 1;
        }

        targetIndex = Mathf.Clamp(targetIndex, 0, cardsHolder.childCount - 1);
        card.transform.SetSiblingIndex(targetIndex);
        card.index = card.transform.GetSiblingIndex();
    }

    public void AddCard(CardView card, CardInstance data)
    {
        if (card == null || cardsHolder == null) return;

        card.transform.SetParent(cardsHolder, false);
        card.transform.SetAsLastSibling();
        card.homeField = this;
        card.index = card.transform.GetSiblingIndex();
        card.Setup(data);
    }

    public void ClearCards()
    {
        if (cardsHolder == null) return;

        for (int i = cardsHolder.childCount - 1; i >= 0; i--)
        {
            var child = cardsHolder.GetChild(i);
            if (!child.TryGetComponent<CardView>(out var card)) continue;

            card.homeField = null;
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    void DestroyPlaceholder()
    {
        if (_dragPlaceholder == null) return;
        var placeholder = _dragPlaceholder;
        _dragPlaceholder = null;
        placeholder.transform.SetParent(null, false);
        placeholder.SetActive(false);
        if (Application.isPlaying)
            Destroy(placeholder);
        else
            DestroyImmediate(placeholder);
    }

    CardView FindCardAtOrBefore(int startIndex, int direction)
    {
        if (cardsHolder == null) return null;
        for (int i = startIndex; i >= 0 && i < cardsHolder.childCount; i += direction)
        {
            var child = cardsHolder.GetChild(i);
            if (child == _dragPlaceholder?.transform) continue;
            if (child.TryGetComponent<CardView>(out var card) && card != _draggedCard)
                return card;
        }
        return null;
    }

    static float GetScreenCenterX(RectTransform rect, Camera eventCamera)
    {
        if (rect == null) return 0f;
        return RectTransformUtility.WorldToScreenPoint(eventCamera, rect.TransformPoint(rect.rect.center)).x;
    }

    bool CanReorder(CardView card)
    {
        return card != null && cardsHolder != null && card.homeField == this;
    }

    bool IsCardInThisField(CardView card)
    {
        return card != null && card.homeField == this && card.transform.parent == cardsHolder;
    }
}
