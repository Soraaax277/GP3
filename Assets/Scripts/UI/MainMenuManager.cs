using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

public class MainMenuManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public static MainMenuManager Instance;

    public Button     newGameButton;
    public Button     loadGameButton;
    public Button     settingsButton;
    public Button     exitButton;

    [Header("UI Panels")]
    public GameObject mainContent;
    public SettingsPanel settingsPanel;

    // ── Panel Sway ───────────────────────────────────────────────────────────
    [Header("Panel Sway")]
    public RectTransform swayPanel;

    [Range(0f, 20f)]   public float swayAngle         = 3f;
    [Range(0.5f, 10f)] public float swayDuration       = 3f;
    [Range(0f, 1f)]    public float sideSwayFraction   = 0.5f;
    [Range(0f, 0.1f)]  public float scalePulseAmount   = 0.02f;
    public bool pauseOnHover = true;
    [Range(1f, 20f)]   public float hoverFadeSpeed     = 4f;

    // ── Camera Rotation ──────────────────────────────────────────────────────
    [Header("Camera Rotation")]
    public Transform  menuCamera;
    [Range(1f, 180f)] public float cameraRotateSpeed   = 15f;

    // ── Scene Object Swaps ───────────────────────────────────────────────────
    [Header("Scene Object Swaps")]
    [Tooltip("GO1 and GO3 share slot A — only one is ever active.")]
    public GameObject sceneObject1;
    public GameObject sceneObject3;

    [Tooltip("GO2 and GO4 share slot B — only one is ever active.")]
    public GameObject sceneObject2;
    public GameObject sceneObject4;

    [Header("Radio Signals")]
    [Tooltip("Empty GO parented under sceneObject1 — origin of the pulse rings.")]
    public GameObject radioSignal1;
    [Tooltip("Empty GO parented under sceneObject2 — origin of the pulse rings.")]
    public GameObject radioSignal2;
    [Tooltip("Empty GO parented under sceneObject3 — origin of the pulse rings.")]
    public GameObject radioSignal3;
    [Tooltip("Empty GO parented under sceneObject4 — origin of the pulse rings.")]
    public GameObject radioSignal4;

    [Range(0.5f,  20f)]  public float radioMaxRadius     = 4f;
    [Range(0.5f,  10f)]  public float radioPulseDuration = 2f;
    [Range(0.01f, 0.2f)] public float radioLineWidth     = 0.05f;
    public Color radioColor = Color.white;
    [Range(2, 6)]    public int radioRingCount = 3;
    [Range(16, 128)] public int radioSegments  = 64;

    [Header("Rotation Thresholds")]
    [Tooltip("Silent GO swap for slot A (GO1↔GO3). No glitch, just SetActive.")]
    public float goSwapThreshold1  = 130f;

    [Tooltip("Glitch fires here — filter cycles to next. Must be > goSwapThreshold1.")]
    public float glitchThreshold1  = 135f;

    [Tooltip("Silent GO swap for slot B (GO2↔GO4).")]
    public float goSwapThreshold2  = 330f;

    [Tooltip("Glitch fires here — filter cycles to next. Must be > goSwapThreshold2.")]
    public float glitchThreshold2  = 325f;

    // ── Renderer Features ────────────────────────────────────────────────────
    [Header("Renderer Data")]
    public UniversalRendererData rendererData;

    [Header("Filter Features — assigned in order 1 2 3 4")]
    [Tooltip("Filters cycle in order: 1 → 2 → 3 → 4 → 1 → ...")]
    public ScriptableRendererFeature filterForSlot1;
    public ScriptableRendererFeature filterForSlot2;
    public ScriptableRendererFeature filterForSlot3;
    public ScriptableRendererFeature filterForSlot4;

    // ── Glitch ───────────────────────────────────────────────────────────────
    [Header("Glitch Transition")]
    [Range(0.1f, 2f)] public float glitchDuration = 0.5f;

    // ── Sway private state ───────────────────────────────────────────────────
    private Quaternion _baseRot;
    private Vector3    _baseScale;
    private float      _timeOffset;
    private float      _swayIntensity = 1f;
    private bool       _hovered       = false;

    // ── Rotation tracking ────────────────────────────────────────────────────
    private float _prevCamY;
    private float _totalRotation;

    // Four independent "next fire" accumulators — each advances by 360 when fired
    private float _nextGoSwap1;
    private float _nextGlitch1;
    private float _nextGoSwap2;
    private float _nextGlitch2;

    // ── Scene state ───────────────────────────────────────────────────────────
    // Slot A: false = GO1 active, true = GO3 active
    private bool _slotAShowingGO3 = false;
    // Slot B: false = GO2 active, true = GO4 active
    private bool _slotBShowingGO4 = false;

    // Filter index cycles 0→1→2→3→0 matching filterForSlot1..4
    private int  _activeFilterIndex = 0;

    private bool _glitchPlaying = false;

    // ── Radio signal state ───────────────────────────────────────────────────
    // [signalIndex][ringIndex] — built once in Start, animated every Update
    private LineRenderer[][] _radioRenderers;
    private float            _radioTime;
    // Cycles 0→1→2→3→0 on every glitch fire — which radioSignal GO is currently visible
    private int              _activeSignalIndex = 0;
    private GameObject[]     _radioSignals;

    // Found automatically — no drag needed
    private SignalGlitchFeature _glitch;

    // Convenience array so we can index filters 0..3
    private ScriptableRendererFeature[] _filters;

    // ── Unity callbacks ──────────────────────────────────────────────────────
    private void Awake()
    {
        Instance = this;

        // ── BUILD-SPECIFIC WIPE ─────────────────────────────────────────────
        // Since Application.buildGuid is not available in your Unity version, 
        // I've added a unique BUILD_ID. Whenever you prepare a NEW build 
        // to share, simply changing this ID once will guarantee that every 
        // player starts with a fresh slate, wiping all old local data.
        string BUILD_ID = "2026-03-29-V1"; 
        string buildKey = "CleanSlate_" + BUILD_ID;

        if (PlayerPrefs.GetInt(buildKey, 0) == 0)
        {
            SaveSystem.DeleteSave();
            PlayerPrefs.SetInt(buildKey, 1);
            PlayerPrefs.Save();
            Debug.Log($"[MainMenuManager] Fresh Build Detected ({BUILD_ID}). Legacy saves wiped.");
        }
    }

    private void Start()
    {
        // Hook up buttons in code
        if (newGameButton != null)
        {
            if (newGameButton.gameObject.GetComponent<UIButtonSounds>() == null)
                newGameButton.gameObject.AddComponent<UIButtonSounds>();
            newGameButton.onClick.AddListener(OnNewGame);
            RegisterHighlightHover(newGameButton);
        }

        if (loadGameButton != null)
        {
            if (loadGameButton.gameObject.GetComponent<UIButtonSounds>() == null)
                loadGameButton.gameObject.AddComponent<UIButtonSounds>();
            loadGameButton.onClick.AddListener(OnLoadGame);
            RegisterHighlightHover(loadGameButton);
        }

        if (settingsButton != null)
        {
            if (settingsButton.gameObject.GetComponent<UIButtonSounds>() == null)
                settingsButton.gameObject.AddComponent<UIButtonSounds>();
            settingsButton.onClick.AddListener(OnSettings);
            RegisterHighlightHover(settingsButton);
        }

        if (exitButton != null)
        {
            if (exitButton.gameObject.GetComponent<UIButtonSounds>() == null)
                exitButton.gameObject.AddComponent<UIButtonSounds>();
            exitButton.onClick.AddListener(OnExit);
            RegisterHighlightHover(exitButton);
        }

        if (loadGameButton != null)
        {
            // Instead of just graying it out, HIDE the button if no save exists 
            // to ensure accidental clicks on ghost data are impossible.
            bool hasSave = SaveSystem.HasSaveData();
            loadGameButton.gameObject.SetActive(hasSave);
            loadGameButton.interactable = hasSave;
        }

        if (settingsPanel != null)
        {
            settingsPanel.gameObject.SetActive(false); // Always start hidden — safety net for scene transitions
        }

        if (mainContent != null)
            mainContent.SetActive(true);

        if (swayPanel != null)
        {
            _baseRot    = swayPanel.localRotation;
            _baseScale  = swayPanel.localScale;
            _timeOffset = Random.Range(0f, 100f);
        }

        // Build filter array so we can index it
        _filters = new ScriptableRendererFeature[]
        {
            filterForSlot1,
            filterForSlot2,
            filterForSlot3,
            filterForSlot4
        };

        // Find SignalGlitchFeature automatically
        _glitch = FindFeature<SignalGlitchFeature>();
        if (_glitch == null)
            Debug.LogError("[MainMenuManager] SignalGlitchFeature not found in rendererData.");
        else
            Debug.Log("[MainMenuManager] SignalGlitchFeature found.");

        // ── Initial scene state ───────────────────────────────────────────────
        // GO1 + GO2 visible, GO3 + GO4 hidden
        SetActive(sceneObject1, true);
        SetActive(sceneObject2, true);
        SetActive(sceneObject3, false);
        SetActive(sceneObject4, false);

        // Signal 1 is active by default. Signals 2-4 are inactive.
        // The glitch advances the index each time it fires: 1→2→3→4→1→...
        _radioSignals      = new GameObject[] { radioSignal1, radioSignal2, radioSignal3, radioSignal4 };
        _activeSignalIndex = 0;
        SetActive(radioSignal1, true);
        SetActive(radioSignal2, false);
        SetActive(radioSignal3, false);
        SetActive(radioSignal4, false);

        // Only filter 0 (filterForSlot1) active at start
        for (int i = 0; i < _filters.Length; i++)
            SetFeature(_filters[i], i == 0);
        SetFeature(_glitch, false);
        DirtyRenderer();

        _activeFilterIndex = 0;
        _slotAShowingGO3   = false;
        _slotBShowingGO4   = false;

        // ── Rotation threshold accumulators ───────────────────────────────────
        if (menuCamera != null)
            _prevCamY = menuCamera.eulerAngles.y;

        _totalRotation = 0f;
        _nextGoSwap1   = goSwapThreshold1;
        _nextGlitch1   = glitchThreshold1;
        _nextGoSwap2   = goSwapThreshold2;
        _nextGlitch2   = glitchThreshold2;

        // ── Radio signals ─────────────────────────────────────────────────────
        InitRadioSignals();
    }

    private void Update()
    {
        // If settings panel is active, block background rotation/sway 
        // to keep the view stable for the user.
        if (settingsPanel != null && settingsPanel.gameObject.activeInHierarchy) return;

        UpdateSway();
        UpdateCamera();
        UpdateRadioSignals();
        if (!_glitchPlaying) CheckThresholds();
    }

    // ── Threshold checking ────────────────────────────────────────────────────
    private void CheckThresholds()
    {
        if (menuCamera == null) return;

        float currY = menuCamera.eulerAngles.y;
        float delta = Mathf.DeltaAngle(_prevCamY, currY);
        _totalRotation += Mathf.Max(0f, delta);
        _prevCamY = currY;

        // ── Silent GO swaps ──────────────────────────────────────────────────
        // These fire quietly with no visual effect — the glitch at the next
        // threshold will cover the filter change; by then the GO is already
        // in position.
        if (_totalRotation >= _nextGoSwap1)
        {
            _nextGoSwap1 += 360f;
            DoGoSwapA();
        }

        if (_totalRotation >= _nextGoSwap2)
        {
            _nextGoSwap2 += 360f;
            DoGoSwapB();
        }

        // ── Glitch + filter swaps ────────────────────────────────────────────
        if (_totalRotation >= _nextGlitch1)
        {
            _nextGlitch1 += 360f;
            StartCoroutine(PlayGlitch());
        }

        if (_totalRotation >= _nextGlitch2)
        {
            _nextGlitch2 += 360f;
            StartCoroutine(PlayGlitch());
        }
    }

    // ── Silent GO swaps ───────────────────────────────────────────────────────
    // No glitch, no filter change — just SetActive behind the scenes.

    private void DoGoSwapA()
    {
        _slotAShowingGO3 = !_slotAShowingGO3;
        SetActive(sceneObject1, !_slotAShowingGO3);
        SetActive(sceneObject3,  _slotAShowingGO3);
        // Signal must not bleed through before the glitch fires — force both inactive.
        SetActive(radioSignal1, false);
        SetActive(radioSignal3, false);
    }

    private void DoGoSwapB()
    {
        _slotBShowingGO4 = !_slotBShowingGO4;
        SetActive(sceneObject2, !_slotBShowingGO4);
        SetActive(sceneObject4,  _slotBShowingGO4);
        // Signal must not bleed through before the glitch fires — force both inactive.
        SetActive(radioSignal2, false);
        SetActive(radioSignal4, false);
    }

    // ── Glitch coroutine — only handles the filter cycle ─────────────────────
    // The GO is already in place. The glitch fires 30° later and advances
    // the filter index by 1 at peak chaos so the viewer never sees the cut.
    //
    // Filter cycle: 0 → 1 → 2 → 3 → 0 → 1 → ...  (filterForSlot1..4)
    private IEnumerator PlayGlitch()
    {
        _glitchPlaying = true;

        SetFeature(_glitch, true);
        DirtyRenderer();
        _glitch?.SetProgress(0f);

        float elapsed     = 0f;
        float half        = glitchDuration * 0.5f;
        bool  filterSwapped = false;

        while (elapsed < glitchDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _glitch?.SetProgress(Mathf.Clamp01(elapsed / glitchDuration));

            // At peak chaos — advance filter index, hidden inside the static
            if (!filterSwapped && elapsed >= half)
            {
                filterSwapped = true;
                AdvanceFilter();
            }

            yield return null;
        }

        _glitch?.SetProgress(1f);
        SetFeature(_glitch, false);
        DirtyRenderer();

        // Advance the signal cycle: deactivate current, move to next, activate it.
        // Every glitch fire (threshold 1 or 2) steps: 1→2→3→4→1→...
        SetActive(_radioSignals[_activeSignalIndex], false);
        _activeSignalIndex = (_activeSignalIndex + 1) % _radioSignals.Length;
        SetActive(_radioSignals[_activeSignalIndex], true);

        _glitchPlaying = false;
    }

    // ── Advance filter cycle ──────────────────────────────────────────────────
    // Disables the current filter, increments index (wraps 0..3), enables next.
    private void AdvanceFilter()
    {
        if (_filters == null) return;

        // Disable current
        SetFeature(_filters[_activeFilterIndex], false);

        // Advance
        _activeFilterIndex = (_activeFilterIndex + 1) % _filters.Length;

        // Enable next
        SetFeature(_filters[_activeFilterIndex], true);

        DirtyRenderer();

        Debug.Log($"[MainMenuManager] Filter advanced to slot {_activeFilterIndex + 1}");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private T FindFeature<T>() where T : ScriptableRendererFeature
    {
        if (rendererData == null) return null;
        foreach (var f in rendererData.rendererFeatures)
            if (f is T match) return match;
        return null;
    }

    private void SetFeature(ScriptableRendererFeature f, bool on)
    {
        if (f != null) f.SetActive(on);
    }

    private void DirtyRenderer()
    {
        if (rendererData != null) rendererData.SetDirty();
    }

    private static void SetActive(GameObject go, bool on)
    {
        if (go != null) go.SetActive(on);
    }

    // ── Panel sway ───────────────────────────────────────────────────────────
    private void UpdateSway()
    {
        if (swayPanel == null) return;

        float target = (_hovered && pauseOnHover) ? 0f : 1f;
        _swayIntensity = Mathf.MoveTowards(_swayIntensity, target,
                                            hoverFadeSpeed * Time.unscaledDeltaTime);

        if (_swayIntensity <= 0.001f)
        {
            swayPanel.localRotation = _baseRot;
            swayPanel.localScale    = _baseScale;
            return;
        }

        float t    = (Time.unscaledTime + _timeOffset) / swayDuration;
        float lean = Mathf.Sin(t * Mathf.PI * 2f) * swayAngle * _swayIntensity;
        float side = Mathf.Sin(t * Mathf.PI * 2f * 0.7f + 1f)
                   * swayAngle * sideSwayFraction * _swayIntensity;

        swayPanel.localRotation = _baseRot * Quaternion.Euler(lean, 0f, side);
        swayPanel.localScale    = _baseScale
                                * (1f + Mathf.Sin(t * Mathf.PI)
                                      * scalePulseAmount * _swayIntensity);
    }

    // ── Camera rotation ──────────────────────────────────────────────────────
    private void UpdateCamera()
    {
        if (menuCamera == null) return;
        menuCamera.Rotate(Vector3.up, cameraRotateSpeed * Time.deltaTime, Space.World);
    }

    // ── Hover ─────────────────────────────────────────────────────────────────
    public void OnPointerEnter(PointerEventData _) 
    { 
        _hovered = true; 
    }
    public void OnPointerExit(PointerEventData _)  => _hovered = false;

    // Redundant manual sound calls removed, handled by UIButtonSounds component

    // ── Menu buttons ──────────────────────────────────────────────────────────
    public void OnNewGame()
    {
        Time.timeScale = 1f;
        SaveSystem.DeleteSave();
        GridTransitionManager.Instance.LoadScene("GameScene");;
    }

    public void OnLoadGame()
    {
        if (SaveSystem.HasSaveData())
        {
            Time.timeScale = 1f;
            GridTransitionManager.Instance.LoadScene("GameScene");;
        }
    }

    public void OnSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.OpenSettings(mainContent);
        }
    }

    public void ShowMainContent(bool show)
    {
        if (mainContent != null)
            mainContent.SetActive(show);
    }

    public void OnExit()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    // ── Button highlight hover ────────────────────────────────────────────────
    // Finds the "Highlight" child on a button and registers PointerEnter/Exit
    // events via EventTrigger to set its Image alpha to 1 or 0.
    private void RegisterHighlightHover(Button btn)
    {
        Transform highlight = btn.transform.Find("Highlight");
        if (highlight == null)
        {
            Debug.LogWarning($"[MainMenuManager] No 'Highlight' child found on {btn.name}.");
            return;
        }

        Image highlightImage = highlight.GetComponent<Image>();
        if (highlightImage == null)
        {
            Debug.LogWarning($"[MainMenuManager] 'Highlight' on {btn.name} has no Image component.");
            return;
        }

        // Ensure alpha starts at 0
        SetHighlight(highlightImage, 0f);

        EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>()
                            ?? btn.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry enterEntry = new EventTrigger.Entry
            { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener(_ => SetHighlight(highlightImage, 0.35f));
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry
            { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener(_ => SetHighlight(highlightImage, 0f));
        trigger.triggers.Add(exitEntry);
    }

    private static void SetHighlight(Image img, float alpha)
    {
        Color c = img.color;
        c.a     = alpha;
        img.color = c;
    }

    // ── Radio signal emitter ──────────────────────────────────────────────────
    // Builds one LineRenderer child per ring per signal GO in Start(),
    // then drives radius + alpha every Update() via a shared timer.
    private void InitRadioSignals()
    {
        GameObject[] signals = { radioSignal1, radioSignal2, radioSignal3, radioSignal4 };
        _radioRenderers = new LineRenderer[signals.Length][];

        for (int s = 0; s < signals.Length; s++)
        {
            if (signals[s] == null)
            {
                _radioRenderers[s] = new LineRenderer[0];
                continue;
            }

            _radioRenderers[s] = new LineRenderer[radioRingCount];

            for (int r = 0; r < radioRingCount; r++)
            {
                // One child GO per ring so each has its own LineRenderer
                GameObject ringGO = new GameObject($"RadioRing_{s}_{r}");
                ringGO.transform.SetParent(signals[s].transform, false);
                ringGO.transform.localPosition = Vector3.zero;

                LineRenderer lr = ringGO.AddComponent<LineRenderer>();
                lr.useWorldSpace    = false;           // positions are local to the ring GO
                lr.loop             = true;
                lr.positionCount    = radioSegments;
                lr.startWidth       = radioLineWidth;
                lr.endWidth         = radioLineWidth;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows   = false;

                // Use an unlit material so color/alpha are respected without lighting
                lr.material = new Material(Shader.Find("Sprites/Default"));

                Color c = radioColor;
                c.a = 0f;
                lr.startColor = c;
                lr.endColor   = c;

                _radioRenderers[s][r] = lr;
            }
        }

        _radioTime = 0f;

        // Pre-position every ring at its correct staggered radius/alpha so there
        // is no flash-frame of all rings sitting at Vector3.zero on the first render.
        UpdateRadioSignals();
    }

    private void UpdateRadioSignals()
    {
        if (_radioRenderers == null) return;

        _radioTime += Time.deltaTime;

        for (int s = 0; s < _radioRenderers.Length; s++)
        {
            if (_radioRenderers[s] == null) continue;

            for (int r = 0; r < _radioRenderers[s].Length; r++)
            {
                LineRenderer lr = _radioRenderers[s][r];
                if (lr == null) continue;

                // Each ring is offset in phase so rings are always staggered
                float phase  = (float)r / radioRingCount;
                float t      = ((_radioTime / radioPulseDuration) + phase) % 1f;

                float radius = t * radioMaxRadius;
                float alpha  = 1f - t;  // born opaque, fades as it expands

                // Write circle points on the XZ plane (flat horizontal ring)
                for (int i = 0; i < radioSegments; i++)
                {
                    float angle = (float)i / radioSegments * Mathf.PI * 2f;
                    lr.SetPosition(i, new Vector3(
                        Mathf.Cos(angle) * radius,
                        0f,
                        Mathf.Sin(angle) * radius
                    ));
                }

                Color c = radioColor;
                c.a = alpha;
                lr.startColor = c;
                lr.endColor   = c;
            }
        }
    }
}