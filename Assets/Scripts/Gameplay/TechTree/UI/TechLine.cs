using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class TechLine : MonoBehaviour
{
    [Header("Settings")]
    public float animationDuration = 1.0f;
    public Ease animationEase = Ease.OutExpo;

    [Header("Debug Info (Auto-Filled)")]
    public float targetWidth; 
    public TechNode sourceNode; 
    public TechNode targetNode;

    private RectTransform rectTrans;

    private void Awake()
    {
        rectTrans = GetComponent<RectTransform>();
    }

    private void Start()
    {
        // Snap instantly on start (no animation)
        UpdateVisuals(false); 
    }

    public void UpdateVisuals(bool animate)
    {
        if (sourceNode == null) return;
        if (rectTrans == null) rectTrans = GetComponent<RectTransform>();

        // 1. Check if the SOURCE (Prerequisite) is unlocked
        if (sourceNode.IsUnlocked)
        {
            Vector2 endSize = new Vector2(targetWidth, rectTrans.sizeDelta.y);

            if (animate)
            {
                // Only animate if we are currently hidden (width near 0)
                if (rectTrans.sizeDelta.x < targetWidth * 0.1f)
                {
                    rectTrans.DOKill();
                    // --- THE FIX IS HERE: .SetUpdate(true) ---
                    rectTrans.DOSizeDelta(endSize, animationDuration)
                             .SetEase(animationEase)
                             .SetUpdate(true); // <--- IGNORES TIME.TIMESCALE = 0
                }
            }
            else
            {
                // Snap instantly (e.g. when opening the window)
                rectTrans.sizeDelta = endSize;
            }
        }
        else
        {
            // If Source is locked, hide the line
            rectTrans.sizeDelta = new Vector2(0, rectTrans.sizeDelta.y);
        }
    }
}