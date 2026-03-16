using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ActionLogUI : MonoBehaviour
{
    public static ActionLogUI Instance;

    [Header("Hierarchy Assignment")]
    [Tooltip("The parent object (Content) that contains the Layout Group.")]
    public Transform logContainer;
    
    [Tooltip("Optional: A text object that says 'No messages' or similar.")]
    public GameObject emptyLabel;

    [Header("Item Template")]
    [Tooltip("The GameObject in the scene used as a template for log entries.")]
    public GameObject logItemTemplate;

    [Header("Animation Settings")]
    public float displayDuration = 5f;
    public float fadeDuration = 0.5f;

    private List<GameObject> activeLogs = new List<GameObject>();
    private float lastActivityTime;
    private bool isVisible = true;
    private CanvasGroup containerCanvasGroup;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Hide the template so it doesn't show up in the UI at start
        if (logItemTemplate != null)
        {
            logItemTemplate.SetActive(false);
        }

        // Setup the container for fading
        if (logContainer != null)
        {
            containerCanvasGroup = logContainer.GetComponent<CanvasGroup>();
            if (containerCanvasGroup == null) containerCanvasGroup = logContainer.gameObject.AddComponent<CanvasGroup>();
            
            // Start transparent
            containerCanvasGroup.alpha = 0f;
            isVisible = false;
        }

        UpdateEmptyLabel();
        lastActivityTime = Time.time;
    }

    private void Update()
    {
        // Auto-fade out after duration (5 seconds as requested)
        if (isVisible && activeLogs.Count > 0)
        {
            if (Time.time - lastActivityTime > displayDuration)
            {
                HideLogs();
            }
        }
    }

    public void Log(string message, Color color)
    {
        if (logItemTemplate == null || logContainer == null) 
        {
            return;
        }

        lastActivityTime = Time.time;
        
        // Clone the template
        GameObject logObj = Instantiate(logItemTemplate, logContainer);
        logObj.SetActive(true); 
        
        TextMeshProUGUI text = logObj.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            // Format message with bracketed tags if not already present
            string formattedMessage = message;
            if (!message.StartsWith("["))
            {
                if (color == Colors.Player) formattedMessage = "[Player] " + message;
                else if (color == Colors.Enemy) formattedMessage = "[Enemy] " + message;
                else if (color == Colors.World) formattedMessage = "[Environment] " + message;
                else if (color == Colors.Construction) formattedMessage = "[Construction] " + message;
                else if (color == Colors.Unit) formattedMessage = "[Unit] " + message;
                else if (color == Colors.Neutral) formattedMessage = "[Control] " + message;
            }

            text.text = formattedMessage;
            text.color = color;
        }

        activeLogs.Add(logObj);
        UpdateEmptyLabel();
        
        // Ensure logs are visible
        if (!isVisible || containerCanvasGroup.alpha < 0.1f)
        {
            ShowLogs();
        }

        // Limit count (keep last 10)
        if (activeLogs.Count > 10)
        {
            GameObject oldest = activeLogs[0];
            activeLogs.RemoveAt(0);
            if (oldest != null) Destroy(oldest);
        }
    }

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
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            containerCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        containerCanvasGroup.alpha = targetAlpha;

        if (targetAlpha == 0f)
        {
            ClearLogs();
            UpdateEmptyLabel();
        }
        
        fadeCoroutine = null;
    }

    private void UpdateEmptyLabel()
    {
        if (emptyLabel != null)
        {
            emptyLabel.SetActive(activeLogs.Count == 0);
        }
    }

    private void ClearLogs()
    {
        foreach (var log in activeLogs)
        {
            if (log != null) Destroy(log);
        }
        activeLogs.Clear();
    }

    public static void Post(string message, Color color)
    {
        if (Instance != null) Instance.Log(message, color);
    }

    /// <summary>
    /// Filters logs so we only show enemy actions if they are Saboteurs or SalesMarketers affecting the player.
    /// </summary>
    public static void PostFiltered(PlayerData actor, string message, Color color, bool isAlwaysVisible = false)
    {
        if (Instance == null) return;
        if (actor == null) { Post(message, color); return; }

        // Rule: Don't log enemy unless it's Saboteur/SalesMarketer (or explicitly always visible like World Events)
        if (actor.isAI && !isAlwaysVisible)
        {
            bool isSabotage = message.ToLower().Contains("saboteur") || message.ToLower().Contains("sabotage");
            bool isMarketing = message.ToLower().Contains("marketer") || message.ToLower().Contains("marketing");
            
            if (!isSabotage && !isMarketing) return;
        }

        Post(message, color);
    }

    public static string GetFriendlyName(string typeName)
    {
        return typeName.Replace("Unit", "").Replace("Unit", ""); // Basic cleanup
    }

    public static class Colors
    {
        public static Color Player = new Color(0.2f, 1f, 0.4f); 
        public static Color Enemy = new Color(1f, 0.3f, 0.3f);  
        public static Color World = new Color(1f, 1f, 0f);      
        public static Color Unit = new Color(0f, 0.8f, 1f);    
        public static Color Construction = new Color(1f, 0.6f, 0f); 
        public static Color Neutral = Color.white;
    }
}
