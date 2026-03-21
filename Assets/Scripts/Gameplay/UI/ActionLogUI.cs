using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ActionLogUI : MonoBehaviour
{
    public static ActionLogUI Instance;

    [Header("Panel Reference")]
    public GameObject panelRoot;

    [Header("Scroll Setup")]
    [Tooltip("ScrollRect inside the panel. Content needs Vertical Layout Group + Content Size Fitter.")]
    public ScrollRect scrollRect;

    [Header("Entry Prefab")]
    [Tooltip("Prefab with a single TextMeshProUGUI child.")]
    public GameObject entryPrefab;

    [Header("Empty State")]
    public GameObject emptyLabel;

    [Header("Animation Settings")]
    [Tooltip("How long each new entry takes to fade in.")]
    public float entryFadeDuration = 0.3f;
    [Tooltip("How long the log stays visible after the last message.")]
    public float displayDuration   = 5f;
    [Tooltip("How long the whole panel takes to fade out after displayDuration.")]
    public float fadeDuration      = 0.5f;

    // ── Pool ─────────────────────────────────────────────────────────────────
    private List<LogEntryRow> _pool           = new List<LogEntryRow>();
    private int               _activeRowCount = 0;

    // ── Fade state ────────────────────────────────────────────────────────────
    private float       lastActivityTime;
    private bool        isVisible = false;
    private CanvasGroup containerCanvasGroup;
    private Coroutine   fadeCoroutine;

    private class LogMessage
    {
        public string text;
        public Color  color;
    }
    private List<LogMessage> _messages = new List<LogMessage>();
    private const int MaxMessages = 10;

    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (scrollRect != null)
        {
            containerCanvasGroup = scrollRect.GetComponent<CanvasGroup>();
            if (containerCanvasGroup == null)
                containerCanvasGroup = scrollRect.gameObject.AddComponent<CanvasGroup>();
            containerCanvasGroup.alpha = 0f;
        }

        isVisible        = false;
        lastActivityTime = Time.time;
        UpdateEmptyLabel();
    }

    // -------------------------------------------------------------------------
    //  PUBLIC PANEL CONTROLS
    // -------------------------------------------------------------------------

    public void Show()   { if (panelRoot != null) panelRoot.SetActive(true);  }
    public void Hide()   { if (panelRoot != null) panelRoot.SetActive(false); }
    public void Toggle() { if (panelRoot != null) panelRoot.SetActive(!panelRoot.activeSelf); }

    // -------------------------------------------------------------------------

    private void Update()
    {
        if (isVisible && _activeRowCount > 0)
            if (Time.time - lastActivityTime > displayDuration)
                HideLogs();
    }

    // -------------------------------------------------------------------------
    //  LOG
    // -------------------------------------------------------------------------

    public void Log(string message, Color color)
    {
        if (scrollRect == null || entryPrefab == null) return;

        lastActivityTime = Time.time;

        // Format — identical to original
        string formatted = message;
        if (!message.StartsWith("["))
        {
            if      (color == Colors.Player)       formatted = "[Player] "       + message;
            else if (color == Colors.Enemy)        formatted = "[Enemy] "        + message;
            else if (color == Colors.World)        formatted = "[Environment] "  + message;
            else if (color == Colors.Construction) formatted = "[Construction] " + message;
            else if (color == Colors.Unit)         formatted = "[Unit] "         + message;
            else if (color == Colors.Neutral)      formatted = "[Control] "      + message;
        }

        // Cap at MaxMessages
        _messages.Add(new LogMessage { text = formatted, color = color });
        if (_messages.Count > MaxMessages)
        {
            _messages.RemoveAt(0);
            // Hide the oldest visible row — it will be reused by RebuildRows
            RebuildRowsSilent();
        }

        // Spawn only the NEW row and fade it in — don't rebuild everything
        AppendNewRow(formatted, color);

        if (!isVisible || containerCanvasGroup.alpha < 0.1f)
            ShowLogs();
    }

    // ── Appends one new row at the TOP and fades it in individually ─────────────
    private void AppendNewRow(string text, Color color)
    {
        LogEntryRow row = GetOrCreateRow(_activeRowCount);
        row.gameObject.SetActive(true);
        row.Set(text, color, 0f); // start at alpha 0

        // Move to top of content so newest entry is always first
        row.transform.SetSiblingIndex(0);

        _activeRowCount++;

        UpdateEmptyLabel();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

        // Scroll to top so the newest entry is visible
        scrollRect.normalizedPosition = new Vector2(0f, 1f);

        // Notify box — log now has content
        ActionLogBox.Instance?.NotifyLogChanged(true);

        // Fade this single row in
        StartCoroutine(FadeRowIn(row, entryFadeDuration));
    }

    // ── Rebuilds all rows silently (no fade) — used when capping at MaxMessages
    private void RebuildRowsSilent()
    {
        SetAllRowsHidden();
        _activeRowCount = 0;

        // Iterate in reverse so newest (_messages.Last) ends up at sibling index 0
        for (int i = _messages.Count - 1; i >= 0; i--)
        {
            LogEntryRow row = GetOrCreateRow(_activeRowCount);
            row.gameObject.SetActive(true);
            row.Set(_messages[i].text, _messages[i].color, 1f);
            row.transform.SetSiblingIndex(_activeRowCount);
            _activeRowCount++;
        }

        UpdateEmptyLabel();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        scrollRect.normalizedPosition = new Vector2(0f, 1f);
    }

    // ── Fades a single row's CanvasGroup from 0 to 1 ─────────────────────────
    private IEnumerator FadeRowIn(LogEntryRow row, float duration)
    {
        if (row == null) yield break;

        CanvasGroup cg = row.canvasGroup;
        if (cg == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (row == null || !row.gameObject.activeSelf) yield break;
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        cg.alpha = 1f;
    }

    // ── Pool helpers ──────────────────────────────────────────────────────────

    private void SetAllRowsHidden()
    {
        foreach (var row in _pool)
            if (row != null) row.gameObject.SetActive(false);
    }

    private LogEntryRow GetOrCreateRow(int index)
    {
        while (_pool.Count <= index)
        {
            GameObject  obj = Instantiate(entryPrefab, scrollRect.content);
            LogEntryRow row = obj.GetComponent<LogEntryRow>();

            if (row == null)
            {
                row = obj.AddComponent<LogEntryRow>();
                var labels = obj.GetComponentsInChildren<TextMeshProUGUI>(true);
                if (labels.Length >= 1) row.label = labels[0];
            }

            // Ensure each row has a CanvasGroup for individual fading
            if (row.canvasGroup == null)
                row.canvasGroup = obj.GetComponent<CanvasGroup>() ?? obj.AddComponent<CanvasGroup>();

            _pool.Add(row);
        }

        return _pool[index];
    }

    // ── Panel fade ──────────────────────────────────────────

    private void ShowLogs()
    {
        isVisible = true;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(1f));
    }

    private void HideLogs()
    {
        isVisible = false;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(0f));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        if (containerCanvasGroup == null) yield break;

        float startAlpha = containerCanvasGroup.alpha;
        float elapsed    = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            containerCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        containerCanvasGroup.alpha = targetAlpha;

        if (targetAlpha == 0f)
        {
            _messages.Clear();
            SetAllRowsHidden();
            _activeRowCount = 0;
            UpdateEmptyLabel();
            // Notify box — log is now empty
            ActionLogBox.Instance?.NotifyLogChanged(false);
        }

        fadeCoroutine = null;
    }

    private void UpdateEmptyLabel()
    {
        if (emptyLabel != null)
            emptyLabel.SetActive(_activeRowCount == 0);
    }

    // -------------------------------------------------------------------------
    //  STATIC API
    // -------------------------------------------------------------------------

    public static void Post(string message, Color color)
    {
        if (Instance != null) Instance.Log(message, color);
    }

    public static void PostFiltered(PlayerData actor, string message, Color color, bool isAlwaysVisible = false)
    {
        if (Instance == null) return;
        if (actor == null)    { Post(message, color); return; }

        if (actor.isAI && !isAlwaysVisible)
        {
            bool isSabotage  = message.ToLower().Contains("saboteur")  || message.ToLower().Contains("sabotage");
            bool isMarketing = message.ToLower().Contains("marketer") || message.ToLower().Contains("marketing");
            if (!isSabotage && !isMarketing) return;
        }

        Post(message, color);
    }

    public static string GetFriendlyName(string typeName) => typeName.Replace("Unit", "");

    public static class Colors
    {
        public static Color Player       = new Color(0.2f, 1f,   0.4f);
        public static Color Enemy        = new Color(1f,   0.3f, 0.3f);
        public static Color World        = new Color(1f,   1f,   0f);
        public static Color Unit         = new Color(0f,   0.8f, 1f);
        public static Color Construction = new Color(1f,   0.6f, 0f);
        public static Color Neutral      = Color.white;
    }
}