using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Phase7EEnhancementTargetTests
{
    readonly List<Object> _assets = new();
    readonly List<GameObject> _objects = new();
    CardManager _cards;
    RunManager _run;
    CardEnhancementData _enhancement;
    EnhancementTargetUI _picker;

    [SetUp]
    public void SetUp()
    {
        _enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
        _enhancement.id = "sharp";
        _enhancement.displayName = "Sharp";
        _enhancement.description = "Adds attack";
        _enhancement.price = 9;
        _assets.Add(_enhancement);
        _cards = Make<CardManager>("Cards");
        _cards.Configure(null, null, null, System.Array.Empty<RelicData>(), new[] { _enhancement });
        var combat = Make<CombatManager>("Combat");
        combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Run");
        _run.thiefType = Enemy("Thief");
        _run.goblinType = Enemy("Goblin");
        _run.knightType = Enemy("Knight");
        _run.StartRunWithSeed("7E-TARGET-CHOICE");
    }

    [TearDown]
    public void TearDown()
    {
        if (_picker) InvokePicker("OnDisable");
        foreach (var obj in _objects)
            if (obj) Object.DestroyImmediate(obj);
        foreach (var asset in _assets)
            if (asset) Object.DestroyImmediate(asset);
        _objects.Clear();
        _assets.Clear();
    }

    [Test]
    public void Shop_AppliesToChosenInstanceOnly_AndChargesCachedPriceExactlyOnce()
    {
        var offer = OpenShopOffer();
        _cards.AddGold(50);
        var chosen = _cards.ownedCards[10];
        var untouched = _cards.ownedCards[11];
        int price = offer.price;
        offer.enhancement.price += 200;
        Assert.That(_run.PurchaseShopOffer(offer.StableId), Is.False, "Implicit target cannot purchase");
        Assert.That(_cards.gold, Is.EqualTo(50));
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, chosen.Id), Is.True);
        Assert.That(_cards.gold, Is.EqualTo(50 - price));
        Assert.That(chosen.Enhancement, Is.SameAs(_enhancement));
        Assert.That(untouched.Enhancement, Is.Null);
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, chosen.Id), Is.False);
        Assert.That(_cards.gold, Is.EqualTo(50 - price));
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(40));
    }

    [Test]
    public void Shop_RejectsAlreadyEnhancedAndNonOwnedTargetsWithoutSpending_ThenAcceptsAnother()
    {
        var offer = OpenShopOffer();
        _cards.AddGold(40);
        var invalid = _cards.ownedCards[3];
        Assert.That(_cards.ApplyEnhancement(invalid.Id, _enhancement), Is.True);
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, invalid.Id), Is.False);
        Assert.That(_run.GetEnhancementTargetUnavailableReason(invalid.Id), Is.EqualTo("Target already enhanced"));
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, 99999), Is.False);
        Assert.That(_cards.gold, Is.EqualTo(40));
        Assert.That(_run.ActiveNode.kind, Is.EqualTo(MapNodeType.Shop));
        Assert.That(_run.GetCurrentShopOffers(), Does.Contain(offer));
        var chosen = _cards.ownedCards[6];
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, chosen.Id), Is.True);
        Assert.That(_cards.gold, Is.EqualTo(40 - offer.price));
        Assert.That(chosen.Enhancement, Is.SameAs(_enhancement));
    }

    [Test]
    public void RemovedOwnedCard_IsRejectedAtConfirmation_WithoutSpendingOrLosingOffer()
    {
        var offer = OpenShopOffer();
        _cards.AddGold(40);
        int removedId = _cards.ownedCards[4].Id;
        var collection = (CardCollection)typeof(CardManager)
            .GetField("_cards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(_cards);
        collection.Initialize(_cards.ownedCards.Where(card => card.Id != removedId).ToArray());
        Assert.That(_cards.FindOwnedCard(removedId), Is.Null);
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, removedId), Is.False);
        Assert.That(_cards.gold, Is.EqualTo(40));
        Assert.That(_run.GetCurrentShopOffers(), Does.Contain(offer));
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(39));
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, _cards.ownedCards[5].Id), Is.True);
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(39));
        Assert.That(_cards.gold, Is.EqualTo(40 - offer.price));
    }

    [Test]
    public void Shop_RejectsRemovedOfferInvalidPriceAndClosedNode()
    {
        var offer = OpenShopOffer();
        _cards.AddGold(40);
        int cardId = _cards.ownedCards[4].Id;
        offer.price = -1;
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, cardId), Is.False);
        offer.price = 9;
        ((List<ShopOffer>)_run.GetCurrentShopOffers()).Remove(offer);
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, cardId), Is.False);
        Assert.That(_cards.gold, Is.EqualTo(40));
        Assert.That(_cards.ownedCards[4].Enhancement, Is.Null);
        _run.OnShopDone();
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, cardId), Is.False);
    }

    [Test]
    public void Upgrade_AppliesToPlayerChosenCard_OnlyOnce_WithoutChargingGold()
    {
        var offer = OpenUpgradeOffer();
        var chosen = _cards.ownedCards[8];
        var other = _cards.ownedCards[9];
        _cards.AddGold(18);
        Assert.That(_run.ChooseUpgradeOffer(0), Is.False);
        Assert.That(_run.ChooseUpgradeOffer(0, chosen.Id), Is.True);
        Assert.That(chosen.Enhancement, Is.SameAs(offer.enhancement));
        Assert.That(other.Enhancement, Is.Null);
        Assert.That(_cards.gold, Is.EqualTo(18));
        Assert.That(_run.ActiveNode, Is.Null);
        Assert.That(_run.ChooseUpgradeOffer(0, other.Id), Is.False);
    }

    [Test]
    public void Upgrade_InvalidTargetLeavesNodeAndOfferIntact_ForRetry()
    {
        var offer = OpenUpgradeOffer();
        var invalid = _cards.ownedCards[2];
        _cards.ApplyEnhancement(invalid.Id, _enhancement);
        Assert.That(_run.ChooseUpgradeOffer(0, invalid.Id), Is.False);
        Assert.That(_run.ChooseUpgradeOffer(0, 9999), Is.False);
        Assert.That(_run.ActiveNode.kind, Is.EqualTo(MapNodeType.Upgrade));
        Assert.That(_run.GetCurrentUpgradeOffers(), Does.Contain(offer));
        Assert.That(_cards.ownedCards.Count, Is.EqualTo(40));
        Assert.That(_run.ChooseUpgradeOffer(0, _cards.ownedCards[7].Id), Is.True);
    }

    [Test]
    public void EqualSuitRankInstances_RemainIndependent_WhenOnlyOneIsChosen()
    {
        var offer = OpenShopOffer();
        _cards.AddGold(40);
        var original = _cards.ownedCards[0];
        var duplicate = new CardInstance(original.Definition, 9000);
        // Establish a second owned instance of the same definition through the model, not a card view.
        var collection = (CardCollection)typeof(CardManager)
            .GetField("_cards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(_cards);
        Assert.That(collection.AddOwnedToDeck(duplicate), Is.True);
        Assert.That(_run.PurchaseShopEnhancement(offer.StableId, duplicate.Id), Is.True);
        Assert.That(duplicate.Enhancement, Is.SameAs(_enhancement));
        Assert.That(original.Enhancement, Is.Null);
        Assert.That(original.Suit, Is.EqualTo(duplicate.Suit));
        Assert.That(original.Rank, Is.EqualTo(duplicate.Rank));
    }

    [Test]
    public void TargetChoice_DoesNotChangeOfferIdentityOrOtherSeededStreams()
    {
        var first = OpenShopOffer();
        var offerIds = string.Join(",", _run.GetCurrentShopOffers().Select(o => o.StableId));
        string mapSignature = string.Join(",", _run.GenerateMapForIndex(4).Select(n => $"{n.id}:{n.kind}:{n.contentId}"));
        _cards.AddGold(40);
        Assert.That(_run.PurchaseShopEnhancement(first.StableId, _cards.ownedCards[0].Id), Is.True);
        string afterChoice = string.Join(",", _run.GenerateMapForIndex(4).Select(n => $"{n.id}:{n.kind}:{n.contentId}"));
        Assert.That(afterChoice, Is.EqualTo(mapSignature));
        Assert.That(string.Join(",", _run.GetCurrentShopOffers().Select(o => o.StableId)), Is.EqualTo(offerIds));

        _run.RestartRun();
        for (int i = 0; i < 5; i++) _cards.ShuffleDeck();
        var repeated = OpenShopOffer();
        Assert.That(string.Join(",", _run.GetCurrentShopOffers().Select(o => o.StableId)), Is.EqualTo(offerIds));
        Assert.That(repeated.StableId, Does.StartWith("enhancement:"));
        Assert.That(repeated.slot, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void UpgradeOfferIdentity_AndUnrelatedMapRng_DoNotDependOnChosenTarget()
    {
        OpenUpgradeOffer();
        string ids = string.Join(",", _run.GetCurrentUpgradeOffers().Select(o => o.StableId));
        string map = string.Join(",", _run.GenerateMapForIndex(5).Select(n => $"{n.id}:{n.kind}:{n.contentId}"));
        Assert.That(_run.ChooseUpgradeOffer(0, _cards.ownedCards[13].Id), Is.True);
        Assert.That(string.Join(",", _run.GenerateMapForIndex(5).Select(n => $"{n.id}:{n.kind}:{n.contentId}")), Is.EqualTo(map));
        _run.RestartRun();
        for (int i = 0; i < 5; i++) _cards.ShuffleDeck();
        OpenUpgradeOffer();
        Assert.That(string.Join(",", _run.GetCurrentUpgradeOffers().Select(o => o.StableId)), Is.EqualTo(ids));
        Assert.That(_run.ChooseUpgradeOffer(0, _cards.ownedCards[25].Id), Is.True);
        Assert.That(string.Join(",", _run.GenerateMapForIndex(5).Select(n => $"{n.id}:{n.kind}:{n.contentId}")), Is.EqualTo(map));
    }

    [Test]
    public void Picker_CancelInvalidThenChooseAnother_AndRestartClearsPendingChoice()
    {
        var offer = OpenShopOffer();
        _cards.AddGold(40);
        MakePicker();
        _picker.OpenShop(offer.StableId);
        Assert.That(_picker.panel.activeSelf, Is.True);
        int invalidId = _cards.ownedCards[0].Id;
        int validId = _cards.ownedCards[1].Id;
        ClickTarget(invalidId);
        _cards.ApplyEnhancement(invalidId, _enhancement);
        _picker.confirmButton.onClick.Invoke();
        Assert.That(_cards.gold, Is.EqualTo(40));
        Assert.That(_picker.panel.activeSelf, Is.True);
        Assert.That(_picker.feedbackLabel.text, Does.Contain("already enhanced"));
        ClickTarget(validId);
        _picker.backButton.onClick.Invoke();
        Assert.That(_picker.SelectedCardId, Is.Zero);
        Assert.That(_cards.gold, Is.EqualTo(40));
        _picker.OpenShop(offer.StableId);
        ClickTarget(validId);
        _run.RestartRun();
        Assert.That(_picker.panel.activeSelf, Is.False);
        Assert.That(_picker.SelectedCardId, Is.Zero);
        Assert.That(_cards.gold, Is.Zero);
        Assert.That(_cards.ownedCards.All(c => c.Enhancement == null), Is.True);
    }

    [Test]
    public void Picker_ShopConfirmationChargesOnce_AndUpgradeCancelDoesNotComplete()
    {
        var offer = OpenShopOffer();
        _cards.AddGold(40);
        MakePicker();
        _picker.OpenShop(offer.StableId);
        int id = _cards.ownedCards[5].Id;
        ClickTarget(id);
        _picker.confirmButton.onClick.Invoke();
        Assert.That(_cards.gold, Is.EqualTo(40 - offer.price));
        Assert.That(_cards.FindOwnedCard(id).Enhancement, Is.SameAs(_enhancement));
        _picker.confirmButton.onClick.Invoke();
        Assert.That(_cards.gold, Is.EqualTo(40 - offer.price));
        _run.OnShopDone();
        var upgrade = _run.currentPath.First(n => n.kind == MapNodeType.Upgrade);
        upgrade.accessible = true;
        _run.OnPathChosen(upgrade.id);
        _picker.OpenUpgrade(0);
        _picker.backButton.onClick.Invoke();
        Assert.That(_run.ActiveNode, Is.SameAs(upgrade));
        Assert.That(upgrade.completed, Is.False);
    }

    ShopOffer OpenShopOffer()
    {
        var node = _run.currentPath.First(n => n.kind == MapNodeType.Shop);
        node.accessible = true;
        _run.OnPathChosen(node.id);
        return _run.GetCurrentShopOffers().First(o => o.kind == ShopOfferKind.Enhancement);
    }

    ShopOffer OpenUpgradeOffer()
    {
        var node = _run.currentPath.First(n => n.kind == MapNodeType.Upgrade);
        node.accessible = true;
        _run.OnPathChosen(node.id);
        return _run.GetCurrentUpgradeOffers()[0];
    }

    void MakePicker()
    {
        var root = new GameObject("Picker");
        _objects.Add(root);
        root.SetActive(false);
        _picker = root.AddComponent<EnhancementTargetUI>();
        _picker.panel = new GameObject("Target panel");
        _objects.Add(_picker.panel);
        _picker.cardsContainer = new GameObject("Card choices").transform;
        _objects.Add(_picker.cardsContainer.gameObject);
        var prefab = new GameObject("Card choice prefab", typeof(RectTransform), typeof(Image), typeof(Button));
        _objects.Add(prefab);
        new GameObject("Card label", typeof(RectTransform), typeof(TextMeshProUGUI)).transform.SetParent(prefab.transform);
        _picker.cardButtonPrefab = prefab;
        _picker.titleLabel = Make<TextMeshProUGUI>("Title");
        _picker.feedbackLabel = Make<TextMeshProUGUI>("Feedback");
        _picker.confirmButton = Make<Button>("Confirm");
        _picker.backButton = Make<Button>("Back");
        root.SetActive(true);
        InvokePicker("OnEnable");
    }

    void InvokePicker(string method) => typeof(EnhancementTargetUI)
        .GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        .Invoke(_picker, null);

    void ClickTarget(int id) => _picker.cardsContainer.Cast<Transform>()
        .Last(t => t && t.name == $"TargetCard_{id}").GetComponent<Button>().onClick.Invoke();

    T Make<T>(string name) where T : Component
    {
        var obj = new GameObject(name);
        _objects.Add(obj);
        return obj.AddComponent<T>();
    }

    EnemyTypeData Enemy(string name)
    {
        var enemy = EnemyTypeData.Create(name, 10, 3, 3);
        enemy.name = name;
        _assets.Add(enemy);
        return enemy;
    }
}
