using System.Collections;
using UnityEngine;

/// <summary>
/// Smoothly crossfades between two URP Renderer Feature materials by lerping
/// an _Intensity float on each — one fades out while the other fades in.
///
/// REQUIREMENTS
///   Your renderer feature shaders must expose a float property named
///   _Intensity (0 = no effect, 1 = full effect). The transition drives
///   exactly this property on both materials simultaneously.
///
/// SETUP
///   1. In your URP Renderer asset, add both features (e.g. B&W and Vibrant).
///      Both should be ALWAYS ENABLED — this script controls visibility via
///      _Intensity, not SetActive. Starting _Intensity: RF1 = 1, RF2 = 0.
///   2. Assign rf1Material and rf2Material in the Inspector (the materials
///      used by each renderer feature's Blit pass).
///   3. Call TransitionTo(1) or TransitionTo(2) from anywhere — e.g.
///      MainMenuManager can call it on era change or scene event.
///
/// EASING
///   The transitionCurve AnimationCurve controls the feel of the blend.
///   A smooth S-curve (ease in + ease out) is the default. You can swap it
///   for a linear, ease-in-only, or spring curve directly in the Inspector.
/// </summary>
public class RendererFeatureTransition : MonoBehaviour
{
    [Header("Renderer Feature Materials")]
    [Tooltip("Material used by Renderer Feature 1 (e.g. Black & White). " +
             "Must have an _Intensity float property.")]
    public Material rf1Material;

    [Tooltip("Material used by Renderer Feature 2 (e.g. Vibrant). " +
             "Must have an _Intensity float property.")]
    public Material rf2Material;

    [Header("Transition Settings")]
    [Tooltip("How many seconds the crossfade takes.")]
    [Range(0.1f, 5f)]
    public float transitionDuration = 1.2f;

    [Tooltip("Easing curve for the blend. X = normalised time (0–1), Y = blend value (0–1). " +
             "Default S-curve gives a smooth ease-in + ease-out feel.")]
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Starting State")]
    [Tooltip("Which feature is active at startup. 1 = RF1 (e.g. B&W), 2 = RF2 (e.g. Vibrant).")]
    public int startingFeature = 1;

    // The name of the float property on both materials that controls effect strength.
    private const string IntensityProperty = "_Intensity";

    // Which feature is currently the active (fully visible) one: 1 or 2.
    private int   _activeFeature;
    private bool  _isTransitioning = false;

    // ── Unity callbacks ──────────────────────────────────────────────────────
    private void Start()
    {
        _activeFeature = startingFeature;

        // Initialise materials to match the starting state
        SetIntensity(rf1Material, _activeFeature == 1 ? 1f : 0f);
        SetIntensity(rf2Material, _activeFeature == 2 ? 1f : 0f);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Transition to feature 1 or feature 2.
    /// Safe to call while a transition is already running — it will cancel the
    /// current one and start the new transition from wherever the blend currently is.
    /// </summary>
    public void TransitionTo(int featureIndex)
    {
        if (featureIndex == _activeFeature && !_isTransitioning) return;

        if (_isTransitioning)
            StopAllCoroutines();

        _activeFeature = featureIndex;
        StartCoroutine(DoTransition(featureIndex));
    }

    /// <summary>Convenience shortcut — toggle between RF1 and RF2.</summary>
    public void Toggle()
    {
        TransitionTo(_activeFeature == 1 ? 2 : 1);
    }

    // ── Transition coroutine ─────────────────────────────────────────────────
    private IEnumerator DoTransition(int targetFeature)
    {
        _isTransitioning = true;

        // Read the current intensities so we can start from wherever we are
        // (important if the user interrupts a mid-way transition).
        float startRF1 = GetIntensity(rf1Material);
        float startRF2 = GetIntensity(rf2Material);

        float targetRF1 = targetFeature == 1 ? 1f : 0f;
        float targetRF2 = targetFeature == 2 ? 1f : 0f;

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t       = Mathf.Clamp01(elapsed / transitionDuration);
            float eased   = transitionCurve.Evaluate(t);

            SetIntensity(rf1Material, Mathf.Lerp(startRF1, targetRF1, eased));
            SetIntensity(rf2Material, Mathf.Lerp(startRF2, targetRF2, eased));

            yield return null;
        }

        // Snap to exact target values to avoid floating-point drift
        SetIntensity(rf1Material, targetRF1);
        SetIntensity(rf2Material, targetRF2);

        _isTransitioning = false;
    }

    // ── Material helpers ─────────────────────────────────────────────────────
    private static void SetIntensity(Material mat, float value)
    {
        if (mat != null && mat.HasProperty(IntensityProperty))
            mat.SetFloat(IntensityProperty, value);
    }

    private static float GetIntensity(Material mat)
    {
        if (mat != null && mat.HasProperty(IntensityProperty))
            return mat.GetFloat(IntensityProperty);
        return 0f;
    }

    // ── Editor helper ────────────────────────────────────────────────────────
    // Resets intensities to their authored values when you exit Play Mode,
    // so the materials aren't left in a mid-transition state in the Project.
    private void OnApplicationQuit()
    {
        SetIntensity(rf1Material, startingFeature == 1 ? 1f : 0f);
        SetIntensity(rf2Material, startingFeature == 2 ? 1f : 0f);
    }
}
