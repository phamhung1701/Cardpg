using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class KeyButtonBorderStyleTests
{
    GameObject _buttonObject;

    [TearDown]
    public void TearDown()
    {
        if (_buttonObject) Object.DestroyImmediate(_buttonObject);
    }

    [Test]
    public void ApplyAddsPersistentVisibleOutlineWithoutChangingHitTargetOrRaycastGraphic()
    {
        _buttonObject = new GameObject("Primary Button", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = _buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(180f, 56f);
        var button = _buttonObject.GetComponent<Button>();
        var image = _buttonObject.GetComponent<Image>();
        button.targetGraphic = image;
        int graphicCount = _buttonObject.GetComponents<Graphic>().Length;

        KeyButtonBorderStyle.Apply(button);

        var outline = image.GetComponent<Outline>();
        Assert.That(outline, Is.Not.Null);
        Assert.That(outline.enabled, Is.True);
        Assert.That(outline.effectColor, Is.EqualTo(KeyButtonBorderStyle.BorderColor));
        Assert.That(outline.effectDistance, Is.EqualTo(KeyButtonBorderStyle.BorderOffset));
        Assert.That(button.targetGraphic, Is.SameAs(image));
        Assert.That(image.raycastTarget, Is.True);
        Assert.That(_buttonObject.GetComponents<Graphic>(), Has.Length.EqualTo(graphicCount),
            "The border is an Outline effect on the existing target Graphic, not a raycast-blocking overlay.");
        Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(180f, 56f)));

        button.interactable = false;
        Assert.That(outline.enabled, Is.True, "Disabled state retains the border while Button tint stays authoritative.");
    }

    [Test]
    public void ApplyIgnoresNullButtonsAndButtonsWithoutTargetGraphics()
    {
        KeyButtonBorderStyle.Apply(null);
        _buttonObject = new GameObject("No Target Button", typeof(RectTransform), typeof(Button));
        Assert.DoesNotThrow(() => KeyButtonBorderStyle.Apply(_buttonObject.GetComponent<Button>()));
    }
}
