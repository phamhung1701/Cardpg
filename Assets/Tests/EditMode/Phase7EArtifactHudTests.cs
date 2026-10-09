using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed class Phase7EArtifactHudTests
{
    readonly List<Object> _assets = new();
    readonly List<GameObject> _objects = new();
    CardManager _cards;
    RunManager _run;
    RunStatusUI _hud;
    TMP_Text _artifactLabel;
    RelicData[] _artifacts;

    [SetUp]
    public void SetUp()
    {
        _artifacts = Enumerable.Range(1, 8)
            .Select(i => Artifact($"artifact_{i}", $"Artifact {i}", $"Icon{i}"))
            .ToArray();
        _cards = Make<CardManager>("CardManager");
        _cards.Configure(null, null, null, _artifacts);
        var combat = Make<CombatManager>("CombatManager");
        combat.ConfigurePlayer(30);
        _run = Make<RunManager>("RunManager");
        _run.thiefType = Enemy("Thief");
        _run.goblinType = Enemy("Goblin");
        _run.knightType = Enemy("Knight");

        var host = new GameObject("RunStatusUI fixture");
        host.SetActive(false);
        _objects.Add(host);
        _hud = host.AddComponent<RunStatusUI>();
        var label = new GameObject("OwnedArtifactsLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        _objects.Add(label);
        _artifactLabel = label.GetComponent<TMP_Text>();
        _hud.ownedArtifactsLabel = _artifactLabel;
        host.SetActive(true);
        InvokeHud("OnEnable"); // EditMode fixture invokes the UI subscription explicitly.
    }

    [TearDown]
    public void TearDown()
    {
        if (_hud) InvokeHud("OnDisable");
        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i]) Object.DestroyImmediate(_objects[i]);
        foreach (var asset in _assets)
            if (asset) Object.DestroyImmediate(asset);
        _objects.Clear();
        _assets.Clear();
    }

    [Test]
    public void EmptyConsumableSlots_HideOnlyThePlaceholderLabels()
    {
        var labels = new TMP_Text[3];
        var parents = new GameObject[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            parents[i] = new GameObject($"Backpack Slot {i + 1}");
            _objects.Add(parents[i]);
            var labelObject = new GameObject("ItemDescriptionLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            _objects.Add(labelObject);
            labelObject.transform.SetParent(parents[i].transform, false);
            labels[i] = labelObject.GetComponent<TMP_Text>();
        }
        _hud.backpackSlotLabels = labels;

        InvokeHud("Refresh");

        for (int i = 0; i < labels.Length; i++)
        {
            Assert.That(parents[i].activeSelf, Is.True, "Keep the consumable slot control available.");
            Assert.That(labels[i].gameObject.activeSelf, Is.False);
            Assert.That(labels[i].text, Is.Empty);
        }
    }

    [Test]
    public void EmptyCollection_ShowsZeroAndNoOwnedArtifacts()
    {
        Assert.That(_cards.ownedArtifacts, Is.Empty);
        Assert.That(_artifactLabel.text, Does.Contain("ARTIFACTS  •  0/5"));
        Assert.That(_artifactLabel.text, Does.Contain("None owned"));
    }

    [Test]
    public void Acquisition_UpdatesNameIconAndCountFromCardManager()
    {
        Assert.That(_cards.BuyArtifact(_artifacts[0], 0), Is.True);
        Assert.That(_cards.ownedArtifacts, Has.Count.EqualTo(1));
        Assert.That(_artifactLabel.text, Does.Contain("ARTIFACTS  •  1/5"));
        Assert.That(_artifactLabel.text, Does.Contain("Icon1 Artifact 1"));
        Assert.That(_artifactLabel.text, Does.Not.Contain("Artifact 2"));
    }

    [Test]
    public void MultipleOwnedArtifacts_AllAppearInAcquisitionOrderWithoutDisplayCap()
    {
        for (int i = 5; i < _artifacts.Length; i++)
            _artifacts[i].capacityCategory = ArtifactCapacityCategory.NonSlot;
        foreach (var artifact in _artifacts)
            Assert.That(_cards.BuyArtifact(artifact, 0), Is.True);
        Assert.That(_artifactLabel.text, Does.Contain("ARTIFACTS  •  5/5"));
        foreach (var artifact in _artifacts)
            Assert.That(_artifactLabel.text, Does.Contain($"{artifact.icon} {artifact.displayName}"));
        Assert.That(_artifactLabel.text.IndexOf("Artifact 1"), Is.LessThan(_artifactLabel.text.IndexOf("Artifact 8")));
        Assert.That(_cards.ownedArtifacts, Has.Count.EqualTo(8));
    }

    [Test]
    public void ClearingOwnedArtifacts_ThroughResetImmediatelyClearsHud()
    {
        _cards.BuyArtifact(_artifacts[0], 0);
        _cards.BuyArtifact(_artifacts[1], 0);
        _cards.Reset();
        Assert.That(_cards.ownedArtifacts, Is.Empty);
        Assert.That(_artifactLabel.text, Does.Contain("ARTIFACTS  •  0/5"));
        Assert.That(_artifactLabel.text, Does.Contain("None owned"));
        Assert.That(_artifactLabel.text, Does.Not.Contain("Artifact 1"));
    }

    [Test]
    public void RunStartAndRestart_ClearPriorArtifactsInHud()
    {
        _cards.BuyArtifact(_artifacts[0], 0);
        _run.StartRunWithSeed("ARTIFACT-HUD");
        Assert.That(_artifactLabel.text, Does.Contain("None owned"));
        _cards.BuyArtifact(_artifacts[1], 0);
        Assert.That(_artifactLabel.text, Does.Contain("Artifact 2"));
        _run.RestartRun();
        Assert.That(_cards.ownedArtifacts, Is.Empty);
        Assert.That(_artifactLabel.text, Does.Contain("ARTIFACTS  •  0/5"));
        Assert.That(_artifactLabel.text, Does.Not.Contain("Artifact 2"));
    }

    void InvokeHud(string method) => typeof(RunStatusUI)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(_hud, null);

    T Make<T>(string name) where T : Component
    {
        var obj = new GameObject(name);
        _objects.Add(obj);
        return obj.AddComponent<T>();
    }

    RelicData Artifact(string id, string displayName, string icon)
    {
        var artifact = ScriptableObject.CreateInstance<RelicData>();
        artifact.id = id;
        artifact.displayName = displayName;
        artifact.icon = icon;
        artifact.damageMultiplier = 1;
        _assets.Add(artifact);
        return artifact;
    }

    EnemyTypeData Enemy(string name)
    {
        var enemy = EnemyTypeData.Create(name, 10, 3, 3);
        enemy.name = name;
        _assets.Add(enemy);
        return enemy;
    }
}
