using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public Button newGameButton;
    public Button loadGameButton;
    public Button settingsButton;
    public Button exitButton;
    public GameObject settingsPanel;

    private void Start()
    {
        if (loadGameButton != null)
        {
            loadGameButton.interactable = SaveSystem.HasSaveData();
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    public void OnNewGame()
    {
        SaveSystem.DeleteSave();
        SceneManager.LoadScene("GameScene");
    }

    public void OnLoadGame()
    {
        if (SaveSystem.HasSaveData())
        {
            SceneManager.LoadScene("GameScene");
        }
    }

    public void OnSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
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
