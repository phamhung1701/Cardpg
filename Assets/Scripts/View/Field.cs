using UnityEngine;

public class Field : MonoBehaviour
{
    [Header("References")]
    public RectTransform cardsHolder;

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

    bool CanReorder(CardView card)
    {
        return card != null && cardsHolder != null && card.homeField == this;
    }

    bool IsCardInThisField(CardView card)
    {
        return card != null && card.homeField == this && card.transform.parent == cardsHolder;
    }
}
