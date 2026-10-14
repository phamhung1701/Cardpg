using NUnit.Framework;
using UnityEngine;

public sealed class GameplayMenuCoordinatorGateTests
{
    GameObject _host;
    GameObject _panel;
    GameplayMenuCoordinator _coordinator;
    float _originalTimeScale;

    [SetUp]
    public void SetUp()
    {
        _originalTimeScale = Time.timeScale;
        GameplayInputGate.Clear();
        _host = new GameObject("Gameplay Menu Coordinator Gate Test");
        _coordinator = _host.AddComponent<GameplayMenuCoordinator>();
        _panel = new GameObject("Modal Panel");
        _panel.SetActive(false);
    }

    [TearDown]
    public void TearDown()
    {
        if (SavePromptUI.IsOpen)
            Object.FindFirstObjectByType<SavePromptUI>()?.Cancel();
        GameplayInputGate.Clear();
        Time.timeScale = _originalTimeScale <= 0f ? 1f : _originalTimeScale;
        if (_panel != null) Object.DestroyImmediate(_panel);
        if (_host != null) Object.DestroyImmediate(_host);
    }

    [Test]
    public void ModalPushAndPop_PreserveExternalConsumableRewardBlock()
    {
        GameplayInputGate.Set(GameplayInputBlockReason.ConsumableReward, true);
        var owner = new object();

        Assert.That(_coordinator.Push(owner, _panel, null), Is.True);
        Assert.That(GameplayInputGate.Reasons.HasFlag(GameplayInputBlockReason.ConsumableReward), Is.True);
        Assert.That(GameplayInputGate.Reasons.HasFlag(GameplayInputBlockReason.FrontendMenu), Is.True);

        Assert.That(_coordinator.Pop(owner), Is.True);
        Assert.That(GameplayInputGate.Reasons, Is.EqualTo(GameplayInputBlockReason.ConsumableReward));
    }

    [Test]
    public void SameReasonFromIndependentOwners_RemainsBlockedUntilBothOwnersReleaseIt()
    {
        var owner = new object();
        Assert.That(_coordinator.Push(owner, _panel, null), Is.True);
        GameplayInputGate.Set(GameplayInputBlockReason.FrontendMenu, true);

        Assert.That(_coordinator.Pop(owner), Is.True);
        Assert.That(GameplayInputGate.IsBlocked, Is.True,
            "Removing the coordinator's FrontendMenu reason must not clear the external owner's same reason.");
        Assert.That(GameplayInputGate.Reasons, Is.EqualTo(GameplayInputBlockReason.FrontendMenu));

        GameplayInputGate.Set(GameplayInputBlockReason.FrontendMenu, false);
        Assert.That(GameplayInputGate.IsBlocked, Is.False);
    }

    [Test]
    public void SavePromptCancellation_DoesNotReleaseCoordinatorOwnedFrontendMenuReason()
    {
        var owner = new object();
        Assert.That(_coordinator.Push(owner, _panel, null), Is.True);

        SavePromptUI.ShowError("Test prompt");
        var prompt = Object.FindFirstObjectByType<SavePromptUI>();
        Assert.That(prompt, Is.Not.Null);
        Assert.That(GameplayInputGate.Reasons.HasFlag(GameplayInputBlockReason.FrontendMenu), Is.True);

        prompt.Cancel();
        Assert.That(GameplayInputGate.IsBlocked, Is.True,
            "Closing the prompt must release only the prompt's ownership, not the coordinator's same reason.");
        Assert.That(_coordinator.Pop(owner), Is.True);
        Assert.That(GameplayInputGate.IsBlocked, Is.False);
    }

    [Test]
    public void Clear_RemovesLegacyAndOwnerScopedReasons()
    {
        GameplayInputGate.Set(GameplayInputBlockReason.ConsumableReward, true);
        GameplayInputGate.SetOwned(this, GameplayInputBlockReason.GearChoice, true);

        GameplayInputGate.Clear();

        Assert.That(GameplayInputGate.IsBlocked, Is.False);
        Assert.That(GameplayInputGate.Reasons, Is.EqualTo(GameplayInputBlockReason.None));
    }
}
