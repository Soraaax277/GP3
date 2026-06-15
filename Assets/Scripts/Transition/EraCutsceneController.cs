using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;

// Lives inside EraCutsceneScene (loaded additively by EraAnnouncementController).
//
// CAMERA SWAP CONTRACT:
//   EraAnnouncementController owns the camera swap — it activates the era camera
//   at peak glitch and calls SignalCameraReady() to start this controller running.
//   In standalone/debug mode this script handles its own camera activation.
//
// HIERARCHY SETUP (EraCutsceneScene):
//   [EraCutsceneController GO]   ← this script
//   [Canvas - Screen Space Overlay, sort order 999]
//     ├── EraLabel   (TMP)       ← assign eraLabel
//     └── FlavorLabel (TMP)      ← assign flavorLabel
//   All 4 era objects + cameras live here, inactive by default.
public class EraCutsceneController : MonoBehaviour
{
    public static EraCutsceneController Instance;

    // ── Per-label style  /  Per-era slot ─────────────────────────────────────

    [System.Serializable]
    public class LabelStyle
    {
        [Tooltip("Anchored Y position of the label's RectTransform.")]
        public float posY      = 0f;
        [Tooltip("Font size in points.")]
        public float fontSize  = 36f;
        [Tooltip("Leave null to keep the label's default font asset.")]
        public TMP_FontAsset fontAsset = null;
        [Tooltip("Base font color. Alpha is overridden during fade-in/out — set it to any non-zero value.")]
        public Color fontColor = Color.white;
    }

    [System.Serializable]
    public class EraSlot
    {
        [Header("Identity")]
        public string displayName = "Era Name";
        [TextArea] public string flavorText = "Flavor text here.";
        public Color accentColor = Color.white;

        [Header("Era Label Style")]
        public LabelStyle eraLabelStyle    = new LabelStyle();

        [Header("Flavor Label Style")]
        public LabelStyle flavorLabelStyle = new LabelStyle();

        [Header("Object")]
        [Tooltip("The hero prop for this era. Active/inactive handled automatically.")]
        public GameObject eraObject;
        public Vector3    objectStartPos;
        public Vector3    objectStartRot;
        public Vector3    objectEndPos;
        public Vector3    objectEndRot;
        [Tooltip("Seconds after cutscene starts before this object begins moving.")]
        public float      objectStartDelay = 0f;
        [Tooltip("How long the object takes to travel from start to end.")]
        public float      objectDuration   = 1.5f;

        [Header("Camera")]
        [Tooltip("Dedicated camera for this era. Leave inactive in scene — activated at peak glitch by EraAnnouncementController.")]
        public Camera  eraCamera;
        public Vector3 cameraStartPos;
        public Vector3 cameraStartRot;
        public Vector3 cameraEndPos;
        public Vector3 cameraEndRot;
        [Tooltip("Seconds after cutscene starts before this camera begins moving.")]
        public float   cameraStartDelay = 0f;
        [Tooltip("How long the camera takes to travel from start to end.")]
        public float   cameraDuration   = 1.5f;

        [Header("Mid-Cutscene Text")]
        [Tooltip("When ON: text appears mid-cutscene at textMidAppearDelay seconds. When OFF: text appears after all animations finish.")]
        public bool    showTextMidCutscene = false;
        [Tooltip("Seconds from cutscene start before text fades in. Only used when showTextMidCutscene is ON.")]
        public float   textMidAppearDelay  = 1.0f;
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Era Slots  (index matches TurnManager.GameEra)")]
    [Tooltip("0 = Industrial  |  1 = EarlyEighties  |  2 = Retro  |  3 = Futuristic")]
    public EraSlot[] eraSlots = new EraSlot[4];

    [Header("UI References")]
    public TextMeshProUGUI eraLabel;
    public TextMeshProUGUI flavorLabel;
    [Tooltip("Optional canvas whose render camera is replaced by the active era's camera. " +
             "Set the canvas to Screen Space - Camera mode for this to take effect.")]
    public Canvas eraCanvas;

    [Header("Text Timing")]
    public float textFadeInTime  = 0.5f;
    public float holdDuration    = 2.5f;
    public float textFadeOutTime = 0.4f;

    [Header("Camera Wobble")]
    [Tooltip("How much the camera drifts positionally. Keep very small (e.g. 0.03).")]
    public float wobblePositionAmount = 0.03f;
    [Tooltip("How much the camera tilts in degrees. Keep subtle (e.g. 0.2).")]
    public float wobbleRotationAmount = 0.2f;
    [Tooltip("How fast the wobble cycles. Lower = slower, lazier feel.")]
    public float wobbleSpeed          = 0.8f;

    [Header("Debug")]
    [Tooltip("When ON: cutscene plays but never signals completion — scene stays open for hierarchy inspection.")]
    public bool debugHold = false;
    [Tooltip("Standalone era index when running without main scene. 0=Industrial 1=EarlyEighties 2=Retro 3=Futuristic")]
    [Range(0, 3)]
    public int debugStandaloneEra = 0;

    // ── Internal state ────────────────────────────────────────────────────────

    private bool    _cameraComplete  = false;
    private bool    _objectComplete  = false;
    private bool    _cameraReady     = false; // set by SignalCameraReady() or standalone mode
    private int     _currentEraIndex = 0;
    private Vector3 _cameraBasePos   = Vector3.zero;
    private Vector3 _cameraBaseRot   = Vector3.zero;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;

        // FIX: Resolve the era index and initialize cameras here in Awake, NOT in
        // Start/RunCutscene. EraAnnouncementController calls GetEraCamera() and
        // SetActive(true) on the camera in the same frame that LoadSceneAsync
        // completes — which is before Start() has had a chance to run. If we left
        // the "SetActive(false) all cameras" loop in RunCutscene (called from
        // Start), it would fire one frame later and silently undo that activation.
        bool isStandalone = EraAnnouncementController.Instance == null;

        _currentEraIndex = isStandalone
            ? Mathf.Clamp(debugStandaloneEra, 0, eraSlots.Length - 1)
            : Mathf.Clamp((int)EraAnnouncementController.Instance.currentEra, 0, eraSlots.Length - 1);

        // Deactivate every era camera and snap the correct one to its start
        // transform while still inactive (transform writes work on inactive GOs).
        for (int i = 0; i < eraSlots.Length; i++)
        {
            if (eraSlots[i].eraCamera != null)
                eraSlots[i].eraCamera.gameObject.SetActive(false);
        }

        EraSlot slot = eraSlots[_currentEraIndex];
        if (slot.eraCamera != null)
        {
            slot.eraCamera.transform.position    = slot.cameraStartPos;
            slot.eraCamera.transform.eulerAngles = slot.cameraStartRot;
        }

        // Standalone: self-activate camera immediately so the scene is renderable.
        if (isStandalone && slot.eraCamera != null)
        {
            slot.eraCamera.gameObject.SetActive(true);
            _cameraReady = true;
            SyncCanvasCamera();
        }
    }

    private void Start()
    {
        StartCoroutine(RunCutscene());
    }

    // ── Public API (called by EraAnnouncementController) ─────────────────────

    // Returns the era camera for the given era index so EraAnnouncementController
    // can activate it at peak glitch. Safe to call right after scene load because
    // Awake() has already run and set up the slots.
    public Camera GetEraCamera(int eraIndex)
    {
        if (eraIndex < 0 || eraIndex >= eraSlots.Length) return null;
        return eraSlots[eraIndex].eraCamera;
    }

    // Called by EraAnnouncementController after it activates the era camera.
    // This unblocks RunCutscene so movement and wobble begin.
    public void SignalCameraReady()
    {
        _cameraReady = true;
        SyncCanvasCamera();
    }

    // ── Main sequence ─────────────────────────────────────────────────────────

    private IEnumerator RunCutscene()
    {
        bool isStandalone = EraAnnouncementController.Instance == null;

        // Era index and camera init were already handled in Awake().
        // Just grab the slot and proceed.
        EraSlot slot = eraSlots[_currentEraIndex];

        if (isStandalone)
            Debug.LogWarning("[EraCutsceneController] Standalone/debug mode. Era = " + _currentEraIndex);

        // ── Activate era objects — cameras handled in Awake + EraAnnouncementController ──
        for (int i = 0; i < eraSlots.Length; i++)
        {
            if (eraSlots[i].eraObject != null)
                eraSlots[i].eraObject.SetActive(i == _currentEraIndex);
        }

        // ── Prepare text (invisible) ──────────────────────────────────────────
        if (eraLabel != null)
        {
            eraLabel.text  = slot.displayName;
            ApplyLabelStyle(eraLabel, slot.eraLabelStyle);
        }
        if (flavorLabel != null)
        {
            flavorLabel.text  = slot.flavorText;
            ApplyLabelStyle(flavorLabel, slot.flavorLabelStyle);
        }

        // ── Wait for EraAnnouncementController to activate the camera ─────────
        // (In standalone mode _cameraReady was already set true in Awake.)
        yield return new WaitUntil(() => _cameraReady);

        // ── Set up base positions and launch independent wobble + movement ─────
        _cameraComplete = false;
        _objectComplete = false;
        _cameraBasePos  = slot.cameraStartPos;
        _cameraBaseRot  = slot.cameraStartRot;

        Transform camTransform = slot.eraCamera != null ? slot.eraCamera.transform : null;

        float totalDuration = Mathf.Max(slot.cameraStartDelay + slot.cameraDuration,
                                        slot.objectStartDelay  + slot.objectDuration)
                            + textFadeInTime + holdDuration + textFadeOutTime;

        StartCoroutine(WobbleLoop(camTransform, totalDuration));

        StartCoroutine(MoveCameraBase(
            slot.cameraStartPos, slot.cameraStartRot,
            slot.cameraEndPos,   slot.cameraEndRot,
            slot.cameraStartDelay, slot.cameraDuration,
            () => _cameraComplete = true));

        StartCoroutine(MoveTransform(
            slot.eraObject != null ? slot.eraObject.transform : null,
            slot.objectStartPos, slot.objectStartRot,
            slot.objectEndPos,   slot.objectEndRot,
            slot.objectStartDelay, slot.objectDuration,
            () => _objectComplete = true));

        // ── Text logic ────────────────────────────────────────────────────────
        if (slot.showTextMidCutscene)
        {
            StartCoroutine(FadeInTextAfterDelay(slot.textMidAppearDelay, slot));
            yield return new WaitUntil(() => _cameraComplete && _objectComplete);
            if (eraLabel    != null) eraLabel.DOFade(0f,    textFadeOutTime).SetEase(Ease.InCubic);
            if (flavorLabel != null) flavorLabel.DOFade(0f, textFadeOutTime).SetEase(Ease.InCubic);
            yield return new WaitForSeconds(textFadeOutTime);
        }
        else
        {
            yield return new WaitUntil(() => _cameraComplete && _objectComplete);
            if (eraLabel != null)
            {
                eraLabel.DOFade(1f, textFadeInTime).SetEase(Ease.OutCubic);
                eraLabel.transform.DOScale(Vector3.one, textFadeInTime)
                    .From(Vector3.one * 0.88f).SetEase(Ease.OutBack);
            }
            if (flavorLabel != null)
                flavorLabel.DOFade(1f, textFadeInTime).SetDelay(0.15f).SetEase(Ease.OutCubic);
            yield return new WaitForSeconds(textFadeInTime + holdDuration);
            if (eraLabel    != null) eraLabel.DOFade(0f,    textFadeOutTime).SetEase(Ease.InCubic);
            if (flavorLabel != null) flavorLabel.DOFade(0f, textFadeOutTime).SetEase(Ease.InCubic);
            yield return new WaitForSeconds(textFadeOutTime);
        }

        // ── Signal completion ─────────────────────────────────────────────────
        if (debugHold)
            Debug.LogWarning("[EraCutsceneController] debugHold ON — scene will not unload.");
        else if (!isStandalone)
            EraAnnouncementController.Instance.OnCutsceneComplete();
    }

    // ── Mid-cutscene text ─────────────────────────────────────────────────────

    private IEnumerator FadeInTextAfterDelay(float delay, EraSlot slot)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (eraLabel != null)
        {
            ApplyLabelStyle(eraLabel, slot.eraLabelStyle);
            eraLabel.DOFade(1f, textFadeInTime).SetEase(Ease.OutCubic);
            eraLabel.transform.DOScale(Vector3.one, textFadeInTime)
                .From(Vector3.one * 0.88f).SetEase(Ease.OutBack);
        }
        if (flavorLabel != null)
        {
            ApplyLabelStyle(flavorLabel, slot.flavorLabelStyle);
            flavorLabel.DOFade(1f, textFadeInTime).SetDelay(0.15f).SetEase(Ease.OutCubic);
        }
    }

    // ── Wobble loop — independent, runs entire cutscene ───────────────────────

    private IEnumerator WobbleLoop(Transform t, float duration)
    {
        if (t == null) yield break;
        float elapsed = 0f, wobbleTime = 0f;
        while (elapsed < duration)
        {
            elapsed    += Time.deltaTime;
            wobbleTime += Time.deltaTime;
            t.position    = _cameraBasePos + WobblePos(wobbleTime);
            t.eulerAngles = _cameraBaseRot + WobbleRot(wobbleTime);
            yield return null;
        }
    }

    // ── Camera base movement ──────────────────────────────────────────────────

    private IEnumerator MoveCameraBase(
        Vector3 startPos, Vector3 startRot,
        Vector3 endPos,   Vector3 endRot,
        float   delay,    float   duration,
        System.Action onComplete)
    {
        _cameraBasePos = startPos;
        _cameraBaseRot = startRot;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float tt = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            _cameraBasePos = Vector3.Lerp(startPos, endPos, tt);
            _cameraBaseRot = Vector3.Lerp(startRot, endRot, tt);
            yield return null;
        }
        _cameraBasePos = endPos;
        _cameraBaseRot = endRot;
        onComplete?.Invoke();
    }

    // ── Object movement ───────────────────────────────────────────────────────

    private IEnumerator MoveTransform(
        Transform t,
        Vector3 startPos, Vector3 startRot,
        Vector3 endPos,   Vector3 endRot,
        float   delay,    float   duration,
        System.Action onComplete)
    {
        if (t == null) { onComplete?.Invoke(); yield break; }
        t.position    = startPos;
        t.eulerAngles = startRot;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float tt  = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            t.position    = Vector3.Lerp(startPos, endPos, tt);
            t.eulerAngles = Vector3.Lerp(startRot, endRot, tt);
            yield return null;
        }
        t.position    = endPos;
        t.eulerAngles = endRot;
        onComplete?.Invoke();
    }

    // ── Perlin noise helpers ──────────────────────────────────────────────────

    private Vector3 WobblePos(float t)
    {
        float x = (Mathf.PerlinNoise(t * wobbleSpeed,        0.3f) - 0.5f) * 2f * wobblePositionAmount;
        float y = (Mathf.PerlinNoise(0.7f, t * wobbleSpeed        ) - 0.5f) * 2f * wobblePositionAmount;
        float z = (Mathf.PerlinNoise(t * wobbleSpeed * 0.6f, 1.3f) - 0.5f) * 2f * wobblePositionAmount * 0.4f;
        return new Vector3(x, y, z);
    }

    private Vector3 WobbleRot(float t)
    {
        float pitch = (Mathf.PerlinNoise(t * wobbleSpeed + 5f,       0.9f) - 0.5f) * 2f * wobbleRotationAmount;
        float yaw   = (Mathf.PerlinNoise(1.1f, t * wobbleSpeed + 5f       ) - 0.5f) * 2f * wobbleRotationAmount;
        float roll  = (Mathf.PerlinNoise(t * wobbleSpeed * 0.5f + 10f, 2.1f) - 0.5f) * 2f * wobbleRotationAmount * 0.3f;
        return new Vector3(pitch, yaw, roll);
    }

    // ── Label style application ───────────────────────────────────────────────

    private void ApplyLabelStyle(TextMeshProUGUI label, LabelStyle style)
    {
        if (label == null || style == null) return;

        // Anchored Y position
        RectTransform rt = label.rectTransform;
        Vector2 ap = rt.anchoredPosition;
        ap.y = style.posY;
        rt.anchoredPosition = ap;

        // Font size
        label.fontSize = style.fontSize;

        // Font asset (only swap when one is explicitly assigned)
        if (style.fontAsset != null)
            label.font = style.fontAsset;

        // Font color — alpha forced to 0 so DOFade still drives the fade-in
        label.color = new Color(style.fontColor.r, style.fontColor.g, style.fontColor.b, 0f);
    }

    // ── Canvas camera sync ────────────────────────────────────────────────────

    // Assigns the active era's camera as the render camera of eraCanvas.
    // Called once the era camera is confirmed active (SignalCameraReady / standalone Awake).
    // Requires the canvas to be in Screen Space - Camera render mode to have any visual effect.
    private void SyncCanvasCamera()
    {
        if (eraCanvas == null) return;
        EraSlot slot = eraSlots[_currentEraIndex];
        if (slot.eraCamera != null)
            eraCanvas.worldCamera = slot.eraCamera;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ApplyTransform(GameObject go, Vector3 pos, Vector3 rot)
    {
        if (go == null) return;
        go.transform.position    = pos;
        go.transform.eulerAngles = rot;
    }
}