using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

public class GridTransitionManager : MonoBehaviour
{
    public static GridTransitionManager Instance;

    [Header("Configuration")]
    public GameObject cellPrefab;
    public int stripCount = 25;
    public float glitchInDuration  = 0.4f;  // Cover animation (leaving a scene)
    public float glitchOutDuration = 1.2f;  // Reveal animation (entering a scene)
    public float maxXOffset = 12f;           // Max horizontal glitch shift in pixels

    [Header("Fallback Panel / Loading Screen")]
    [Tooltip("Assign the Image GameObject that sits behind the strips. Add a CanvasGroup to it manually.")]
    public Image fallbackPanel;
    [Tooltip("The 4 loading screen UI Images to cycle through.")]
    public Image[] loadingImages;
    [Tooltip("How fast to swap between loading images (in seconds).")]
    public float imageCycleSpeed = 0.5f;

    [Tooltip("Color of the fallback panel. Set to white if using sprites!")]
    public Color fallbackColor = Color.white;
    [Tooltip("How long the fallback panel takes to gently fade in.")]
    public float fallbackFadeInDuration = 0.4f;
    [Tooltip("How long the fallback panel takes to fade out alongside the glitch-out.")]
    public float fallbackFadeOutDuration = 0.8f;

    private CanvasGroup      fallbackCanvasGroup;
    private Coroutine        imageCycleCoroutine;

    private List<RectTransform> strips     = new List<RectTransform>();
    private List<CanvasGroup>   stripGroups = new List<CanvasGroup>();
    private bool     isGridGenerated = false;
    private Sequence masterSequence;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject.transform.parent.gameObject);

            // Worst-case tween budget per AnimateGrid call:
            //   25 strips x 9 tweens (double-flicker path) = 225 tweeners
            //   25 strip sequences + 1 master             = 26  sequences
            // Two calls can briefly overlap (glitch-in fires, scene loads, glitch-out starts),
            // so double both budgets: 450 tweeners / 52 sequences.
            // Add 150 / 50 headroom for everything else in the project.
            DOTween.SetTweensCapacity(600, 100);
        }
        else
        {
            Destroy(gameObject.transform.parent.gameObject);
        }
    }

    void Start()
    {
        InitFallbackPanel();
        StartCoroutine(GenerateGridRoutine());
    }

    // ── Fallback panel setup ───────────────────────────────────────────────────

    private void InitFallbackPanel()
    {
        if (fallbackPanel == null) return;

        fallbackPanel.color = fallbackColor;

        fallbackCanvasGroup = fallbackPanel.GetComponent<CanvasGroup>();
        if (fallbackCanvasGroup == null)
        {
            // Auto-add it if they forgot, so the fade math never breaks and traps the player
            fallbackCanvasGroup = fallbackPanel.gameObject.AddComponent<CanvasGroup>();
        }

        if (fallbackCanvasGroup != null)
        {
            fallbackCanvasGroup.alpha = 0f;
            fallbackCanvasGroup.blocksRaycasts = false;
            fallbackCanvasGroup.interactable = false;
        }
        
        // Hide the entire panel immediately so it doesn't block UI or accidentally show up
        fallbackPanel.gameObject.SetActive(false);
    }

    // ── Grid generation ────────────────────────────────────────────────────────

    IEnumerator GenerateGridRoutine()
    {
        if (cellPrefab == null)
        {
            Debug.LogError("[GridTransitionManager] cellPrefab is missing in the Inspector! The glitch wipe cannot generate.");
            isGridGenerated = false;
            yield break;
        }

        RectTransform rt = GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        Canvas.ForceUpdateCanvases();
        yield return null;

        float totalHeight = rt.rect.height;
        float totalWidth  = rt.rect.width;
        float stripHeight = totalHeight / stripCount;

        for (int i = 0; i < stripCount; i++)
        {
            GameObject    cellObj = Instantiate(cellPrefab, transform);
            RectTransform strip   = cellObj.GetComponent<RectTransform>();

            strip.anchorMin = Vector2.zero;
            strip.anchorMax = Vector2.zero;
            strip.pivot     = new Vector2(0.5f, 0.5f);
            strip.sizeDelta = new Vector2(totalWidth, stripHeight);
            strip.anchoredPosition = new Vector2(
                totalWidth * 0.5f,
                stripHeight * i + stripHeight * 0.5f
            );

            CanvasGroup cg = cellObj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            strips.Add(strip);
            stripGroups.Add(cg);
        }

        isGridGenerated = true;
    }

    // ── Public entry point ─────────────────────────────────────────────────────

    public void LoadScene(string sceneName)
    {
        // The new flow: We do NOT glitch in and block the screen with static bars while loading.
        // We instantly bring up the custom animated loading screen.
        // Once loading is finished, the glitch-out plays to dynamically reveal the game world!
        StartCoroutine(LoadSceneWithFallback(sceneName));
    }

    // ── Scene loading with fallback ────────────────────────────────────────────

    // Starts the async load and instantly brings up the loading screen.
    // The loading screen cycles through its images organically while waiting.
    private IEnumerator LoadSceneWithFallback(string sceneName)
    {
        // Force the panel on just in case it was disabled
        if (fallbackPanel != null) fallbackPanel.gameObject.SetActive(true);

        // Start fading in the loading screen and kicking off the animation cycle immediately
        FadeFallbackPanel(1f, fallbackFadeInDuration);
        
        if (imageCycleCoroutine != null) StopCoroutine(imageCycleCoroutine);
        imageCycleCoroutine = StartCoroutine(CycleLoadingImages());

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;   // Hold until we're ready

        while (asyncLoad.progress < 0.9f)          // 0.9 = fully loaded, waiting for activation
        {
            yield return null;
        }

        // Scene is ready — activate it then kick off the reveal
        asyncLoad.allowSceneActivation = true;
        yield return asyncLoad;                    // Wait one frame for scene to fully activate

        // Give the new scene exactly one second to lay out its UI, then immediately drop the curtain!
        if (sceneName.ToLower().Contains("game") || sceneName.ToLower().Contains("main"))
        {
            float maxWait = 1f; // Do NOT freeze for 5 Mississippi seconds.
            float waited = 0f;
            while (waited < maxWait)
            {
                // Drop the curtain the exact microsecond we spot the UI canvas or TurnManager.
                GameObject eraCanvas = GameObject.Find("UI_EraCanvas");
                if (eraCanvas == null) eraCanvas = GameObject.Find("EraCanvas");
                
                if (eraCanvas != null && eraCanvas.activeInHierarchy)
                    break;
                
                yield return null;
                waited += Time.unscaledDeltaTime;
            }
        }

        StartCoroutine(AnimateOutAfterLoad());
    }

    private IEnumerator CycleLoadingImages()
    {
        if (loadingImages == null || loadingImages.Length == 0) yield break;

        // Ensure all are aggressively disabled
        for (int i = 0; i < loadingImages.Length; i++)
        {
            if (loadingImages[i] != null) loadingImages[i].gameObject.SetActive(false);
        }

        int index = 0;

        while (true)
        {
            // 1. Turn on the current image
            if (loadingImages[index] != null) 
                loadingImages[index].gameObject.SetActive(true);

            // 2. Wait exactly imageCycleSpeed seconds (0.5s by default)
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, imageCycleSpeed)); // Safety net

            // 3. Turn off the current image
            if (loadingImages[index] != null) 
                loadingImages[index].gameObject.SetActive(false);

            // 4. Move to the next element (0 -> 1 -> 2 -> 3)
            index = (index + 1) % loadingImages.Length;
        }
    }

    // ── Animate out + optional panel fade-out ─────────────────────────────────

    private IEnumerator AnimateOutAfterLoad()
    {
        yield return new WaitForSecondsRealtime(0.1f);

        // If the grid was generated successfully, use it to stylishly reveal the gameplay.
        // AnimateGrid(false) instantly forces the glitch bars to 100% opacity over the entire screen,
        // so we can instantly kill the loading screen safely behind them, then flicker open!
        if (isGridGenerated && cellPrefab != null)
        {
            AnimateGrid(false, null);
        }

        // Clean up the loading icons
        if (imageCycleCoroutine != null) StopCoroutine(imageCycleCoroutine);
        
        if (loadingImages != null)
        {
            for (int i = 0; i < loadingImages.Length; i++)
            {
                if (loadingImages[i] != null) 
                    loadingImages[i].gameObject.SetActive(false);
            }
        }

        // The glitch bars are heavily populated right now, so we can hide the panel
        // while it's safely obscured behind the animation.
        FadeFallbackPanel(0f, fallbackFadeOutDuration);
        yield return new WaitForSecondsRealtime(fallbackFadeOutDuration);
    }

    // ── Fallback panel tween helper ────────────────────────────────────────────

    private void FadeFallbackPanel(float targetAlpha, float duration)
    {
        if (fallbackCanvasGroup == null) return;

        if (targetAlpha > 0f) fallbackCanvasGroup.blocksRaycasts = true;

        DOTween.Kill(fallbackCanvasGroup);
        fallbackCanvasGroup
            .DOFade(targetAlpha, duration)
            .SetEase(targetAlpha > 0f ? Ease.OutQuad : Ease.InQuad)
            .OnComplete(() => {
                if (targetAlpha <= 0f)
                {
                    fallbackCanvasGroup.blocksRaycasts = false;
                    if (fallbackPanel != null) fallbackPanel.gameObject.SetActive(false);
                }
            })
            .SetUpdate(true);
    }

    // ── Grid animation ─────────────────────────────────────────────────────────

    private void AnimateGrid(bool show, System.Action onComplete)
    {
        // Kill everything in flight before creating new tweens
        masterSequence?.Kill();
        for (int i = 0; i < strips.Count; i++)
        {
            DOTween.Kill(stripGroups[i]);
            DOTween.Kill(strips[i]);
        }

        // Snap strips to a clean known state
        float canonicalX = strips.Count > 0 ? strips[0].anchoredPosition.x : 0f;
        for (int i = 0; i < strips.Count; i++)
        {
            Vector2 p = strips[i].anchoredPosition;
            p.x = canonicalX;
            strips[i].anchoredPosition = p;
            stripGroups[i].alpha = show ? 0f : 1f;
        }

        float duration      = show ? glitchInDuration : glitchOutDuration;
        float staggerWindow = duration * 0.5f;

        // ── Per-transition globals ─────────────────────────────────────────────
        // Changes the overall character of every glitch so no two look the same
        float globalSpeedMult  = Random.Range(0.75f, 1.25f); // anim runs faster or slower overall
        float globalOffsetMult = Random.Range(0.5f,  1.5f);  // shifts tighter or wilder overall

        masterSequence = DOTween.Sequence();
        masterSequence.SetUpdate(true);

        for (int i = 0; i < strips.Count; i++)
        {
            RectTransform strip = strips[i];
            CanvasGroup   cg    = stripGroups[i];

            // ── Per-strip randomisation ────────────────────────────────────────
            float randomDelay   = Random.Range(0f, staggerWindow);

            // Each strip gets its own flicker tempo — no sync'd pulsing across strips
            float flickerSpeed  = duration * Random.Range(0.10f, 0.22f) * globalSpeedMult;

            // X offset scaled by the per-transition global
            float xOffset       = Random.Range(-maxXOffset, maxXOffset) * globalOffsetMult;

            // Alpha targets vary per strip so the coverage looks uneven
            float alphaHigh     = Random.Range(0.65f, 0.95f);
            float alphaMid      = Random.Range(0.10f, 0.35f);
            float alphaLow      = Random.Range(0.20f, 0.45f);

            // ~35% of strips stutter twice before settling/cutting
            bool doubleFlicker  = Random.value > 0.65f;

            Vector2 originPos = strip.anchoredPosition;

            Sequence stripSeq = DOTween.Sequence();
            stripSeq.SetUpdate(true);

            if (show)
            {
                // First flicker hit
                stripSeq.Append(cg.DOFade(alphaHigh, flickerSpeed));
                stripSeq.Join(strip.DOAnchorPosX(originPos.x + xOffset, flickerSpeed));

                if (doubleFlicker)
                {
                    // Dip back down then spike again before settling
                    float xOffset2 = Random.Range(-maxXOffset, maxXOffset) * globalOffsetMult * 0.6f;
                    stripSeq.Append(cg.DOFade(alphaMid, flickerSpeed * 0.7f));
                    stripSeq.Join(strip.DOAnchorPosX(originPos.x + xOffset2, flickerSpeed * 0.7f));
                    stripSeq.Append(cg.DOFade(alphaHigh * 0.9f, flickerSpeed * 0.5f));
                }
                else
                {
                    stripSeq.Append(cg.DOFade(alphaMid, flickerSpeed));
                }

                // Settle into full opacity — duration also varied so strips don't finish together
                stripSeq.Append(cg.DOFade(1f, flickerSpeed * Random.Range(0.8f, 1.4f)).SetEase(Ease.OutQuad));
                stripSeq.Join(strip.DOAnchorPosX(originPos.x, flickerSpeed).SetEase(Ease.OutQuad));
            }
            else
            {
                // Initial stutter
                stripSeq.Append(cg.DOFade(alphaLow, flickerSpeed));
                stripSeq.Join(strip.DOAnchorPosX(originPos.x + xOffset, flickerSpeed));

                if (doubleFlicker)
                {
                    // Snap back partway, spike bright, then cut
                    float xOffset2 = Random.Range(-maxXOffset * 0.4f, maxXOffset * 0.4f) * globalOffsetMult;
                    stripSeq.Append(cg.DOFade(alphaHigh, flickerSpeed * 0.5f));
                    stripSeq.Join(strip.DOAnchorPosX(originPos.x + xOffset2, flickerSpeed * 0.5f));
                }
                else
                {
                    stripSeq.Append(cg.DOFade(alphaHigh * 0.85f, flickerSpeed));
                    stripSeq.Join(strip.DOAnchorPosX(originPos.x, flickerSpeed * 0.5f));
                }

                // Fade out — duration varied so strips don't all vanish at once
                stripSeq.Append(cg.DOFade(0f, flickerSpeed * Random.Range(0.8f, 1.3f)).SetEase(Ease.InQuad));
                stripSeq.Join(strip.DOAnchorPosX(originPos.x, flickerSpeed * 0.4f).SetEase(Ease.InQuad));
            }

            masterSequence.Insert(randomDelay, stripSeq);
        }

        masterSequence.OnComplete(() => onComplete?.Invoke());
    }
}