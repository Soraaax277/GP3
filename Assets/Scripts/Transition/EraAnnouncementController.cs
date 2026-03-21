using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;

// Controls the fullscreen era transition announcement.
// Uses EraBokehPanel shader on the EraPanel Image to blur + darken + bokeh
// everything underneath — works in Screen Space Overlay.
//
// HIERARCHY SETUP:
//   [EraAnnouncementController GO]   ← this script
//   [Canvas - Screen Space Overlay, sort order 999]
//     └── EraPanel (Image)           ← assign eraPanelImage + eraPanelCanvasGroup
//           ├── EraLabel             ← assign eraLabel  (TMP)
//           └── FlavorLabel          ← assign flavorLabel (TMP)
//
// PANEL SETUP:
//   - EraPanel Image: full stretch, material = Mat_EraBokeh
//   - Mat_EraBokeh uses Custom/URP/EraBokehPanel shader
//   - EraPanel also has a CanvasGroup component
//   - In URP Pipeline Asset: tick "Opaque Texture" (required for _CameraOpaqueTexture)
public class EraAnnouncementController : MonoBehaviour
{
    public static EraAnnouncementController Instance;

    [Header("UI References")]
    [Tooltip("The fullscreen Canvas GO (sort order 999). Starts inactive, activated on Start.")]
    public GameObject      announcementCanvas;
    [Tooltip("CanvasGroup on the EraPanel for overall alpha control.")]
    public CanvasGroup     eraPanelCanvasGroup;
    [Tooltip("The Image component on EraPanel using Mat_EraBokeh material.")]
    public Image           eraPanelImage;
    [Tooltip("Large TMP showing era name.")]
    public TextMeshProUGUI eraLabel;
    [Tooltip("Smaller TMP for flavor text.")]
    public TextMeshProUGUI flavorLabel;

    [Header("HUD Era Text")]
    [Tooltip("GameStatusUI.eraText — gets a color pulse after announcement.")]
    public TextMeshProUGUI hudEraText;

    [Header("Timing")]
    public float blurFadeDuration = 0.7f;
    public float textFadeInTime   = 0.5f;
    public float holdDuration     = 2.5f;
    public float textFadeOutTime  = 0.4f;
    public float panelFadeOutTime = 0.6f;
    public float hudPunchDuration = 0.4f;

    [Header("Bokeh Settings")]
    public float maxBlurSize     = 3f;
    public float maxDarkness     = 0.45f;
    public float maxTintStrength = 0.08f;

    // Shader property IDs — cached for performance
    private static readonly int ID_BlurSize     = Shader.PropertyToID("_BlurSize");
    private static readonly int ID_Darkness     = Shader.PropertyToID("_Darkness");
    private static readonly int ID_TintColor    = Shader.PropertyToID("_TintColor");
    private static readonly int ID_TintStrength = Shader.PropertyToID("_TintStrength");

    private Material _panelMat; // instance material so we don't modify the shared asset
    private bool     _isPlaying = false;

    // ── Per-era data ──────────────────────────────────────────────────────────

    private struct EraData
    {
        public string displayName;
        public string flavorText;
        public Color  accentColor;
    }

    private static readonly EraData[] EraTable = new EraData[]
    {
        new EraData
        {
            displayName = "Industrial Era",
            flavorText  = "The age of steel and steam — build your foundation.",
            accentColor = new Color(0.85f, 0.72f, 0.45f)
        },
        new EraData
        {
            displayName = "Early 80's",
            flavorText  = "Neon lights and early silicon — the digital race begins.",
            accentColor = new Color(0.30f, 0.90f, 1.00f)
        },
        new EraData
        {
            displayName = "Retro Era",
            flavorText  = "Networks grow, screens glow — information is power.",
            accentColor = new Color(0.20f, 0.90f, 0.35f)  // green
        },
        new EraData
        {
            displayName = "Futuristic Era",
            flavorText  = "Beyond the horizon — whoever adapts, dominates.",
            accentColor = new Color(0.15f, 0.50f, 1.00f)  // deep electric blue
        },
    };

    // -------------------------------------------------------------------------

    private void Awake()
    {
        Instance = this;

        if (announcementCanvas != null)
            announcementCanvas.SetActive(false);

        // Create a material instance so we never modify the shared asset
        if (eraPanelImage != null && eraPanelImage.material != null)
        {
            _panelMat = new Material(eraPanelImage.material);
            eraPanelImage.material = _panelMat;
        }

        // Start with zero blur/darkness
        SetPanelValues(0f, 0f, Color.black, 0f);

        if (eraPanelCanvasGroup != null)
        {
            eraPanelCanvasGroup.alpha          = 0f;
            eraPanelCanvasGroup.blocksRaycasts = false;
            eraPanelCanvasGroup.interactable   = false;
        }

        if (eraLabel    != null) eraLabel.alpha    = 0f;
        if (flavorLabel != null) flavorLabel.alpha = 0f;
    }

    private void Start()
    {
        if (announcementCanvas != null)
            announcementCanvas.SetActive(true);

        TriggerAnnouncement(TurnManager.GameEra.Industrial);
    }

    // -------------------------------------------------------------------------

    public void TriggerAnnouncement(TurnManager.GameEra era)
    {
        if (_isPlaying) return;
        StartCoroutine(PlayAnnouncement(era));
    }

    // -------------------------------------------------------------------------

    // Same as TriggerAnnouncement but stops any in-progress sequence first.
    // Called by DebugCheatManager so the cheat always shows the announcement
    // even if one is already playing.
    public void ForceTriggerAnnouncement(TurnManager.GameEra era)
    {
        StopAllCoroutines();

        // Reset all visual state instantly so the new sequence starts clean
        SetPanelValues(0f, 0f, Color.black, 0f);

        if (eraPanelCanvasGroup != null)
        {
            eraPanelCanvasGroup.alpha          = 0f;
            eraPanelCanvasGroup.blocksRaycasts = false;
            eraPanelCanvasGroup.interactable   = false;
        }

        if (eraLabel    != null) { eraLabel.alpha    = 0f; }
        if (flavorLabel != null) { flavorLabel.alpha = 0f; }

        _isPlaying = false;

        StartCoroutine(PlayAnnouncement(era));
    }

    private IEnumerator PlayAnnouncement(TurnManager.GameEra era)
    {
        _isPlaying = true;

        if (announcementCanvas != null)
            announcementCanvas.SetActive(true);

        EraData data = EraTable[Mathf.Clamp((int)era, 0, EraTable.Length - 1)];

        // Set text
        if (eraLabel != null)
        {
            eraLabel.text  = data.displayName;
            eraLabel.color = new Color(data.accentColor.r, data.accentColor.g, data.accentColor.b, 0f);
        }
        if (flavorLabel != null)
        {
            flavorLabel.text  = data.flavorText;
            flavorLabel.color = new Color(1f, 1f, 1f, 0f);
        }

        // Unblock panel
        if (eraPanelCanvasGroup != null)
        {
            eraPanelCanvasGroup.blocksRaycasts = true;
            eraPanelCanvasGroup.interactable   = true;
        }

        // ── Fade in bokeh + darkness ──────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < blurFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / blurFadeDuration);
            if (eraPanelCanvasGroup != null) eraPanelCanvasGroup.alpha = t;
            SetPanelValues(
                Mathf.Lerp(0f, maxBlurSize,     t),
                Mathf.Lerp(0f, maxDarkness,     t),
                data.accentColor,
                Mathf.Lerp(0f, maxTintStrength, t));
            yield return null;
        }
        if (eraPanelCanvasGroup != null) eraPanelCanvasGroup.alpha = 1f;
        SetPanelValues(maxBlurSize, maxDarkness, data.accentColor, maxTintStrength);

        // ── Fade in text ──────────────────────────────────────────────────────
        if (eraLabel != null)
        {
            eraLabel.DOFade(1f, textFadeInTime).SetEase(Ease.OutCubic);
            eraLabel.transform.DOScale(Vector3.one, textFadeInTime)
                .From(Vector3.one * 0.88f).SetEase(Ease.OutBack);
        }
        if (flavorLabel != null)
            flavorLabel.DOFade(1f, textFadeInTime).SetDelay(0.15f).SetEase(Ease.OutCubic);

        yield return new WaitForSeconds(textFadeInTime + holdDuration);

        // ── Fade out text ─────────────────────────────────────────────────────
        if (eraLabel    != null) eraLabel.DOFade(0f,    textFadeOutTime).SetEase(Ease.InCubic);
        if (flavorLabel != null) flavorLabel.DOFade(0f, textFadeOutTime).SetEase(Ease.InCubic);

        yield return new WaitForSeconds(textFadeOutTime);

        // ── Fade out bokeh ────────────────────────────────────────────────────
        elapsed = 0f;
        while (elapsed < panelFadeOutTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / panelFadeOutTime);
            if (eraPanelCanvasGroup != null) eraPanelCanvasGroup.alpha = 1f - t;
            SetPanelValues(
                Mathf.Lerp(maxBlurSize,     0f, t),
                Mathf.Lerp(maxDarkness,     0f, t),
                data.accentColor,
                Mathf.Lerp(maxTintStrength, 0f, t));
            yield return null;
        }

        SetPanelValues(0f, 0f, Color.black, 0f);
        if (eraPanelCanvasGroup != null)
        {
            eraPanelCanvasGroup.alpha          = 0f;
            eraPanelCanvasGroup.blocksRaycasts = false;
            eraPanelCanvasGroup.interactable   = false;
        }

        if (announcementCanvas != null)
            announcementCanvas.SetActive(false);

        // ── Pulse HUD era text ────────────────────────────────────────────────
        if (hudEraText != null)
        {
            Color orig = hudEraText.color;
            hudEraText.DOColor(data.accentColor, hudPunchDuration * 0.5f)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => hudEraText.DOColor(orig, hudPunchDuration * 0.5f).SetEase(Ease.InCubic));
            hudEraText.transform.DOPunchScale(Vector3.one * 0.15f, hudPunchDuration, 2, 0.5f);
        }

        _isPlaying = false;
    }

    // ── Sets all four shader properties at once ───────────────────────────────
    private void SetPanelValues(float blurSize, float darkness, Color tint, float tintStrength)
    {
        if (_panelMat == null) return;
        _panelMat.SetFloat(ID_BlurSize,     blurSize);
        _panelMat.SetFloat(ID_Darkness,     darkness);
        _panelMat.SetColor(ID_TintColor,    tint);
        _panelMat.SetFloat(ID_TintStrength, tintStrength);
    }
}