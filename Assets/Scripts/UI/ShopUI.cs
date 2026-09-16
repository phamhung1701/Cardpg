using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopUI : MonoBehaviour
{
    [Header("References")]
    public GameObject panel;
    public Transform itemsContainer;
    public GameObject itemButtonPrefab;
    public Button continueButton;
    public TMP_Text goldText;

    void OnEnable()
    {
        if (CombatManager.Instance != null)
            CombatManager.Instance.OnStateChanged += HandleStateChanged;
        if (CardManager.Instance != null)
            CardManager.Instance.OnGoldChanged += HandleGoldChanged;
        if (continueButton) continueButton.onClick.AddListener(OnContinue);
    }

    void OnDisable()
    {
        if (CombatManager.Instance != null)
            CombatManager.Instance.OnStateChanged -= HandleStateChanged;
        if (CardManager.Instance != null)
            CardManager.Instance.OnGoldChanged -= HandleGoldChanged;
        if (continueButton) continueButton.onClick.RemoveListener(OnContinue);
    }

    void HandleStateChanged(GameState state)
    {
        if (state == GameState.GameWon)
        {
            Open();
        }
        else
        {
            Close();
        }
    }

    void HandleGoldChanged(int gold)
    {
        if (goldText) goldText.text = $"{gold}g";
        if (panel != null && panel.activeSelf) RefreshItems();
    }

    void Open()
    {
        if (panel) panel.SetActive(true);
        RefreshItems();
    }

    void Close()
    {
        if (panel) panel.SetActive(false);
    }

    void RefreshItems()
    {
        if (itemsContainer == null || itemButtonPrefab == null) return;

        foreach (Transform child in itemsContainer)
            Destroy(child.gameObject);

        var cm = CardManager.Instance;
        foreach (var relic in cm.relicCatalog)
        {
            bool owned = cm.HasRelic(relic.id);
            bool canAfford = cm.gold >= relic.price;

            var btn = Instantiate(itemButtonPrefab, itemsContainer);
            var text = btn.GetComponentInChildren<TMP_Text>();
            if (text) text.text = $"{relic.icon} {relic.displayName} - {relic.price}g\n{relic.description}";

            var button = btn.GetComponent<Button>();
            if (button)
            {
                button.interactable = !owned && canAfford;
                var r = relic;
                button.onClick.AddListener(() => { if (cm.BuyRelic(r)) RefreshItems(); });
            }
        }
    }

    void OnContinue()
    {
        Close();
        RunManager.Instance.OnShopDone();
    }
}
