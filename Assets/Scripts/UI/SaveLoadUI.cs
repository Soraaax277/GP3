using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SaveLoadUI : MonoBehaviour
{
    private void Start()
    {
        // Shotgun approach: Find EVERY button in the active scene and ensure it has sound triggers.
        // This covers the Save/Reload buttons wherever they are in the hierarchy.
        Button[] allButtons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in allButtons)
        {
            if (b.gameObject.GetComponent<UIButtonSounds>() == null)
                b.gameObject.AddComponent<UIButtonSounds>();
        }
    }

    public void SaveGame()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.buttonClickSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClickSFX);

        SaveSystem.SaveGame();
        Debug.Log("Game Saved via UI");
    }

    public void LoadGame()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.buttonClickSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClickSFX);

        if (SaveSystem.HasSaveData())
        {
            Time.timeScale = 1f;
            // Reload the current scene. 
            // GameManager will detect save data in SetupGame and call SaveSystem.LoadGame()
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            Debug.Log("Loading Saved Game...");
        }
        else
        {
            Debug.LogWarning("No save data found to load!");
        }
    }

    public void ReloadGame()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.buttonClickSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClickSFX);

        Time.timeScale = 1f;
        // "Reload basically means to reset back to the first start of the game"
        SaveSystem.DeleteSave();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        Debug.Log("Game Reloaded (Reset to Start)");
    }
}
