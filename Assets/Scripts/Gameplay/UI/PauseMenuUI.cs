using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuUI : MonoBehaviour
{
    [Header("UI Reference")]
    public GameObject pauseMenuUI; 
    public Button pauseButton;
    public Button settingsButton;
    public SettingsPanel settingsPanel;

    [Header("Blur Background (Era-Aware)")]
    [Tooltip("UI Image with Mat_EraBokeh material (uses EraBokehPanel shader).")]
    public Image blurImage;

    public static bool GameIsPaused = false;

    private void OnEnable()
    {
        if (GameIsPaused) UpdateBlurForEra();
    }

    private void OnDisable()
    {
        EraBlurFeature.IsActive = false;
    }

    void Start()
    {
        if (pauseButton != null)
        {
            if (pauseButton.gameObject.GetComponent<UIButtonSounds>() == null)
                pauseButton.gameObject.AddComponent<UIButtonSounds>();
            pauseButton.onClick.AddListener(TogglePause);
        }

        if (settingsButton != null)
        {
            if (settingsButton.gameObject.GetComponent<UIButtonSounds>() == null)
                settingsButton.gameObject.AddComponent<UIButtonSounds>();
            settingsButton.onClick.AddListener(OnSettings);
        }

        if (settingsPanel != null)
            settingsPanel.gameObject.SetActive(false);
    }

    void Update()
    {
        // Ignore Escape if other major windows are open
        if (TechTreeWindowManager.IsTechTreeOpen) return;
        if (settingsPanel != null && settingsPanel.gameObject.activeInHierarchy) return;

        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    private static int _lastToggleFrame = -1;

    public void TogglePause()
    {
        if (Time.frameCount == _lastToggleFrame) return;
        _lastToggleFrame = Time.frameCount;

        if (TechTreeWindowManager.IsTechTreeOpen) return;
        if (settingsPanel != null && settingsPanel.gameObject.activeInHierarchy) return;

        if (GameIsPaused) Resume();
        else Pause();
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false); 
        if (blurImage != null) blurImage.gameObject.SetActive(false);
        EraBlurFeature.IsActive = false;
        Time.timeScale = 1f;        
        GameIsPaused = false;
    }

    void Pause()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;  
        GameIsPaused = true;

        // ── DYNAMIC ERA BLUR REFRESH ──
        UpdateBlurForEra();
    }

    private void UpdateBlurForEra()
    {
        // 1. Activate the Global Full-Screen Blur (The Renderer Feature)
        // This blurs EVERYTHING behind the UI without needing a special shader on the overlay image.
        EraBlurFeature.IsActive = true;

        // 2. Optional: Ensure your 'darkoverlay' Image is active to darken the background
        if (blurImage != null)
        {
            blurImage.gameObject.SetActive(true);
            
            // Set it to a simple semi-transparent black for the 'darkoverlay' look
            // No materials or shaders needed on this Image anymore!
            blurImage.color = new Color(0, 0, 0, 0.45f);

            // ERA TINTS: If you want to color the 'darkoverlay' by era
            if (TurnManager.Instance != null)
            {
                Color eraTint = Color.black;
                switch (TurnManager.Instance.currentEra)
                {
                    case TurnManager.GameEra.Industrial:    eraTint = new Color(0.3f, 0.2f, 0.1f); break; // Dark Sepia
                    case TurnManager.GameEra.EarlyEighties: eraTint = new Color(0.1f, 0.3f, 0.4f); break; // Dark Cyan
                    case TurnManager.GameEra.Retro:          eraTint = new Color(0.1f, 0.4f, 0.2f); break; // Dark Green
                    case TurnManager.GameEra.Futuristic:     eraTint = new Color(0.1f, 0.2f, 0.5f); break; // Dark Blue
                }
                blurImage.color = new Color(eraTint.r, eraTint.g, eraTint.b, 0.5f);
            }
        }
    }

    public void GoToMainMenu()
    {
        if (settingsPanel != null)
            settingsPanel.gameObject.SetActive(false);

        if (blurImage != null) blurImage.gameObject.SetActive(false);
        SaveSystem.SaveGame();
        DG.Tweening.DOTween.KillAll();
        Time.timeScale = 1f;
        GameIsPaused = false;
        SceneManager.LoadScene("MainMenuScene");
    }

    public void OnSettings()
    {
        if (settingsPanel != null)
        {
            // Pause UI goes off, Settings UI comes on (and it has its own blur)
            settingsPanel.OpenSettings(pauseMenuUI);
        }
    }
}