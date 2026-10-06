using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RunEventUILayoutTests
{
    [Test]
    public void PrepareChoicesLayout_CreatesClippedScrollableContent()
    {
        var parent = new GameObject("EventCard", typeof(RectTransform));
        var contentObject = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentObject.transform.SetParent(parent.transform, false);
        var content = contentObject.GetComponent<RectTransform>();
        content.sizeDelta = new Vector2(700f, 300f);
        var ui = parent.AddComponent<RunEventUI>();
        typeof(RunEventUI).GetField("choicesContainer", BindingFlags.Instance | BindingFlags.Public)
            .SetValue(ui, content);

        try
        {
            typeof(RunEventUI).GetMethod("PrepareChoicesLayout", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ui, null);

            var scroll = parent.GetComponentInChildren<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.content, Is.EqualTo(content));
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(scroll.vertical, Is.True);
            Assert.That(content.GetComponent<ContentSizeFitter>().verticalFit,
                Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
            Assert.That(content.GetComponent<VerticalLayoutGroup>().childControlHeight, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(parent);
        }
    }

    [Test]
    public void ConfigureChoiceHeight_UsesCurrentViewportWidthForWrapping()
    {
        var button = new GameObject("Choice", typeof(RectTransform), typeof(Image));
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(button.transform, false);
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "A long event choice description that wraps over multiple lines and must remain reachable in the choice list.";
        try
        {
            var configure = typeof(RunEventUI).GetMethod("ConfigureChoiceHeight", BindingFlags.Static | BindingFlags.NonPublic);
            configure.Invoke(null, new object[] { button, label, 140f });
            float narrowHeight = button.GetComponent<LayoutElement>().preferredHeight;
            configure.Invoke(null, new object[] { button, label, 600f });
            float wideHeight = button.GetComponent<LayoutElement>().preferredHeight;

            Assert.That(narrowHeight, Is.GreaterThan(92f));
            Assert.That(narrowHeight, Is.GreaterThan(wideHeight));
            Assert.That(button.GetComponent<LayoutElement>().flexibleWidth, Is.EqualTo(1f));
            Assert.That(label.overflowMode, Is.EqualTo(TextOverflowModes.Overflow));
        }
        finally
        {
            Object.DestroyImmediate(button);
        }
    }

    [Test]
    public void UpgradeChoicesExpandToTheCardAndFitThreeRowsWithoutClipping()
    {
        var cardObject = new GameObject("Responsive EventCard", typeof(RectTransform));
        var card = cardObject.GetComponent<RectTransform>();
        card.sizeDelta = new Vector2(1320f, 500f);

        var descriptionObject = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
        descriptionObject.transform.SetParent(cardObject.transform, false);
        var descriptionRect = descriptionObject.GetComponent<RectTransform>();
        descriptionRect.anchorMin = new Vector2(0.1f, 0.63f);
        descriptionRect.anchorMax = new Vector2(0.9f, 0.8f);
        descriptionRect.sizeDelta = Vector2.zero;

        var choicesObject = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup));
        choicesObject.transform.SetParent(cardObject.transform, false);
        var choices = choicesObject.GetComponent<RectTransform>();
        choices.sizeDelta = new Vector2(700f, 300f);
        var layout = choicesObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;

        var ui = cardObject.AddComponent<RunEventUI>();
        typeof(RunEventUI).GetField("choicesContainer", BindingFlags.Instance | BindingFlags.Public)
            .SetValue(ui, choices);
        typeof(RunEventUI).GetField("descriptionLabel", BindingFlags.Instance | BindingFlags.Public)
            .SetValue(ui, descriptionObject.GetComponent<TMP_Text>());

        try
        {
            typeof(RunEventUI).GetMethod("PrepareChoicesLayout", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ui, null);
            for (int i = 0; i < 3; i++)
            {
                var row = new GameObject($"UpgradeChoice{i}", typeof(RectTransform), typeof(LayoutElement));
                row.transform.SetParent(choices, false);
                var element = row.GetComponent<LayoutElement>();
                element.minHeight = 92f;
                element.preferredHeight = 92f;
            }

            typeof(RunEventUI).GetMethod("RefreshChoiceHeights", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ui, null);
            var scroll = cardObject.GetComponentInChildren<ScrollRect>();
            float totalRows = 0f;
            foreach (Transform row in choices)
                totalRows += row.GetComponent<LayoutElement>().preferredHeight;
            totalRows += layout.spacing * (choices.childCount - 1);

            Assert.That(scroll.viewport.rect.width, Is.GreaterThan(choices.sizeDelta.x),
                "The choice viewport should use responsive card width rather than the old narrow fixed width.");
            Assert.That(scroll.viewport.rect.height, Is.GreaterThanOrEqualTo(totalRows),
                "Three standard-height Upgrade rows plus fitted spacing must fit without clipping.");
            Assert.That(layout.spacing, Is.LessThan(18f),
                "Spacing should compress only as much as needed to fit the available viewport.");
        }
        finally
        {
            Object.DestroyImmediate(cardObject);
        }
    }

    [Test]
    public void PrepareDescriptionLayout_ClipsLongDescriptionInsideScrollableViewport()
    {
        var parent = new GameObject("EventCard", typeof(RectTransform));
        var descriptionObject = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
        descriptionObject.transform.SetParent(parent.transform, false);
        var ui = parent.AddComponent<RunEventUI>();
        var label = descriptionObject.GetComponent<TextMeshProUGUI>();
        typeof(RunEventUI).GetField("descriptionLabel", BindingFlags.Instance | BindingFlags.Public)
            .SetValue(ui, label);

        try
        {
            typeof(RunEventUI).GetMethod("PrepareDescriptionLayout", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ui, null);

            var scroll = parent.GetComponentInChildren<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.content, Is.EqualTo(label.rectTransform));
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(label.overflowMode, Is.EqualTo(TextOverflowModes.Overflow));
            Assert.That(label.GetComponent<ContentSizeFitter>().verticalFit,
                Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
        }
        finally
        {
            Object.DestroyImmediate(parent);
        }
    }
}
