using UnityEngine;
using UnityEngine.UI;

public class Field : MonoBehaviour
{
    [Header("References")]
    public RectTransform cardsHolder;

    public int CardCount => cardsHolder != null ? cardsHolder.childCount : 0;

    public void ReturnCardToStart(CardView card)
    {
        card.transform.SetParent(cardsHolder);
        card.transform.SetSiblingIndex(card.index);
    }

    public void SetNewCard(CardView card, CardView left, CardView right)
    {
        RepositionCard(card, left, right);
        card.homeField = this;
    }

    public void RepositionCard(CardView card, CardView left, CardView right)
    {
        card.transform.SetParent(cardsHolder);

        int targetIndex = 0;
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
            targetIndex = cardsHolder.childCount;
        }

        targetIndex = Mathf.Clamp(targetIndex, 0, cardsHolder.childCount);
        card.transform.SetSiblingIndex(targetIndex);
        card.index = targetIndex;
    }

    public void AddCard(CardView card, CardData data)
    {
        card.transform.SetParent(cardsHolder);
        card.Setup(data);
        card.homeField = this;
    }

    public void ClearCards()
    {
        for (int i = cardsHolder.childCount - 1; i >= 0; i--)
            Destroy(cardsHolder.GetChild(i).gameObject);
    }
}
