using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ArtifactRailUITests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    ArtifactRailUI _rail;
    RectTransform _slots;
    TMP_Text _capacity;
    TMP_Text _tooltip;
    TMP_Text _detail;
    GameObject _tooltipPanel;
    GameObject _detailPanel;
    Button _close;

    [SetUp]
    public void SetUp()
    {
        _cards = Create("Cards").AddComponent<CardManager>();
        var host = Create("Rail");
        host.SetActive(false);
        _rail = host.AddComponent<ArtifactRailUI>();
        _slots = CreateUI("Slots").GetComponent<RectTransform>();
        _capacity = CreateUI("Capacity").AddComponent<TextMeshProUGUI>();
        _tooltipPanel = CreateUI("Tooltip");
        _tooltip = _tooltipPanel.AddComponent<TextMeshProUGUI>();
        _tooltipPanel.SetActive(false);
        _detailPanel = CreateUI("Detail");
        _detail = _detailPanel.AddComponent<TextMeshProUGUI>();
        _detailPanel.SetActive(false);
        _close = CreateUI("Close").AddComponent<Button>();
        _rail.source = _cards;
        _rail.slotsRoot = _slots;
        _rail.capacityLabel = _capacity;
        _rail.tooltipPanel = _tooltipPanel;
        _rail.tooltipText = _tooltip;
        _rail.detailPanel = _detailPanel;
        _rail.detailText = _detail;
        _rail.detailCloseButton = _close;
        host.SetActive(true);
        InvokeRail("OnEnable"); // EditMode does not consistently run MonoBehaviour lifecycle callbacks.
        _rail.Refresh();
    }

    [TearDown]
    public void TearDown()
    {
        if (_rail) InvokeRail("OnDisable");
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i]) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void Acquisition_ShowsOnlyIconUntilHoveredOrClicked()
    {
        var artifact = Artifact("relic-1", "Hidden Name", "★", "Detail body");
        Assert.That(_cards.BuyArtifact(artifact, 0), Is.True);
        Assert.That(_capacity.text, Does.Contain("1/5"));
        Assert.That(_slots.childCount, Is.EqualTo(1));
        var slot = _slots.GetChild(0).GetComponent<ArtifactIconSlotUI>();
        Assert.That(slot.Artifact, Is.SameAs(artifact));
        Assert.That(slot.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("★"));
        Assert.That(_capacity.text, Does.Not.Contain("Hidden Name"));
        Assert.That(_tooltipPanel.activeSelf, Is.False);
        Assert.That(_detailPanel.activeSelf, Is.False);

        slot.OnPointerEnter(null);
        Assert.That(_tooltipPanel.activeSelf, Is.True);
        Assert.That(_tooltip.text, Does.Contain("Hidden Name"));
        slot.OnPointerExit(null);
        Assert.That(_tooltipPanel.activeSelf, Is.False);
        slot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(_detailPanel.activeSelf, Is.True);
        Assert.That(_detail.text, Does.Contain("Detail body"));
        _close.onClick.Invoke();
        Assert.That(_detailPanel.activeSelf, Is.False);
    }

    [Test]
    public void Reset_ClearsSlotsAndOpenDetail_ThenAcquisitionReusesSlot()
    {
        var first = Artifact("first", "First", "A", "First detail");
        var second = Artifact("second", "Second", "B", "Second detail");
        _cards.BuyArtifact(first, 0);
        var slot = _slots.GetChild(0).GetComponent<ArtifactIconSlotUI>();
        slot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        _cards.Reset();
        Assert.That(_capacity.text, Does.Contain("0/5"));
        Assert.That(slot.gameObject.activeSelf, Is.False);
        Assert.That(_detailPanel.activeSelf, Is.False);
        _cards.BuyArtifact(second, 0);
        Assert.That(_slots.childCount, Is.EqualTo(1));
        Assert.That(slot.Artifact, Is.SameAs(second));
        Assert.That(slot.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("B"));
    }

    void InvokeRail(string method) => typeof(ArtifactRailUI)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(_rail, null);

    GameObject Create(string name)
    {
        var go = new GameObject(name);
        _created.Add(go);
        return go;
    }

    GameObject CreateUI(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        _created.Add(go);
        return go;
    }

    RelicData Artifact(string id, string name, string icon, string description)
    {
        var artifact = ScriptableObject.CreateInstance<RelicData>();
        artifact.id = id;
        artifact.displayName = name;
        artifact.icon = icon;
        artifact.description = description;
        _created.Add(artifact);
        return artifact;
    }
}
