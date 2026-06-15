using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;
using System.Collections;

// Coordinates era transition cutscenes.
//
// GLITCH + CAMERA SWAP SEQUENCE:
//
//   IN:
//     1. Enable glitch feature, ramp 0 → 0.5  (game scene still fully visible)
//     2. Load EraCutsceneScene additively      (hidden behind peak glitch)
//     3. At peak: disable game camera + canvas + objects
//                 activate cutscene era camera
//                 call EraCutsceneController.SignalCameraReady()
//     4. Ramp 0.5 → 1                          (glitch clears into cutscene)
//     5. EraRendererController.ForceSync()
//
//   OUT (after cutscene signals complete):
//     1. Reset glitch to 0, ramp 0 → 0.5      (cutscene still fully visible)
//     2. At peak: deactivate cutscene era camera
//                 re-enable game camera + canvas + objects
//     3. Ramp 0.5 → 1                          (glitch clears back into game)
//     4. Disable glitch feature
//     5. Unload EraCutsceneScene
//     6. HUD pulse
public class EraAnnouncementController : MonoBehaviour
{
    public static EraAnnouncementController Instance;

    [Header("Cutscene Scene")]
    [Tooltip("Exact name of the scene to load additively (must be in Build Settings).")]
    public string cutsceneSceneName = "EraCutsceneScene";

    [Header("Glitch Transition")]
    [Tooltip("The SignalGlitchFeature on your URP Renderer asset.")]
    public SignalGlitchFeature glitchFeature;
    [Tooltip("Time in seconds to ramp from 0→0.5 (build up) or 0.5→1 (clear). Keep short, e.g. 0.35.")]
    public float glitchRampDuration = 0.35f;

    [Header("Main Scene References")]
    [Tooltip("Your main game Canvas (HUD). Disabled at peak glitch when entering cutscene.")]
    public Canvas mainGameCanvas;
    [Tooltip("Your main game Camera. Disabled at peak glitch; re-enabled at peak glitch on exit.")]
    public Camera mainGameCamera;
    [Tooltip("Any game scene root objects to hide during cutscene (grid, terrain, buildings, etc.).")]
    public GameObject[] gameSceneObjectsToHide;

    [Header("HUD Era Text")]
    [Tooltip("GameStatusUI.eraText — gets a color pulse after the cutscene ends.")]
    public TextMeshProUGUI hudEraText;

    [Header("Timing")]
    [Tooltip("One-time delay before the very first announcement so scene fade-in can finish.")]
    public float startupDelay     = 1.5f;
    public float hudPunchDuration = 0.4f;

    [Header("HUD Accent Colors")]
    [Tooltip("One color per era (index matches TurnManager.GameEra).")]
    public Color[] eraAccentColors = new Color[]
    {
        new Color(0.85f, 0.72f, 0.45f),   // Industrial
        new Color(0.30f, 0.90f, 1.00f),   // EarlyEighties
        new Color(0.20f, 0.90f, 0.35f),   // Retro
        new Color(0.15f, 0.50f, 1.00f),   // Futuristic
    };

    // Read by EraCutsceneController in Awake().
    public TurnManager.GameEra currentEra { get; private set; }

    private bool _isPlaying     = false;
    private bool _hasPlayedOnce = false;
    private bool _cutsceneDone  = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;

        // Glitch feature starts disabled — enabled only during transitions.
        if (glitchFeature != null) glitchFeature.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void TriggerAnnouncement(TurnManager.GameEra era)
    {
        if (_isPlaying) return;
        StartCoroutine(PlayAnnouncement(era));
    }

    public void ForceTriggerAnnouncement(TurnManager.GameEra era)
    {
        StopAllCoroutines();
        _isPlaying    = false;
        _cutsceneDone = false;

        if (SceneManager.GetSceneByName(cutsceneSceneName).isLoaded)
            SceneManager.UnloadSceneAsync(cutsceneSceneName);

        // Safety restore in case we interrupted mid-transition.
        SetGameSceneVisible(true);
        if (glitchFeature != null)
        {
            glitchFeature.SetProgress(0f);
            glitchFeature.SetActive(false);
        }

        StartCoroutine(PlayAnnouncement(era));
    }

    // Called by EraCutsceneController when its sequence finishes.
    public void OnCutsceneComplete()
    {
        _cutsceneDone = true;
    }

    // ── Core sequence ─────────────────────────────────────────────────────────

    private IEnumerator PlayAnnouncement(TurnManager.GameEra era)
    {
        _isPlaying    = true;
        _cutsceneDone = false;

        if (!_hasPlayedOnce)
        {
            _hasPlayedOnce = true;
            yield return new WaitForSeconds(startupDelay);
        }

        currentEra = era;

        // ── IN: Build glitch with game scene still fully visible ───────────────
        if (glitchFeature != null)
        {
            glitchFeature.SetActive(true);
            glitchFeature.SetProgress(0f);
        }
        yield return StartCoroutine(RampGlitch(0f, 0.5f, glitchRampDuration));

        // ── Load cutscene scene behind peak glitch ────────────────────────────
        yield return SceneManager.LoadSceneAsync(cutsceneSceneName, LoadSceneMode.Additive);

        // ── Peak: hide game scene, then activate cutscene camera ──────────────
        //
        // FIX 1: SetGameSceneVisible(false) must come BEFORE the camera null-check.
        // Previously it lived after the null-check, so a missing camera would bail
        // via yield break and leave the game scene and glitch transition both stuck
        // in their mid-transition state with no recovery path.
        SetGameSceneVisible(false);

        int eraIndex = Mathf.Clamp((int)era, 0, 3);

        // FIX 2: Use FindFirstObjectOfType as a fallback alongside the static
        // Instance. After an additive load, Awake() has run and set Instance, but
        // defensive fallback guards against any edge case where it hasn't resolved.
        EraCutsceneController cutsceneController = EraCutsceneController.Instance;
        if (cutsceneController == null)
            cutsceneController = FindObjectOfType<EraCutsceneController>();

        Camera cutsceneCam = cutsceneController != null
            ? cutsceneController.GetEraCamera(eraIndex)
            : null;

        if (cutsceneCam == null)
        {
            Debug.LogError("[EraAnnouncementController] Could not find cutscene era camera. " +
                           "Check that EraCutsceneController is in EraCutsceneScene and era slots are assigned.");
            // Safety restore — game scene was already hidden above, so put it back.
            SetGameSceneVisible(true);
            if (glitchFeature != null) glitchFeature.SetActive(false);
            yield return SceneManager.UnloadSceneAsync(cutsceneSceneName);
            _isPlaying = false;
            yield break;
        }

        cutsceneCam.gameObject.SetActive(true);

        // Swap renderer feature at peak glitch — same frame as the camera swap.
        // Pass era directly; TurnManager.currentEra hasn't advanced yet at this
        // point, so the parameterless ForceSync() would apply the wrong feature.
        if (EraRendererController.Instance != null)
            EraRendererController.Instance.ForceSync(era);

        // Signal EraCutsceneController that the camera is live — starts movement + wobble.
        cutsceneController.SignalCameraReady();

        // ── Clear glitch into cutscene ────────────────────────────────────────
        yield return StartCoroutine(RampGlitch(0.5f, 1f, glitchRampDuration));

        if (glitchFeature != null) glitchFeature.SetActive(false);

        // ── Wait for cutscene to finish ───────────────────────────────────────
        yield return new WaitUntil(() => _cutsceneDone);

        // ── OUT: Build glitch with cutscene still fully visible ───────────────
        if (glitchFeature != null)
        {
            glitchFeature.SetActive(true);
            glitchFeature.SetProgress(0f);
        }
        yield return StartCoroutine(RampGlitch(0f, 0.5f, glitchRampDuration));

        // ── Peak: deactivate cutscene camera, restore game scene ──────────────
        if (cutsceneCam != null) cutsceneCam.gameObject.SetActive(false);
        SetGameSceneVisible(true);

        // ── Clear glitch back into game ───────────────────────────────────────
        yield return StartCoroutine(RampGlitch(0.5f, 1f, glitchRampDuration));

        if (glitchFeature != null)
        {
            glitchFeature.SetProgress(0f);
            glitchFeature.SetActive(false);
        }

        // ── Unload cutscene scene ─────────────────────────────────────────────
        yield return SceneManager.UnloadSceneAsync(cutsceneSceneName);

        // ── HUD pulse ─────────────────────────────────────────────────────────
        if (hudEraText != null)
        {
            Color accent = eraAccentColors[Mathf.Clamp((int)era, 0, eraAccentColors.Length - 1)];
            Color orig   = hudEraText.color;
            hudEraText.DOColor(accent, hudPunchDuration * 0.5f)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => hudEraText.DOColor(orig, hudPunchDuration * 0.5f).SetEase(Ease.InCubic));
            hudEraText.transform.DOPunchScale(Vector3.one * 0.15f, hudPunchDuration, 2, 0.5f);
        }

        _isPlaying = false;
    }

    // ── Glitch ramp ───────────────────────────────────────────────────────────

    private IEnumerator RampGlitch(float from, float to, float duration)
    {
        if (glitchFeature == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / duration);
            glitchFeature.SetProgress(Mathf.Lerp(from, to, t));
            yield return null;
        }
        glitchFeature.SetProgress(to);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetGameSceneVisible(bool visible)
    {
        if (mainGameCanvas != null) mainGameCanvas.gameObject.SetActive(visible);
        if (mainGameCamera != null) mainGameCamera.gameObject.SetActive(visible);
        if (gameSceneObjectsToHide != null)
            foreach (GameObject go in gameSceneObjectsToHide)
                if (go != null) go.SetActive(visible);
    }
}