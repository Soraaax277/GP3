using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    public enum Ending { Exodus, Liquidation, Monopoly }

    // Lock to prevent concurrent scene loading operations
    private bool _isProcessing = false; 

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) 
        { 
            Destroy(gameObject); 
            return; 
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        StartCoroutine(BootUpSequence());
    }

    private IEnumerator BootUpSequence()
    {
        _isProcessing = true;

        if (!SceneManager.GetSceneByName("MainMenuScene").isLoaded)
        {
            yield return SceneManager.LoadSceneAsync("MainMenuScene", LoadSceneMode.Additive);
        }
        
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("MainMenuScene"));

        if (SceneManager.GetSceneByName("Boot").isLoaded)
        {
            SceneManager.UnloadSceneAsync("Boot");
        }

        _isProcessing = false;
    }

    // -------------------------------------------------------
    //  MAIN MENU → GAME
    // -------------------------------------------------------

    public void StartGame()
    {
        // Abort if a loading operation is already in progress
        if (_isProcessing) return; 
        StartCoroutine(TransitionToGame());
    }

    private IEnumerator TransitionToGame()
    {
        _isProcessing = true;

        UnloadScene("MainMenuScene");
        
        // Validate before loading to prevent duplicates
        if (!SceneManager.GetSceneByName("GameScene").isLoaded)
        {
            yield return SceneManager.LoadSceneAsync("GameScene", LoadSceneMode.Additive);
        }
        
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("GameScene"));
        
        if (!SceneManager.GetSceneByName("TransitionScene").isLoaded)
        {
            yield return SceneManager.LoadSceneAsync("TransitionScene", LoadSceneMode.Additive);
        }

        _isProcessing = false;
    }

    // -------------------------------------------------------
    //  ENDINGS
    // -------------------------------------------------------

    public void TriggerEnding(Ending ending)
    {
        string scene = ending switch
        {
            Ending.Exodus      => "ExodusEnding",
            Ending.Liquidation => "LiquidationScene",
            Ending.Monopoly    => "MonopolyEnding",
            _                  => ""
        };

        TriggerEndingByName(scene);
    }

    public void TriggerEndingByName(string sceneName)
    {
        if (_isProcessing) return;

        UnloadScene("ExodusEnding");
        UnloadScene("LiquidationScene");
        UnloadScene("MonopolyEnding");
        UnloadScene("GameScene"); 
        UnloadScene("TransitionScene");

        StartCoroutine(ActivateSceneSimple(sceneName));
    }

    // -------------------------------------------------------
    //  RETURN TO MAIN MENU
    // -------------------------------------------------------

    public void ReturnToMainMenu()
    {
        if (_isProcessing) return;

        UnloadScene("GameScene");
        UnloadScene("TransitionScene");
        UnloadScene("ExodusEnding");
        UnloadScene("LiquidationScene");
        UnloadScene("MonopolyEnding");

        StartCoroutine(ActivateSceneSimple("MainMenuScene", () =>
            SceneManager.SetActiveScene(SceneManager.GetSceneByName("MainMenuScene"))));
    }

    // -------------------------------------------------------
    //  Internal helpers
    // -------------------------------------------------------

    private IEnumerator ActivateSceneSimple(string sceneName, Action onDone = null)
    {
        _isProcessing = true;

        if (!SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }
        
        onDone?.Invoke();
        _isProcessing = false;
    }

    private void UnloadScene(string sceneName)
    {
        if (SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            SceneManager.UnloadSceneAsync(sceneName);
        }
    }
}