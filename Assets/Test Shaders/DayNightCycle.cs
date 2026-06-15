using UnityEngine;

/// <summary>
/// Simulates a full looping day/night cycle over a configurable duration.
/// The sun travels a complete 360° arc — sunrise → noon → sunset → midnight → sunrise.
/// All sky, cloud, fog, and light colors transition smoothly through every phase.
///
/// HOW TO SET UP:
///   1. Use the modified WorldSky.shader (includes _UseSunOverride / _SunDirOverride).
///   2. Add this component to any GameObject in the scene.
///   3. Assign your Directional Light to "Sun Light".
///   4. Assign the skybox Material (WorldSky.shader) to "Sky Material".
///   5. Hit Play. The cycle completes once every cycleDurationMinutes and loops.
///
/// DIRECTIONAL LIGHT SETUP:
///   Set the light's Y rotation to taste (e.g. 170° for a south-facing arc).
///   The script only modifies X rotation, leaving Y and Z alone.
///
/// CYCLE PHASES (default cycleStartAngle = -10):
///   t = 0.00  →  Sunrise      (sun just above east horizon)
///   t = 0.25  →  Noon         (sun directly overhead)
///   t = 0.50  →  Sunset       (sun just below west horizon)
///   t = 0.75  →  Midnight     (sun at nadir)
///   t = 1.00  →  Sunrise      (loops back seamlessly)
/// </summary>
[ExecuteAlways]
public class DayNightCycle : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Cycle Settings")]
    [Tooltip("How long one full day/night loop takes, in minutes.")]
    public float cycleDurationMinutes = 20f;

    [Tooltip("Loop continuously. When off, the cycle stops at the end of the first day.")]
    public bool loop = true;

    [Tooltip("X-rotation of the directional light at t=0 (dawn).\n"
           + "Default -10 puts the sun just above the horizon at start.")]
    public float cycleStartAngle = -10f;

    [Header("References")]
    public Light    sunLight;
    public Material skyMaterial;

    // ── Sky: Day ─────────────────────────────────────────────────────────────
    [Header("Sky — Day Colors")]
    public Color dayZenith   = new Color(0.08f, 0.30f, 0.82f);
    public Color dayMid      = new Color(0.22f, 0.60f, 0.96f);
    public Color dayHorizon  = new Color(0.58f, 0.86f, 1.00f);
    public Color dayLow      = new Color(0.80f, 0.96f, 1.00f);

    // ── Sky: Sunrise / Sunset ─────────────────────────────────────────────────
    [Header("Sky — Sunrise / Sunset Colors")]
    public Color duskZenith  = new Color(0.12f, 0.06f, 0.28f);
    public Color duskMid     = new Color(0.65f, 0.22f, 0.08f);
    public Color duskHorizon = new Color(1.00f, 0.48f, 0.08f);
    public Color duskLow     = new Color(1.00f, 0.62f, 0.25f);

    // ── Sky: Night ────────────────────────────────────────────────────────────
    [Header("Sky — Night Colors")]
    public Color nightZenith  = new Color(0.00f, 0.01f, 0.06f);
    public Color nightMid     = new Color(0.01f, 0.02f, 0.10f);
    public Color nightHorizon = new Color(0.02f, 0.04f, 0.14f);
    public Color nightLow     = new Color(0.01f, 0.02f, 0.08f);

    // ── Clouds ────────────────────────────────────────────────────────────────
    [Header("Cloud Colors — Day")]
    public Color dayCloudLight  = new Color(1.00f, 1.00f, 1.00f);
    public Color dayCloudMid    = new Color(0.85f, 0.93f, 1.00f);
    public Color dayCloudShadow = new Color(0.52f, 0.68f, 0.88f);

    [Header("Cloud Colors — Night")]
    public Color nightCloudLight  = new Color(0.08f, 0.10f, 0.18f);
    public Color nightCloudMid    = new Color(0.05f, 0.06f, 0.14f);
    public Color nightCloudShadow = new Color(0.02f, 0.03f, 0.09f);

    // ── Fog ───────────────────────────────────────────────────────────────────
    [Header("Fog Colors")]
    public Color dayFogColor   = new Color(0.78f, 0.92f, 1.00f);
    public Color duskFogColor  = new Color(0.80f, 0.38f, 0.12f);
    public Color nightFogColor = new Color(0.02f, 0.04f, 0.12f);

    // ── Sun disc ──────────────────────────────────────────────────────────────
    [Header("Sun Disc Colors")]
    public Color dawnSunColor = new Color(1.00f, 0.55f, 0.20f);
    public Color noonSunColor = new Color(1.00f, 0.98f, 0.90f);
    public Color duskSunColor = new Color(1.00f, 0.30f, 0.05f);

    // ── Directional light ─────────────────────────────────────────────────────
    [Header("Directional Light")]
    [Tooltip("Peak intensity at noon.")]
    public float maxLightIntensity = 1.2f;
    public Color dawnLightColor = new Color(1.00f, 0.60f, 0.30f);
    public Color noonLightColor = new Color(1.00f, 0.96f, 0.88f);
    public Color duskLightColor = new Color(1.00f, 0.45f, 0.15f);

    // ─────────────────────────────────────────────────────────────────────────
    // Private state
    // ─────────────────────────────────────────────────────────────────────────

    float _elapsed;
    float _normalizedTime;

    // Cached Y and Z from the light's initial rotation in the Editor.
    // These never change — only X rotates during the cycle.
    // Caching them avoids gimbal-lock flicker caused by reading localEulerAngles
    // back from Unity after it has internally decomposed the quaternion.
    float _fixedLightY;
    float _fixedLightZ;
    bool  _eulersCached;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity callbacks
    // ─────────────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        CacheLightEulers();

        if (skyMaterial != null)
            skyMaterial.SetFloat("_UseSunOverride", 1f);
    }

    void OnDisable()
    {
        if (skyMaterial != null)
            skyMaterial.SetFloat("_UseSunOverride", 0f);
    }

    void Update()
    {
        // Safety: re-cache if the reference changed at runtime
        if (!_eulersCached)
            CacheLightEulers();

        float cycleSecs = cycleDurationMinutes * 60f;

        if (Application.isPlaying)
        {
            _elapsed += Time.deltaTime;

            if (loop)
                _elapsed = Mathf.Repeat(_elapsed, cycleSecs);
            else
                _elapsed = Mathf.Min(_elapsed, cycleSecs);
        }

        float arcT      = Mathf.Repeat(_elapsed / cycleSecs, 1f);
        _normalizedTime = loop ? arcT : Mathf.Clamp01(_elapsed / cycleSecs);

        RotateSun(arcT);
        UpdateSkyMaterial();
        UpdateLightAppearance();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cache the Y and Z euler angles set by the artist in the Editor.
    // We read them once here and never read localEulerAngles again in Update,
    // which eliminates gimbal-lock / quaternion-decomposition flicker.
    // ─────────────────────────────────────────────────────────────────────────

    void CacheLightEulers()
    {
        if (sunLight == null) return;
        Vector3 e    = sunLight.transform.localEulerAngles;
        _fixedLightY = e.y;
        _fixedLightZ = e.z;
        _eulersCached = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sun rotation
    // ─────────────────────────────────────────────────────────────────────────

    void RotateSun(float arcT)
    {
        if (sunLight == null) return;

        float xAngle = Mathf.Repeat(arcT * 360f + cycleStartAngle, 360f);

        // Always reconstruct from the cached Y/Z — never read back from the transform.
        // This is the key fix: reading localEulerAngles after Unity has recomposed
        // the quaternion can return flipped Y/Z values near 90°/270° X, causing
        // the light direction to jump and the scene to flash.
        sunLight.transform.localEulerAngles = new Vector3(xAngle, _fixedLightY, _fixedLightZ);

        if (skyMaterial != null)
        {
            Vector3 towardSun = -sunLight.transform.forward;
            skyMaterial.SetVector("_SunDirOverride",
                new Vector4(towardSun.x, towardSun.y, towardSun.z, 0f));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sky material
    // ─────────────────────────────────────────────────────────────────────────

    void UpdateSkyMaterial()
    {
        if (skyMaterial == null || sunLight == null) return;

        float elevation = (-sunLight.transform.forward).y;

        // ── Sky gradient ──────────────────────────────────────────────────────
        Color zenith, mid, horizon, low;

        if (elevation >= 0.25f)
        {
            zenith  = dayZenith;
            mid     = dayMid;
            horizon = dayHorizon;
            low     = dayLow;
        }
        else if (elevation >= -0.15f)
        {
            float bandT   = (elevation + 0.15f) / 0.40f;
            float duskPeak = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(1f - Mathf.Abs(bandT - 0.375f) / 0.375f));

            Color baseZenith  = Color.Lerp(nightZenith,  dayZenith,  bandT);
            Color baseMid     = Color.Lerp(nightMid,     dayMid,     bandT);
            Color baseHorizon = Color.Lerp(nightHorizon, dayHorizon, bandT);
            Color baseLow     = Color.Lerp(nightLow,     dayLow,     bandT);

            zenith  = Color.Lerp(baseZenith,  duskZenith,  duskPeak);
            mid     = Color.Lerp(baseMid,     duskMid,     duskPeak * 0.9f);
            horizon = Color.Lerp(baseHorizon, duskHorizon, duskPeak);
            low     = Color.Lerp(baseLow,     duskLow,     duskPeak * 0.8f);
        }
        else
        {
            zenith  = nightZenith;
            mid     = nightMid;
            horizon = nightHorizon;
            low     = nightLow;
        }

        skyMaterial.SetColor("_SkyColorZenith",  zenith);
        skyMaterial.SetColor("_SkyColorMid",     mid);
        skyMaterial.SetColor("_SkyColorHorizon", horizon);
        skyMaterial.SetColor("_SkyColorLow",     low);

        // ── Clouds ────────────────────────────────────────────────────────────
        float cloudT = 1f - Mathf.Clamp01((elevation + 0.15f) / 0.40f);
        skyMaterial.SetColor("_CloudColorLight",  Color.Lerp(dayCloudLight,  nightCloudLight,  cloudT));
        skyMaterial.SetColor("_CloudColorMid",    Color.Lerp(dayCloudMid,    nightCloudMid,    cloudT));
        skyMaterial.SetColor("_CloudColorShadow", Color.Lerp(dayCloudShadow, nightCloudShadow, cloudT));

        // ── Fog ───────────────────────────────────────────────────────────────
        Color fogCol;
        if (elevation >= 0.25f)
        {
            fogCol = dayFogColor;
        }
        else if (elevation >= -0.15f)
        {
            float bandT   = (elevation + 0.15f) / 0.40f;
            float duskPeak = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(1f - Mathf.Abs(bandT - 0.375f) / 0.375f));
            Color baseCol = Color.Lerp(nightFogColor, dayFogColor, bandT);
            fogCol = Color.Lerp(baseCol, duskFogColor, duskPeak);
        }
        else
        {
            fogCol = nightFogColor;
        }
        skyMaterial.SetColor("_FogColor", fogCol);

        // ── Sun disc color ────────────────────────────────────────────────────
        Color sunCol;
        if (elevation >= 0.40f)
        {
            sunCol = noonSunColor;
        }
        else if (elevation >= 0.05f)
        {
            sunCol = Color.Lerp(dawnSunColor, noonSunColor, (elevation - 0.05f) / 0.35f);
        }
        else if (elevation >= -0.05f)
        {
            sunCol = duskSunColor;
        }
        else if (elevation >= -0.20f)
        {
            sunCol = Color.Lerp(Color.black, duskSunColor, (elevation + 0.20f) / 0.15f);
        }
        else
        {
            sunCol = Color.black;
        }
        skyMaterial.SetColor("_SunColor", sunCol);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Directional light appearance
    // ─────────────────────────────────────────────────────────────────────────

    void UpdateLightAppearance()
    {
        if (sunLight == null) return;

        float elevation = (-sunLight.transform.forward).y;

        float intensity = Mathf.Clamp01((elevation + 0.05f) / 0.35f);
        sunLight.intensity = intensity * maxLightIntensity;

        Color lightColor;
        if (elevation >= 0.35f)
        {
            lightColor = noonLightColor;
        }
        else if (elevation >= 0.05f)
        {
            lightColor = Color.Lerp(dawnLightColor, noonLightColor, (elevation - 0.05f) / 0.30f);
        }
        else if (elevation >= 0f)
        {
            lightColor = Color.Lerp(duskLightColor, dawnLightColor, elevation / 0.05f);
        }
        else
        {
            lightColor = Color.Lerp(duskLightColor, Color.black, Mathf.Clamp01(-elevation / 0.1f));
        }
        sunLight.color = lightColor;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Restart the cycle from dawn.</summary>
    public void ResetToMorning()
    {
        _elapsed        = 0f;
        _normalizedTime = 0f;
    }

    /// <summary>
    /// Jump to any point in the cycle.
    /// 0 = dawn, 0.25 = noon, 0.5 = dusk, 0.75 = midnight.
    /// </summary>
    public void SetNormalizedTime(float t)
    {
        _normalizedTime = Mathf.Repeat(t, 1f);
        _elapsed        = _normalizedTime * cycleDurationMinutes * 60f;
    }

    /// <summary>Current 0–1 progress through the cycle.</summary>
    public float NormalizedTime => _normalizedTime;

    /// <summary>Elapsed seconds within the current cycle.</summary>
    public float ElapsedSeconds => _elapsed;

    /// <summary>
    /// Current sun elevation.
    /// +1 = overhead (noon), 0 = horizon, -1 = nadir (midnight).
    /// </summary>
    public float SunElevation =>
        sunLight != null ? (-sunLight.transform.forward).y : 0f;
}