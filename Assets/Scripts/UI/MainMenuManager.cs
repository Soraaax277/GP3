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
        }

        if (loadGameButton != null)
        {
            if (loadGameButton.gameObject.GetComponent<UIButtonSounds>() == null)
                loadGameButton.gameObject.AddComponent<UIButtonSounds>();
            loadGameButton.onClick.AddListener(OnLoadGame);
        }

        if (settingsButton != null)
        {
            if (settingsButton.gameObject.GetComponent<UIButtonSounds>() == null)
                settingsButton.gameObject.AddComponent<UIButtonSounds>();
            settingsButton.onClick.AddListener(OnSettings);
        }

        if (exitButton != null)
        {
            if (exitButton.gameObject.GetComponent<UIButtonSounds>() == null)
                exitButton.gameObject.AddComponent<UIButtonSounds>();
            exitButton.onClick.AddListener(OnExit);
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
    }

    private void Update()
    {
        // If settings panel is active, block background rotation/sway 
        // to keep the view stable for the user.
        if (settingsPanel != null && settingsPanel.gameObject.activeInHierarchy) return;

        UpdateSway();
        UpdateCamera();
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
    }

    private void DoGoSwapB()
    {
        _slotBShowingGO4 = !_slotBShowingGO4;
        SetActive(sceneObject2, !_slotBShowingGO4);
        SetActive(sceneObject4,  _slotBShowingGO4);
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
}