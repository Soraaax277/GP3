using UnityEngine;

// Attach to QuestsPanel (the parent).
// QuestsPanel must be ACTIVE in the Editor at all times — the button is a child and needs to stay visible.
// UIAnimator on QuestsPanel handles all animation.
//
// UIAnimator Inspector settings:
//   - UI Type                    → SlidePanel
//   - Slide Direction            → Right
//   - Use Explicit Slide Positions → TRUE
//   - Explicit Hidden X          → 500
//   - Explicit Visible X         → 0
//   - Override Theme             → your QuestPanelTheme asset
//
// ToggleButton OnClick → QuestPanelUI.Toggle()

public class QuestPanelUI : MonoBehaviour
{
    [SerializeField] private UIAnimator uiAnimator;

    private bool isOpen = false;

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    private void Open()
    {
        if (isOpen) return;
        isOpen = true;
        uiAnimator.PlayEntryAnimation();
    }

    private void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        uiAnimator.AnimateExit(null);
    }
}