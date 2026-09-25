using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Phase7EShopSelectionTests
{
    readonly List<Object> _assets = new();
    readonly List<GameObject> _objects = new();
    CardManager _cards;
    RunManager _run;
    ShopUI _ui;

    [SetUp]
    public void SetUp()
    {
        var artifactA = CreateArtifact("first", 10);
        var artifactB = CreateArtifact("second", 12);
        var enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
        enhancement.id = "sharp";
        enhancement.displayName = "Sharpened";
        enhancement.description = "Attack bonus";
        enhancement.price = 8;
        _assets.Add(enhancement);

        _cards = Make<CardManager>("Cards");
        _cards.Configure(null, null, null, new[] { artifactA, artifactB }, new[] { enhancement });
        var combat = Make<CombatManager>("Combat");
        combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Run");
        _run.thiefType = CreateEnemy("Thief");
        _run.goblinType = CreateEnemy("Goblin");
        _run.knightType = CreateEnemy("Knight");

        var uiObject = new GameObject("Shop UI");
        _objects.Add(uiObject);
        uiObject.SetActive(false);
        _ui = uiObject.AddComponent<ShopUI>();
        _ui.panel = new GameObject("Panel");
        _objects.Add(_ui.panel);
        _ui.itemsContainer = new GameObject("Offers").transform;
        _objects.Add(_ui.itemsContainer.gameObject);
        var template = new GameObject("Offer template", typeof(RectTransform), typeof(Image), typeof(Button));
        _objects.Add(template);
        var templateLabel = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        templateLabel.transform.SetParent(template.transform);
        _ui.itemButtonPrefab = template;
        _ui.buyButton = Make<Button>("Buy");
        _ui.continueButton = Make<Button>("Continue");
        _ui.selectedOfferText = Make<TextMeshProUGUI>("Details");
        _ui.goldText = Make<TextMeshProUGUI>("Gold");
        uiObject.SetActive(true);
        InvokeUi("OnEnable"); // MonoBehaviour lifecycle is not dispatched for plain EditMode fixtures.
        _run.StartRunWithSeed("7E-SLICE-1");
        OpenShop();
    }

    [TearDown]
    public void TearDown()
    {
        if (_ui) InvokeUi("OnDisable");
        foreach (var obj in _objects)
            if (obj) Object.DestroyImmediate(obj);
        _objects.Clear();
        foreach (var asset in _assets)
            if (asset) Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    [Test]
    public void SelectAndSwitch_InspectWithoutSpendingOrGranting()
    {
        _cards.AddGold(50);
        var offers = _run.GetCurrentShopOffers();
        var first = offers[0];
        var second = offers[1];
        Click(first);
        Assert.That(_ui.SelectedOfferId, Is.EqualTo(first.StableId));
        Assert.That(_ui.selectedOfferText.text, Does.Contain(first.DisplayName));
        Assert.That(_ui.selectedOfferText.text, Does.Contain($"{first.price}g"));
        Assert.That(ButtonFor(first).GetComponentInChildren<TMP_Text>().text, Does.StartWith("SELECTED"));
        Click(second);
        Assert.That(_ui.SelectedOfferId, Is.EqualTo(second.StableId));
        Assert.That(ButtonFor(first).GetComponentInChildren<TMP_Text>().text, Does.Not.StartWith("SELECTED"));
        Assert.That(_cards.gold, Is.EqualTo(50));
        Assert.That(_cards.ownedArtifacts, Is.Empty);
        Assert.That(_cards.ownedCards.All(card => card.Enhancement == null), Is.True);
    }

    [Test]
    public void Buy_ChargesCachedPriceOnlyAtConfirmation_AndClearsSelection()
    {
        _cards.AddGold(50);
        var offer = ArtifactOffer();
        Click(offer);
        int price = offer.price;
        offer.artifact.price = price + 100;
        Assert.That(_cards.gold, Is.EqualTo(50));
        _ui.buyButton.onClick.Invoke();
        Assert.That(_cards.gold, Is.EqualTo(50 - price));
        Assert.That(_cards.HasArtifact(offer.artifact), Is.True);
        Assert.That(_ui.SelectedOfferId, Is.Null);
        Assert.That(_ui.buyButton.interactable, Is.False);
    }

    [Test]
    public void InsufficientGold_AndAlreadyOwnedArtifact_FailWithoutSpending()
    {
        var offer = ArtifactOffer();
        Click(offer);
        _ui.buyButton.onClick.Invoke();
        Assert.That(_cards.gold, Is.Zero);
        Assert.That(_cards.HasArtifact(offer.artifact), Is.False);
        Assert.That(_ui.selectedOfferText.text, Does.Contain("Need"));
        _cards.AddGold(50);
        Click(offer);
        _cards.relics.Add(offer.artifact.id); // Simulate ownership changing after selection, without a UI refresh.
        _ui.buyButton.onClick.Invoke();
        Assert.That(_cards.gold, Is.EqualTo(50));
        Assert.That(_cards.ownedArtifacts, Is.Empty);
        Assert.That(_ui.selectedOfferText.text, Does.Contain("Already owned"));
    }

    [Test]
    public void StaleOfferAndIneligibleEnhancement_AreRejectedWithoutSpending()
    {
        _cards.AddGold(50);
        var enhancement = _run.GetCurrentShopOffers().First(offer => offer.kind == ShopOfferKind.Enhancement);
        Click(enhancement);
        var chosenCard = _cards.ownedCards.First(card => card.Enhancement == null);
        Assert.That(_cards.ApplyEnhancement(chosenCard.Id, enhancement.enhancement), Is.True);
        Assert.That(_ui.SelectedOfferId, Is.Null, "Build refresh clears stale selection");
        Click(enhancement);
        _ui.buyButton.onClick.Invoke();
        Assert.That(_cards.gold, Is.EqualTo(50));
        Assert.That(_ui.enhancementTargetUI, Is.Null, "This fixture tests the Shop selection shell separately");
        Assert.That(_run.PurchaseShopEnhancement(enhancement.StableId, chosenCard.Id), Is.False);
        Assert.That(_run.GetEnhancementTargetUnavailableReason(chosenCard.Id), Does.Contain("already enhanced"));

        var artifact = ArtifactOffer();
        Click(artifact);
        _run.GetCurrentShopOffers();
        _run.OnShopDone();
        Assert.That(_ui.SelectedOfferId, Is.Null);
        Assert.That(_run.PurchaseShopOffer(artifact.StableId), Is.False);
        Assert.That(_cards.gold, Is.EqualTo(50));
    }

    [Test]
    public void RemovedCachedOffer_RejectsStaleSelectedBuy()
    {
        _cards.AddGold(50);
        var offer = ArtifactOffer();
        Click(offer);
        ((List<ShopOffer>)_run.GetCurrentShopOffers()).Remove(offer);
        _ui.buyButton.onClick.Invoke();
        Assert.That(_cards.gold, Is.EqualTo(50));
        Assert.That(_cards.ownedArtifacts, Is.Empty);
        Assert.That(_ui.SelectedOfferId, Is.Null);
        Assert.That(_ui.selectedOfferText.text, Does.Contain("unavailable"));
    }

    [Test]
    public void InvalidCachedPrice_RejectsBuyWithoutGrantingOrChangingGold()
    {
        _cards.AddGold(50);
        var offer = ArtifactOffer();
        Click(offer);
        offer.price = -5;
        _ui.buyButton.onClick.Invoke();
        Assert.That(_cards.gold, Is.EqualTo(50));
        Assert.That(_cards.ownedArtifacts, Is.Empty);
        Assert.That(_ui.selectedOfferText.text, Does.Contain("Invalid price"));
    }

    [Test]
    public void FullCapacity_OfferCanBeInspectedButBuyFailsWithoutSpendingOrRemovingIt()
    {
        _cards.AddGold(50);
        var offer = ArtifactOffer();
        for (int i = 0; i < CardManager.BASE_ARTIFACT_CAPACITY; i++)
            Assert.That(_cards.BuyArtifact(CreateArtifact($"other_{i}", 0), 0), Is.True);
        int gold = _cards.gold;
        string stableId = offer.StableId;
        int cachedPrice = offer.price;
        Click(offer);
        Assert.That(_ui.SelectedOfferId, Is.EqualTo(stableId));
        Assert.That(_ui.selectedOfferText.text, Does.Contain("Artifact Capacity Full"));
        Assert.That(ButtonFor(offer).GetComponentInChildren<TMP_Text>().text, Does.Contain("Artifact Capacity Full"));
        _ui.buyButton.onClick.Invoke();
        Assert.That(_ui.selectedOfferText.text, Does.Contain("Artifact Capacity Full"));
        Assert.That(_cards.gold, Is.EqualTo(gold));
        Assert.That(_cards.ownedArtifacts, Has.Count.EqualTo(5));
        Assert.That(_cards.HasArtifact(offer.artifact), Is.False);
        Assert.That(_run.GetCurrentShopOffers(), Does.Contain(offer));
        Assert.That(offer.StableId, Is.EqualTo(stableId));
        Assert.That(offer.price, Is.EqualTo(cachedPrice));
    }

    [Test]
    public void CloseAndRestart_ClearSelectionAndBlockStaleBuy()
    {
        _cards.AddGold(50);
        var offer = ArtifactOffer();
        Click(offer);
        _ui.continueButton.onClick.Invoke();
        Assert.That(_ui.SelectedOfferId, Is.Null);
        Assert.That(_ui.panel.activeSelf, Is.False);
        _ui.buyButton.onClick.Invoke();
        Assert.That(_cards.gold, Is.EqualTo(50));

        _run.RestartRun();
        Assert.That(_ui.SelectedOfferId, Is.Null);
        Assert.That(_cards.gold, Is.Zero);
        Assert.That(_cards.ownedArtifacts, Is.Empty);
    }

    void InvokeUi(string method) => typeof(ShopUI)
        .GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        .Invoke(_ui, null);

    void OpenShop()
    {
        var shop = _run.currentPath.First(node => node.kind == MapNodeType.Shop);
        shop.accessible = true;
        _run.OnPathChosen(shop.id);
        Assert.That(_ui.panel.activeSelf, Is.True);
        Assert.That(_ui.itemsContainer.childCount, Is.GreaterThan(0), "ShopUI must populate offers when the shop opens");
    }

    ShopOffer ArtifactOffer() => _run.GetCurrentShopOffers().First(offer => offer.kind == ShopOfferKind.Artifact);

    Button ButtonFor(ShopOffer offer) => _ui.itemsContainer.Cast<Transform>()
        .Last(child => child && child.name == $"ShopOffer_{offer.StableId.Replace(':', '_')}")
        .GetComponent<Button>();

    void Click(ShopOffer offer) => ButtonFor(offer).onClick.Invoke();

    T Make<T>(string name) where T : Component
    {
        var obj = new GameObject(name);
        _objects.Add(obj);
        return obj.AddComponent<T>();
    }

    RelicData CreateArtifact(string id, int price)
    {
        var artifact = ScriptableObject.CreateInstance<RelicData>();
        artifact.id = id;
        artifact.displayName = id;
        artifact.description = "Artifact effect";
        artifact.price = price;
        _assets.Add(artifact);
        return artifact;
    }

    EnemyTypeData CreateEnemy(string name)
    {
        var enemy = EnemyTypeData.Create(name, 10, 3, 3);
        enemy.name = name;
        _assets.Add(enemy);
        return enemy;
    }
}
