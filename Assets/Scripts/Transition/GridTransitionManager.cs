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
        StartCoroutine(GenerateGridRoutine());
    }

    IEnumerator GenerateGridRoutine()
    {
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

    public void LoadScene(string sceneName)
    {
        if (!isGridGenerated)
        {
            Debug.LogWarning("Grid not ready yet, loading instantly.");
            SceneManager.LoadScene(sceneName);
            return;
        }

        AnimateGrid(true, () =>
        {
            SceneManager.LoadScene(sceneName);
            StartCoroutine(AnimateOutAfterLoad());
        });
    }

    private IEnumerator AnimateOutAfterLoad()
    {
        yield return new WaitForSecondsRealtime(0.1f);
        AnimateGrid(false, null);
    }

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