using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

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
    [SerializeField] private ScrollRect scrollRect;

    [Header("Quest Template")]
    public TMP_Text questTextTemplate; // Base template — will be hidden and cloned
    public Transform questContainer;

    [Header("Dedicated Quest Slots (Optional — assign in Inspector)")]
    [Tooltip("Text component that shows the current Mini quest. If assigned, it will be used instead of a pooled slot.")]
    public TMP_Text miniQuestText;

    [Tooltip("Text component that shows the current Main quest. If assigned, it will be used instead of a pooled slot.")]
    public TMP_Text mainQuestText;

    [Tooltip("Text component that shows the current Major quest. If assigned, it will be used instead of a pooled slot.")]
    public TMP_Text majorQuestText;

    // Fallback pooled slots (used when the dedicated text refs above are NOT assigned)
    private List<TMP_Text> questSlots = new List<TMP_Text>();

    private bool isOpen = false;

    private void Awake()
    {
        if (questTextTemplate != null) questTextTemplate.gameObject.SetActive(false);

        // 1. Find or Setup Layout
        VerticalLayoutGroup vlg = GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = GetComponentInChildren<VerticalLayoutGroup>();
        
        if (vlg != null)
        {
            vlg.spacing = 20f;
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperLeft;

            // 2. Ensure ContentSizeFitter exists so the container grows
            ContentSizeFitter csf = vlg.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = vlg.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            questContainer = vlg.transform;
        }

        // 3. Find ScrollRect if not assigned
        if (scrollRect == null) scrollRect = GetComponentInParent<ScrollRect>();
        if (scrollRect != null && vlg != null)
        {
            scrollRect.content = vlg.GetComponent<RectTransform>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.viewport = scrollRect.GetComponentInChildren<RectMask2D>()?.GetComponent<RectTransform>();
        }

        if (questTextTemplate != null) ConfigureText(questTextTemplate);

        // Hide dedicated slots by default (shown when data arrives)
        if (miniQuestText  != null) miniQuestText.gameObject.SetActive(false);
        if (mainQuestText  != null) mainQuestText.gameObject.SetActive(false);
        if (majorQuestText != null) majorQuestText.gameObject.SetActive(false);
    }

    private void Start()
    {
        // Register with CameraController to block inputs when mouse is over this panel
        if (CameraController.Instance != null)
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null && !CameraController.Instance.hoverBlockingPanels.Contains(rt))
            {
                CameraController.Instance.hoverBlockingPanels.Add(rt);
            }

            // Also register the scroll rect's parent or viewport if they are different
            if (scrollRect != null)
            {
                RectTransform srt = scrollRect.GetComponent<RectTransform>();
                if (srt != null && !CameraController.Instance.hoverBlockingPanels.Contains(srt))
                {
                    CameraController.Instance.hoverBlockingPanels.Add(srt);
                }
            }
        }
    }

    private void ConfigureText(TMP_Text txt)
    {
        if (txt == null) return;
        txt.richText = true;
        txt.enableWordWrapping = true;
        txt.overflowMode = TextOverflowModes.Overflow;
    }

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

    /// <summary>
    /// Refreshes the displayed quests.
    /// If dedicated inspector slots (miniQuestText, mainQuestText, majorQuestText) are assigned,
    /// each tier is displayed in its own slot — visible only when an active quest of that tier exists.
    /// Otherwise, falls back to a generic pooled list.
    /// </summary>
    public void RefreshQuestData(List<QuestData> activeQuests, Dictionary<string, bool> completionStatus)
    {
        bool useDedicatedSlots = (miniQuestText != null || mainQuestText != null || majorQuestText != null);

        if (useDedicatedSlots)
        {
            RefreshDedicatedSlots(activeQuests, completionStatus);
        }
        else
        {
            RefreshPooledSlots(activeQuests, completionStatus);
        }
    }

    // ─── Dedicated-slot path ───────────────────────────────────────────────────

    private void RefreshDedicatedSlots(List<QuestData> activeQuests, Dictionary<string, bool> completionStatus)
    {
        // Find by tier
        QuestData miniQ  = null;
        QuestData mainQ  = null;
        QuestData majorQ = null;

        foreach (var q in activeQuests)
        {
            if (q.tier == QuestTier.Mini  && miniQ  == null) miniQ  = q;
            if (q.tier == QuestTier.Main  && mainQ  == null) mainQ  = q;
            if (q.tier == QuestTier.Major && majorQ == null) majorQ = q;
        }

        UpdateDedicatedSlot(miniQuestText,  miniQ,  completionStatus);
        UpdateDedicatedSlot(mainQuestText,  mainQ,  completionStatus);
        UpdateDedicatedSlot(majorQuestText, majorQ, completionStatus);
    }

    private void UpdateDedicatedSlot(TMP_Text slot, QuestData q, Dictionary<string, bool> completionStatus)
    {
        if (slot == null) return;

        if (q == null)
        {
            slot.gameObject.SetActive(false);
            return;
        }

        slot.gameObject.SetActive(true);
        ConfigureText(slot);

        bool isDone = completionStatus != null && completionStatus.ContainsKey(q.id) && completionStatus[q.id];
        string content = BuildQuestText(q, isDone);
        slot.text  = content;
        slot.alpha = isDone ? 0.6f : 1.0f;
    }

    // ─── Pooled-slot path (fallback) ──────────────────────────────────────────

    private void RefreshPooledSlots(List<QuestData> activeQuests, Dictionary<string, bool> completionStatus)
    {
        if (questContainer == null || questTextTemplate == null) return;

        // Ensure we have enough slots
        while (questSlots.Count < activeQuests.Count)
        {
            TMP_Text newSlot = Instantiate(questTextTemplate, questContainer);
            ConfigureText(newSlot);
            questSlots.Add(newSlot);
        }

        // Update slots
        for (int i = 0; i < questSlots.Count; i++)
        {
            if (i < activeQuests.Count)
            {
                QuestData q = activeQuests[i];
                bool isDone = completionStatus != null && completionStatus.ContainsKey(q.id) && completionStatus[q.id];

                questSlots[i].gameObject.SetActive(true);
                questSlots[i].text  = BuildQuestText(q, isDone);
                questSlots[i].alpha = isDone ? 0.6f : 1.0f;
            }
            else
            {
                questSlots[i].gameObject.SetActive(false);
            }
        }
    }

    // ─── Shared text builder ──────────────────────────────────────────────────

    private string BuildQuestText(QuestData qData, bool isDone)
    {
        string tierLabel = qData.tier.ToString().ToUpper();
        string tierColor = "#FFFFFF";
        if (qData.tier == QuestTier.Mini)  tierColor = "#B0B0B0"; // Light Grey
        if (qData.tier == QuestTier.Main)  tierColor = "#FFA500"; // Orange
        if (qData.tier == QuestTier.Major) tierColor = "#FFD700"; // Gold

        string label   = $"<color={tierColor}>[{tierLabel}]</color>";
        string desc    = $"<b>{qData.description}</b>";
        string rewards = $"<size=80%><color=#FFD700>+{qData.goldReward} Gold</color> | <color=#00FFFF>+{qData.rpReward} RP</color></size>";

        string content = $"{label} {desc}\n{rewards}";

        if (isDone)
        {
            content = $"<s><color=#888888>{content}</color></s> <color=#00FF00>[DONE]</color>";
        }

        return content;
    }
}