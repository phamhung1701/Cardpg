using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class SaveLoadSceneTransitionTests
{
    [UnityTest]
    public IEnumerator HidingPanelDuringSceneTeardown_DoesNotStartCoroutineOnInactiveParent()
    {
        var parent = new GameObject("Save transition parent", typeof(RectTransform));
        var panel = new GameObject("Save transition panel", typeof(RectTransform));
        panel.transform.SetParent(parent.transform, false);
        panel.AddComponent<BoardPanelTransition>();
        try
        {
            parent.SetActive(false);
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(panel.activeInHierarchy, Is.False);
            BoardPanelTransition.Hide(panel);
            Assert.That(panel.activeSelf, Is.False);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
        finally { Object.Destroy(parent); }
    }
}
