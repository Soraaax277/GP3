using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveLoadUI : MonoBehaviour
{
    public void SaveGame()
    {
        SaveSystem.SaveGame();
        Debug.Log("Game Saved via UI");
    }

    public void LoadGame()
    {
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
        Time.timeScale = 1f;
        // "Reload basically means to reset back to the first start of the game"
        SaveSystem.DeleteSave();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        Debug.Log("Game Reloaded (Reset to Start)");
    }
}
