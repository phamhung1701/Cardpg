using System;
using System.Linq;

public partial class CardManager
{
    public CardManagerSaveData CaptureSave()
    {
        EnsureArtifactInstances();
        return new CardManagerSaveData
        {
            gold = gold, nextCardId = _nextCardId, random = _cards.CaptureRandomState(),
            artifactIdWatermark = ArtifactRuntimeInstance.IdentityWatermark,
            owned = ownedCards.Select(card => card.CaptureSave()).ToList(),
            deck = deck.Select(card => card.Id).ToList(), hand = hand.Select(card => card.Id).ToList(),
            discard = discardPile.Select(card => card.Id).ToList(),
            artifacts = _orderedArtifactInstances.Select(item => new ArtifactSaveData
            {
                id = item.Id, contentId = item.Definition.id, effects = item.State.CaptureSave(),
                preparedCardId = item.PreparedDwarfSourceCard?.Id ?? 0, pendingCardId = item.PendingDwarfSourceCard?.Id ?? 0
            }).ToList(),
            backpack = _backpack.Select(slot => new ConsumableSlotSaveData
            { contentId = slot.Definition.id, charges = slot.Items.Select(item => item.RemainingCharges).ToList() }).ToList()
        };
    }

    // Caller validates the entire snapshot before any owner is changed. No purchase/grant/draw hooks run here.
    internal void RestoreSave(CardManagerSaveData data, SaveContentCatalog catalog)
    {
        ClearCardState();
        _enemyTargetHandler = null; _enemyTargetEligibility = null;
        gold = data.gold; _nextCardId = data.nextCardId;
        relics.Clear(); ownedArtifacts.Clear(); _orderedArtifactInstances.Clear(); _backpack.Clear();
        _consumableUsedThisEncounter = false;
        var restored = data.owned.Select(saved =>
        {
            var definition = CardData.Create((CardData.Suit)saved.definitionSuit, (CardData.Rank)saved.rank);
            _generatedDefinitions.Add(definition);
            return CardInstance.Restore(definition, saved, string.IsNullOrEmpty(saved.enhancementId) ? null : catalog.Enhancement(saved.enhancementId));
        }).ToList();
        _cards.RestoreZones(restored, data.deck, data.hand, data.discard, data.random);
        foreach (var saved in data.artifacts)
        {
            var definition = catalog.Artifact(saved.contentId);
            var artifact = ArtifactRuntimeInstance.Restore(definition, saved.id, saved.effects);
            artifact.PreparedDwarfSourceCard = FindOwnedCard(saved.preparedCardId);
            artifact.PendingDwarfSourceCard = FindOwnedCard(saved.pendingCardId);
            ownedArtifacts.Add(definition); _orderedArtifactInstances.Add(artifact); relics.Add(definition.id);
        }
        ArtifactRuntimeInstance.RestoreIdentityWatermark(data.artifactIdWatermark);
        foreach (var saved in data.backpack)
        {
            var definition = catalog.Consumable(saved.contentId);
            var slot = new ConsumableStack(definition);
            foreach (int charge in saved.charges) slot.Add(ConsumableInstance.Restore(definition, charge));
            _backpack.Add(slot);
        }
    }

    internal void PresentRestoredSave()
    {
        if (CanCreateViews()) foreach (var card in hand) CreateView(card);
        OnGoldChanged?.Invoke(gold); OnDeckChanged?.Invoke(); OnBuildChanged?.Invoke();
        OnConsumablesChanged?.Invoke(); OnCardSelected?.Invoke(null); OnEnemyTargetingChanged?.Invoke(false);
    }
}
