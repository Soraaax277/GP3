// unused now but was used for debugging the scroll position reset logic in TechTreeWindowManager. Can be deleted if we are sure the new logic is solid.

using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[DefaultExecutionOrder(-100)]
public class ResetScrollPosition : MonoBehaviour
{
    private ScrollRect parentScrollRect;
    private RectTransform myRect;

    private void Awake()
    {
        myRect = GetComponent<RectTransform>();
        parentScrollRect = GetComponentInParent<ScrollRect>();
    }

    private void OnEnable()
    {
        StopAllCoroutines();
        StartCoroutine(ForceResetRoutine());
    }

    private IEnumerator ForceResetRoutine()
    {
        // Disable scroll logic immediately
        if (parentScrollRect != null) {
            parentScrollRect.velocity = Vector2.zero;
            parentScrollRect.enabled = false;
        }

        // Force position for 2 frames (Unity needs this for layout rebuilds)
        for (int i = 0; i < 2; i++)
        {
            Canvas.ForceUpdateCanvases();
            if (myRect != null)
            {
                myRect.anchoredPosition = new Vector2(0, myRect.anchoredPosition.y);
            }
            yield return null; 
        }

        if (parentScrollRect != null) parentScrollRect.enabled = true;
    }
}