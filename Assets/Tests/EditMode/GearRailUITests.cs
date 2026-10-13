using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class GearRailUITests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    GearRailUI _rail;
    RectTransform _slots;
    TMP_Text _capacity;
    TMP_Text _tooltip;
    TMP_Text _detail;
    GameObject _tooltipPanel;
    GameObject _detailPanel;
    Button _close;
    Button _mergeMode;
    Button _mergeConfirm;
    Button _mergeCancel;
    TMP_Text _mergeStatus;

    [SetUp]
    public void SetUp()
    {
        _cards = Create("Cards").AddComponent<CardManager>();
        var host = Create("Rail");
        host.SetActive(false);
        _rail = host.AddComponent<GearRailUI>();
        _slots = CreateUI("Slots").GetComponent<RectTransform>();
        _capacity = CreateUI("Capacity").AddComponent<TextMeshProUGUI>();
        _tooltipPanel = CreateUI("Tooltip");
        _tooltip = _tooltipPanel.AddComponent<TextMeshProUGUI>();
        _tooltipPanel.SetActive(false);
        _detailPanel = CreateUI("Detail");
        _detail = _detailPanel.AddComponent<TextMeshProUGUI>();
        _detailPanel.SetActive(false);
        _close = CreateUI("Close").AddComponent<Button>();
        _mergeMode = CreateUI("Merge Mode").AddComponent<Button>();
        _mergeConfirm = CreateUI("Merge Confirm").AddComponent<Button>();
        _mergeCancel = CreateUI("Merge Cancel").AddComponent<Button>();
        _mergeStatus = CreateUI("Merge Status").AddComponent<TextMeshProUGUI>();
        _rail.source = _cards;
        _rail.slotsRoot = _slots;
        _rail.capacityLabel = _capacity;
        _rail.tooltipPanel = _tooltipPanel;
        _rail.tooltipText = _tooltip;
        _rail.detailPanel = _detailPanel;
        _rail.detailText = _detail;
        _rail.detailCloseButton = _close;
        _rail.mergeModeButton = _mergeMode;
        _rail.mergeConfirmButton = _mergeConfirm;
        _rail.mergeCancelButton = _mergeCancel;
        _rail.mergeStatusText = _mergeStatus;
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
    public void EmptyInventory_ShowsFiveBlankSlotsAndHidesMergeControls()
    {
        Assert.That(_slots.childCount, Is.EqualTo(CardManager.BASE_GEAR_CAPACITY));
        for (int i = 0; i < _slots.childCount; i++)
        {
            var slot = _slots.GetChild(i).GetComponent<GearIconSlotUI>();
            Assert.That(_slots.GetChild(i).gameObject.activeSelf, Is.True);
            Assert.That(slot.Gear, Is.Null);
            Assert.That(string.IsNullOrEmpty(slot.GetComponentInChildren<TMP_Text>(true).text), Is.True);
        }
        Assert.That(_mergeMode.gameObject.activeSelf, Is.False);
        Assert.That(_mergeConfirm.gameObject.activeSelf, Is.False);
        Assert.That(_mergeCancel.gameObject.activeSelf, Is.False);
        Assert.That(_mergeStatus.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void RuntimeControlsAreCreatedInTheRailAndWireSelectionLifecycle()
    {
        InvokeRail("OnDisable");
        _rail.mergeModeButton = null;
        _rail.mergeConfirmButton = null;
        _rail.mergeCancelButton = null;
        _rail.mergeStatusText = null;

        var bar = CreateUI("Runtime Rail Root");
        bar.transform.SetParent(_rail.transform, false);
        var viewport = CreateUI("Runtime Viewport");
        viewport.transform.SetParent(bar.transform, false);
        var slots = CreateUI("Runtime Slots");
        slots.transform.SetParent(viewport.transform, false);
        _rail.slotsRoot = slots.GetComponent<RectTransform>();
        _cards.ownedGear.AddRange(new[]
        {
            Gear("merge-a", "Merge A", "A", ""),
            Gear("merge-b", "Merge B", "B", "")
        });
        InvokeRail("OnEnable");

        Assert.That(_rail.mergeModeButton, Is.Not.Null);
        Assert.That(_rail.mergeConfirmButton, Is.Not.Null);
        Assert.That(_rail.mergeCancelButton, Is.Not.Null);
        Assert.That(_rail.mergeStatusText, Is.Not.Null);
        Assert.That(_rail.mergeModeButton.transform.parent, Is.SameAs(bar.transform));
        foreach (var button in new[] { _rail.mergeModeButton, _rail.mergeConfirmButton, _rail.mergeCancelButton })
        {
            Assert.That(((RectTransform)button.transform).rect.height, Is.GreaterThanOrEqualTo(40f));
            Assert.That(button.GetComponentInChildren<TMP_Text>().fontSizeMin, Is.GreaterThanOrEqualTo(16f));
        }
        Assert.That(_rail.mergeStatusText.fontSizeMin, Is.GreaterThanOrEqualTo(16f));
        Assert.That(viewport.GetComponent<RectTransform>().anchoredPosition.y, Is.LessThan(0f));
        _rail.mergeModeButton.onClick.Invoke();
        Assert.That(_rail.mergeCancelButton.interactable, Is.True);
        Assert.That(_rail.mergeConfirmButton.interactable, Is.False);
        _rail.mergeCancelButton.onClick.Invoke();
        Assert.That(_rail.mergeCancelButton.interactable, Is.False);
    }

    [Test]
    public void ManualMergeRequiresTwoDistinctMatchingInstancesAndExplicitConfirmation()
    {
        var common = Gear("common", "Common", "C", "Common detail");
        common.canonicalId = "family-common";
        common.tier = 1;
        var next = Gear("rare", "Rare", "R", "Rare detail");
        next.canonicalId = "family-rare";
        next.tier = 2;
        next.upgradeFromId = "family-common";
        _cards.gearCatalog.AddRange(new[] { common, next });
        _cards.BuyGear(common, 0);
        _cards.BuyGear(common, 0);
        InvokeRail("Refresh");
        _mergeMode.onClick.Invoke();
        var first = _slots.GetChild(0).GetComponent<GearIconSlotUI>();
        var second = _slots.GetChild(1).GetComponent<GearIconSlotUI>();
        first.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(_mergeConfirm.interactable, Is.False);
        second.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(_mergeConfirm.interactable, Is.True);
        Assert.That(_mergeStatus.text, Does.Contain("Rare"));
        Assert.That(_cards.ownedGear.Count, Is.EqualTo(2));
        _mergeConfirm.onClick.Invoke();
        Assert.That(_cards.ownedGear, Is.EqualTo(new[] { next }));
    }

    [Test]
    public void ManualMergeDoesNotEnableForDifferentGearFamilies()
    {
        var firstGear = Gear("one", "One", "1", "One detail");
        firstGear.canonicalId = "family-one";
        var secondGear = Gear("two", "Two", "2", "Two detail");
        secondGear.canonicalId = "family-two";
        _cards.gearCatalog.AddRange(new[] { firstGear, secondGear });
        Assert.That(_cards.BuyGear(firstGear, 0), Is.True);
        Assert.That(_cards.BuyGear(secondGear, 0), Is.True);
        InvokeRail("Refresh");
        _mergeMode.onClick.Invoke();
        _slots.GetChild(0).GetComponent<GearIconSlotUI>().OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        _slots.GetChild(1).GetComponent<GearIconSlotUI>().OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(_mergeConfirm.interactable, Is.False);
        Assert.That(_cards.ownedGear.Count, Is.EqualTo(2));
    }

    [Test]
    public void Acquisition_ShowsOnlyIconUntilHoveredOrClicked()
    {
        var gear = Gear("relic-1", "Hidden Name", "★", "Detail body");
        Assert.That(_cards.BuyGear(gear, 0), Is.True);
        Assert.That(_capacity.text, Does.Contain("1/5"));
                Assert.That(_slots.childCount, Is.EqualTo(CardManager.BASE_GEAR_CAPACITY));
        Assert.That(Enumerable.Range(0, CardManager.BASE_GEAR_CAPACITY)
            .All(i => _slots.GetChild(i).gameObject.activeSelf), Is.True,
            "The rail keeps a stable five-slot inventory footprint.");
        Assert.That(Enumerable.Range(1, CardManager.BASE_GEAR_CAPACITY - 1)
            .Select(i => _slots.GetChild(i).GetComponent<GearIconSlotUI>().Gear),
            Is.All.Null, "Unowned capacity remains represented by quiet empty slots.");
        var slot = _slots.GetChild(0).GetComponent<GearIconSlotUI>();
        Assert.That(slot.Gear, Is.SameAs(gear));
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
    public void SelectingGearShowsAdjacentDetailsUpdatesSelectionAndEmptySlotsStayBlank()
    {
        var first = Gear("first-detail", "First Name", "A", "First description");
        var second = Gear("second-detail", "Second Name", "B", "Second description");
        Assert.That(_cards.BuyGear(first, 0), Is.True);
        Assert.That(_cards.BuyGear(second, 0), Is.True);
        InvokeRail("Refresh");
        var firstSlot = _slots.GetChild(0).GetComponent<GearIconSlotUI>();
        var secondSlot = _slots.GetChild(1).GetComponent<GearIconSlotUI>();
        firstSlot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(firstSlot.IsDetailSelected, Is.True);
        Assert.That(_detailPanel.activeSelf, Is.True);
        Assert.That(_detail.text, Does.Contain("First Name").And.Contain("First description"));
        var detailRect = _detailPanel.GetComponent<RectTransform>();
        Assert.That(detailRect.rect.width, Is.LessThanOrEqualTo(240f), "Artifact information remains a compact side card, not a modal.");
        Assert.That(detailRect.rect.height, Is.LessThanOrEqualTo(220f));

        secondSlot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(firstSlot.IsDetailSelected, Is.False);
        Assert.That(secondSlot.IsDetailSelected, Is.True);
        Assert.That(_detail.text, Does.Contain("Second Name").And.Contain("Second description"));
        string selectedDescription = _detail.text;
        _slots.GetChild(2).GetComponent<GearIconSlotUI>()
            .OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(_detail.text, Is.EqualTo(selectedDescription));
        Assert.That(_slots.GetChild(2).GetComponent<GearIconSlotUI>().Gear, Is.Null);

        secondSlot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(_detailPanel.activeSelf, Is.False);
        Assert.That(secondSlot.IsDetailSelected, Is.False);
    }

    [Test]
    public void EnemyTargetingClosesGearDetailsAndPreventsCompetingMergeSelection()
    {
        Assert.That(_cards.BuyGear(Gear("target-a", "Target A", "A", "Readable description"), 0), Is.True);
        Assert.That(_cards.BuyGear(Gear("target-b", "Target B", "B", "Other description"), 0), Is.True);
        var slot = _slots.GetChild(0).GetComponent<GearIconSlotUI>();
        slot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(_detailPanel.activeSelf, Is.True);
        Assert.That(_detail.fontSize, Is.GreaterThanOrEqualTo(18f));
        slot.OnPointerEnter(null);
        Assert.That(_tooltipPanel.activeSelf, Is.False, "Selected details must not be covered by a duplicate hover tooltip.");
        _cards.BeginEnemyTargeting(_ => { }, _ => true);
        Assert.That(_detailPanel.activeSelf, Is.False);
        Assert.That(slot.IsDetailSelected, Is.False);
        _mergeMode.onClick.Invoke();
        Assert.That(_mergeCancel.gameObject.activeSelf, Is.False);
        _cards.EndEnemyTargeting();
        _mergeMode.onClick.Invoke();
        Assert.That(_mergeCancel.gameObject.activeSelf, Is.True);
        _cards.BeginEnemyTargeting(_ => { }, _ => true);
        Assert.That(_mergeCancel.gameObject.activeSelf, Is.False);
        _cards.EndEnemyTargeting();
    }

    [Test]
    public void Reset_ClearsSlotsAndOpenDetail_ThenAcquisitionReusesSlot()
    {
        var first = Gear("first", "First", "A", "First detail");
        var second = Gear("second", "Second", "B", "Second detail");
        _cards.BuyGear(first, 0);
        var slot = _slots.GetChild(0).GetComponent<GearIconSlotUI>();
        slot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        _cards.Reset();
        Assert.That(_capacity.text, Does.Contain("0/5"));
        Assert.That(slot.gameObject.activeSelf, Is.True, "The same slot remains visible as an empty inventory slot.");
        Assert.That(slot.Gear, Is.Null);
        Assert.That(_detailPanel.activeSelf, Is.False);
        _cards.BuyGear(second, 0);
        Assert.That(_slots.childCount, Is.EqualTo(CardManager.BASE_GEAR_CAPACITY));
        Assert.That(slot.Gear, Is.SameAs(second));
        Assert.That(slot.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("B"));
    }

    void InvokeRail(string method) => typeof(GearRailUI)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
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

    GearData Gear(string id, string name, string icon, string description)
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.id = id;
        gear.displayName = name;
        gear.icon = icon;
        gear.description = description;
        _created.Add(gear);
        return gear;
    }
}
