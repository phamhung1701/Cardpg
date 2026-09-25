using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RunEventUI : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text titleLabel;
    public TMP_Text descriptionLabel;
    public Transform choicesContainer;
    public GameObject choiceButtonPrefab;
    public EnhancementTargetUI enhancementTargetUI;

    void OnEnable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowEvent += Show;
            RunManager.Instance.OnHideEvent += Hide;
            RunManager.Instance.OnShowUpgrade += ShowUpgrade;
            RunManager.Instance.OnHideUpgrade += Hide;
        }
        Hide();
    }

    void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowEvent -= Show;
            RunManager.Instance.OnHideEvent -= Hide;
            RunManager.Instance.OnShowUpgrade -= ShowUpgrade;
            RunManager.Instance.OnHideUpgrade -= Hide;
        }
    }

    void Show(RunEventDefinition definition)
    {
        if (definition == null || panel == null || choicesContainer == null || choiceButtonPrefab == null)
            return;

        if (titleLabel) titleLabel.text = definition.title;
        if (descriptionLabel) descriptionLabel.text = definition.description;

        ClearChoices();

        for (int i = 0; i < definition.choices.Length; i++)
        {
            int choiceIndex = i;
            var choice = definition.choices[i];
            var buttonObject = Instantiate(choiceButtonPrefab, choicesContainer);
            buttonObject.name = $"EventChoice_{choiceIndex + 1}";
            string unavailableReason = RunManager.Instance.GetEventOptionUnavailableReason(choiceIndex);
            bool available = string.IsNullOrEmpty(unavailableReason);
            var label = buttonObject.GetComponentInChildren<TMP_Text>();
            if (label)
            {
                string status = available ? string.Empty : $"\n<color=#DFA0A0>{unavailableReason}</color>";
                label.text = $"<b>{choice.label}</b>\n{choice.description}{status}";
            }

            var button = buttonObject.GetComponent<Button>();
            if (button)
            {
                button.interactable = available;
                button.onClick.AddListener(() => RunManager.Instance.ChooseEventOption(choiceIndex));
            }
        }

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
    }

    void ShowUpgrade()
    {
        if (panel == null || choicesContainer == null || choiceButtonPrefab == null || RunManager.Instance == null)
            return;

        if (titleLabel) titleLabel.text = "CARD ENHANCEMENT";
        if (descriptionLabel) descriptionLabel.text = "Choose one permanent enhancement for this run.";
        ClearChoices();

        var offers = RunManager.Instance.GetCurrentUpgradeOffers();
        for (int i = 0; i < offers.Count; i++)
        {
            int choiceIndex = i;
            var offer = offers[i];
            var buttonObject = Instantiate(choiceButtonPrefab, choicesContainer);
            buttonObject.name = $"EnhancementChoice_{choiceIndex + 1}";
            var label = buttonObject.GetComponentInChildren<TMP_Text>();
            if (label)
                label.text = $"<b>{offer.Icon} {offer.DisplayName}</b>\n{offer.Description(CardManager.Instance)}";

            var button = buttonObject.GetComponent<Button>();
            if (button)
                button.onClick.AddListener(() => { if (enhancementTargetUI) enhancementTargetUI.OpenUpgrade(choiceIndex); });
        }

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
    }

    void ClearChoices()
    {
        if (choicesContainer == null) return;
        foreach (Transform child in choicesContainer)
            Destroy(child.gameObject);
    }

    void Hide()
    {
        if (panel) panel.SetActive(false);
    }
}
