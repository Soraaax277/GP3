using UnityEngine;
using TMPro;

// Pool row for ActionLogUI. Has its own CanvasGroup so it can fade in independently.
public class LogEntryRow : MonoBehaviour
{
    public TextMeshProUGUI label;
    public CanvasGroup     canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    }

    // <summary>Sets text, color and starting alpha (0 = invisible, 1 = fully visible).</summary>
    public void Set(string message, Color color, float startAlpha = 1f)
    {
        if (label != null)
        {
            label.text  = message;
            label.color = color;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = startAlpha;
    }
}