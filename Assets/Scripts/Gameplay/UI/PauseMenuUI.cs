using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; 

public class PauseMenuUI : MonoBehaviour
{
    [Header("UI Reference")]
    public GameObject pauseMenuUI; 
    public Button pauseButton;

    public static bool GameIsPaused = false;

    void Start()
    {
        // Automatically listen for the button click
        if (pauseButton != null)
        {
            pauseButton.onClick.AddListener(TogglePause);
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

    // New shared function for both Button and Escape Key
    public void TogglePause()
    {
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
        Time.timeScale = 1f;
        GameIsPaused = false;
        SceneManager.LoadScene("MainMenuScene");
    }
}