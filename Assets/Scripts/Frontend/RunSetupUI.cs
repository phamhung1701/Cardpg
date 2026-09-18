using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RunSetupUI : MonoBehaviour
{
    public FrontendMenuNavigator navigator;
    public TMP_InputField seedInput;
    public TMP_Text seedPreviewLabel;
    public Button randomSeedButton;
    public Button startButton;
    public Button backButton;

    void OnEnable()
    {
        if (randomSeedButton) randomSeedButton.onClick.AddListener(RandomizeSeed);
        if (startButton) startButton.onClick.AddListener(StartRun);
        if (backButton) backButton.onClick.AddListener(Back);
        if (seedInput) seedInput.onValueChanged.AddListener(HandleSeedChanged);
    }

    void OnDisable()
    {
        if (randomSeedButton) randomSeedButton.onClick.RemoveListener(RandomizeSeed);
        if (startButton) startButton.onClick.RemoveListener(StartRun);
        if (backButton) backButton.onClick.RemoveListener(Back);
        if (seedInput) seedInput.onValueChanged.RemoveListener(HandleSeedChanged);
    }

    public void Prepare()
    {
        if (seedInput == null || string.IsNullOrWhiteSpace(seedInput.text)) RandomizeSeed();
        else RefreshPreview();
    }

    public void RandomizeSeed()
    {
        string seed = RunSeedUtility.GenerateSeed();
        if (seedInput) seedInput.text = seed;
        RefreshPreview();
    }

    public void StartRun()
    {
        string seed = RunRandomContext.NormalizeSeed(seedInput != null ? seedInput.text : string.Empty);
        if (seedInput) seedInput.text = seed;
        GameFlowService.Instance?.StartRun(seed);
    }

    public void Back() => navigator?.ShowHome();

    void HandleSeedChanged(string _) => RefreshPreview();

    void RefreshPreview()
    {
        if (seedPreviewLabel)
            seedPreviewLabel.text = $"RUN SEED  •  {RunRandomContext.NormalizeSeed(seedInput != null ? seedInput.text : string.Empty)}";
    }
}
