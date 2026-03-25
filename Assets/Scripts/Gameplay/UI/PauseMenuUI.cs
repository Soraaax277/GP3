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

    public static bool GameIsPaused = false;

    void Start()
    {
        // Automatically listen for the button click
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
        {
            settingsPanel.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // If the Tech Tree is currently open, do NOT run the Pause Menu logic.
        if (TechTreeWindowManager.IsTechTreeOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    private static int _lastToggleFrame = -1;

    public void TogglePause()
    {
        // Prevent multiple toggles in the same frame (e.g., if triggered twice by inspector/code)
        if (Time.frameCount == _lastToggleFrame) return;
        _lastToggleFrame = Time.frameCount;

        // Extra check: prevent pausing if tech tree is open (for the button click)
        if (TechTreeWindowManager.IsTechTreeOpen) return;

        if (GameIsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false); 
        Time.timeScale = 1f;        
        GameIsPaused = false;
    }

    void Pause()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;  
        GameIsPaused = true;
    }

    public void GoToMainMenu()
    {
        // Explicitly close settings panel first so it doesn't appear open in the Main Menu
        if (settingsPanel != null)
            settingsPanel.gameObject.SetActive(false);

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
            settingsPanel.OpenSettings(pauseMenuUI);
        }
    }
}