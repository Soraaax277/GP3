using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class SettingsPanel : MonoBehaviour
{
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;
    public Toggle hazardToggle;

    [Header("Blur Settings (Era-Aware)")]
    [Tooltip("UI Image with Mat_EraBokeh material (uses EraBokehPanel shader).")]
    public Image blurImage;

    [Header("Back Button")]
    public Button backButton;

    private void Awake()
    {
    }

    private void Start()
    {
        if (backButton != null)
        {
            if (backButton.gameObject.GetComponent<UIButtonSounds>() == null)
                backButton.gameObject.AddComponent<UIButtonSounds>();
            backButton.onClick.AddListener(OnClose);
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            OnClose();
    }

    private GameObject _previousPanel;

    public void OpenSettings(GameObject panelToHide)
    {
        _previousPanel = panelToHide;
        if (_previousPanel != null)
            _previousPanel.SetActive(false);
        
        UpdateBlurForEra();
        gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        LoadCurrentVolumes();
        UpdateBlurForEra();
    }

    private void OnDisable()
    {
        if (blurImage != null) blurImage.gameObject.SetActive(false);
        EraBlurFeature.IsActive = false;
    }

    private void UpdateBlurForEra()
    {
        // 1. Activate the Global Full-Screen Blur (The Renderer Feature)
        EraBlurFeature.IsActive = true;

        // 2. Optional: Darken the 'darkoverlay' Image
        if (blurImage != null)
        {
            blurImage.gameObject.SetActive(true);
            
            // Subtle dark tint
            blurImage.color = new Color(0, 0, 0, 0.45f);

            // ERA TINTS:
            if (TurnManager.Instance != null)
            {
                Color eraTint = Color.black;
                switch (TurnManager.Instance.currentEra)
                {
                    case TurnManager.GameEra.Industrial:    eraTint = new Color(0.3f, 0.2f, 0.1f); break; 
                    case TurnManager.GameEra.EarlyEighties: eraTint = new Color(0.1f, 0.3f, 0.4f); break; 
                    case TurnManager.GameEra.Retro:          eraTint = new Color(0.1f, 0.4f, 0.2f); break; 
                    case TurnManager.GameEra.Futuristic:     eraTint = new Color(0.1f, 0.2f, 0.5f); break; 
                }
                blurImage.color = new Color(eraTint.r, eraTint.g, eraTint.b, 0.5f);
            }
        }
    }

    private void LoadCurrentVolumes()
    {
        if (AudioManager.Instance != null)
        {
            masterSlider.value = AudioManager.Instance.GetMasterVolume();
            musicSlider.value = AudioManager.Instance.GetMusicVolume();
            sfxSlider.value = AudioManager.Instance.GetSFXVolume();
            if (hazardToggle != null) 
                hazardToggle.isOn = AudioManager.Instance.IsHazardEnabled();
        }
    }

    public void OnMasterVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMasterVolume(value);
    }

    public void OnMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(value);
    }

    public void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSFXVolume(value);
    }

    public void OnHazardToggleChanged(bool value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetHazardEnabled(value);
    }

    public void OnClose()
    {
        if (blurImage != null) blurImage.gameObject.SetActive(false);
        gameObject.SetActive(false);

        if (_previousPanel != null)
        {
            _previousPanel.SetActive(true);
            _previousPanel = null;
        }
        else if (MainMenuManager.Instance != null)
        {
            MainMenuManager.Instance.ShowMainContent(true);
        }
    }
}
