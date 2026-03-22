using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;
    public Toggle hazardToggle;

    private void OnEnable()
    {
        LoadCurrentVolumes();
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
        {
            AudioManager.Instance.SetMasterVolume(value);
        }
    }

    public void OnMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(value);
        }
    }

    public void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(value);
        }
    }

    public void OnHazardToggleChanged(bool value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetHazardEnabled(value);
        }
    }

    public void OnClose()
    {
        gameObject.SetActive(false);
    }
}

