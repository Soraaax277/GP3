using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public AudioMixer audioMixer;

    [Header("BGM clips")]
    public AudioClip bgmMenu;
    public AudioClip bgmIndustrial;
    public AudioClip bgmEarly80s;
    public AudioClip bgmRetro;
    public AudioClip bgmFuturistic;

    [Header("SFX Structures")]
    public AudioClip placeTowerSFX;
    public AudioClip placeWireSFX;
    public AudioClip placeBuildingSFX;

    [Header("SFX Unit Actions")]
    public AudioClip constructSFX;
    public AudioClip repairSFX;
    public AudioClip sabotageSFX;
    public AudioClip layWireSFX;
    public AudioClip powerSFX;
    public AudioClip denySFX;
    public AudioClip recruitSFX;
    public AudioClip convertSFX;
    public AudioClip refillSFX;
    public AudioClip researchSFX;
    public AudioClip maintainSFX;
    public AudioClip selectSFX;
    public AudioClip moveSFX;

    [Header("SFX Hazards")]
    public AudioClip acidRainSFX;
    public AudioClip solarFlareSFX; // Sunray
    public AudioClip powerOutageSFX; // Thunderstorm
    public AudioClip hyperInflationSFX;
    public AudioClip techBoomSFX;
    public AudioClip saboteurSFX;
    public AudioClip geyserSFX;

    [Header("Audio Sources (Internal)")]
    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioSource hazardSource;

    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string HAZARD_VOLUME_KEY = "HazardVolume";
    private const string HAZARD_ENABLED_KEY = "HazardEnabled";

    private Coroutine musicFadeCoroutine;
    private TurnManager.GameEra lastEra;

    private void Awake()
    {
        // NO DONT DESTROY ON LOAD - We want one manager per scene
        Instance = this;
        
        // Setup internal audio sources so we always have them
        EnsureAudioSourcesExist();
        
        // Route to mixer groups if assigned
        if (audioMixer != null)
        {
            AudioMixerGroup[] musicGroups = audioMixer.FindMatchingGroups("Music");
            if (musicGroups.Length > 0) musicSource.outputAudioMixerGroup = musicGroups[0];

            AudioMixerGroup[] sfxGroups = audioMixer.FindMatchingGroups("SFX");
            if (sfxGroups.Length > 0)
            {
                sfxSource.outputAudioMixerGroup = sfxGroups[0];
                hazardSource.outputAudioMixerGroup = sfxGroups[0];
            }
        }

        LoadAudioSettings();
    }

    private void EnsureAudioSourcesExist()
    {
        musicSource = SetupInternalSource("BGM_Source", true);
        sfxSource = SetupInternalSource("SFX_Source", false);
        hazardSource = SetupInternalSource("Hazard_Source", false);
    }

    private AudioSource SetupInternalSource(string objName, bool loop)
    {
        Transform t = transform.Find(objName);
        if (t == null)
        {
            GameObject newObj = new GameObject(objName);
            newObj.transform.SetParent(this.transform);
            t = newObj.transform;
        }

        AudioSource source = t.GetComponent<AudioSource>();
        if (source == null) source = t.gameObject.AddComponent<AudioSource>();
        
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f; // 2D by default
        return source;
    }

    private void Start()
    {
        // Start the BGM check
        StartCoroutine(AutoInitializeBGM());
        
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnEraChanged += HandleEraChanged;
        }
    }

    private IEnumerator AutoInitializeBGM()
    {
        // Wait until we have a scene name and other managers are ready
        while (string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name))
            yield return null;

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToLower();

        if (sceneName.Contains("menu") || sceneName.Contains("main"))
        {
            if (bgmMenu != null) PlayBGM(bgmMenu);
        }
        else
        {
            // Give TurnManager a moment to wake up if needed
            int retryCount = 0;
            while (TurnManager.Instance == null && retryCount < 10)
            {
                retryCount++;
                yield return new WaitForSeconds(0.1f);
            }

            if (TurnManager.Instance != null)
            {
                UpdateBGMForEra(TurnManager.Instance.currentEra, false);
                lastEra = TurnManager.Instance.currentEra;
            }
            else
            {
                Debug.LogWarning("[AudioManager] TurnManager not found. BGM may not start automatically.");
            }
        }
    }

    // Subscribe to scene changes
    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (Instance != this) return;

        // Stop any currently running BGM initialization and start a fresh one for the new scene
        StopAllCoroutines();
        StartCoroutine(AutoInitializeBGM());
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnEraChanged -= HandleEraChanged;
        }
    }

    private void HandleEraChanged(TurnManager.GameEra newEra)
    {
        if (newEra != lastEra)
        {
            UpdateBGMForEra(newEra, true);
            lastEra = newEra;
        }
    }

    private void UpdateBGMForEra(TurnManager.GameEra era, bool fade)
    {
        AudioClip targetClip = null;
        switch (era)
        {
            case TurnManager.GameEra.Industrial: targetClip = bgmIndustrial; break;
            case TurnManager.GameEra.EarlyEighties: targetClip = bgmEarly80s; break;
            case TurnManager.GameEra.Retro: targetClip = bgmRetro; break;
            case TurnManager.GameEra.Futuristic: targetClip = bgmFuturistic; break;
        }

        if (targetClip != null)
        {
            if (fade)
            {
                if (musicFadeCoroutine != null) StopCoroutine(musicFadeCoroutine);
                musicFadeCoroutine = StartCoroutine(FadeBGM(targetClip, 2f));
            }
            else
            {
                PlayBGM(targetClip);
            }
        }
    }

    private void LoadAudioSettings()
    {
        float masterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, 0.75f);
        float musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 0.75f);
        float sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 0.75f);
        float hazardVolume = PlayerPrefs.GetFloat(HAZARD_VOLUME_KEY, 0.75f);
        bool hazardEnabled = PlayerPrefs.GetInt(HAZARD_ENABLED_KEY, 1) == 1;

        SetMasterVolume(masterVolume);
        SetMusicVolume(musicVolume);
        SetSFXVolume(sfxVolume);
        SetHazardVolume(hazardVolume);
        SetHazardEnabled(hazardEnabled);
    }

    public void SetMasterVolume(float volume)
    {
        float dbValue = VolumeToDecibels(volume);
        if (audioMixer != null) audioMixer.SetFloat("MasterVolume", dbValue);
        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, volume);
        PlayerPrefs.Save();
    }

    public void SetMusicVolume(float volume)
    {
        float dbValue = VolumeToDecibels(volume);
        if (audioMixer != null) audioMixer.SetFloat("MusicVolume", dbValue);
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, volume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        float dbValue = VolumeToDecibels(volume);
        if (audioMixer != null) audioMixer.SetFloat("SFXVolume", dbValue);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, volume);
        PlayerPrefs.Save();
    }

    public void SetHazardVolume(float volume)
    {
        // Removed mixer call as it doesn't exist. Hazard volume is controlled by SFX group.
        PlayerPrefs.SetFloat(HAZARD_VOLUME_KEY, volume);
        PlayerPrefs.Save();
        UpdateHazardAudioState();
    }

    public void SetHazardEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(HAZARD_ENABLED_KEY, enabled ? 1 : 0);
        PlayerPrefs.Save();
        UpdateHazardAudioState();
    }

    private void UpdateHazardAudioState()
    {
        if (hazardSource != null)
        {
            hazardSource.mute = !IsHazardEnabled();
        }
    }

    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        // FINAL SAFETY: If the manager is being destroyed or the source is gone, stop here
        if (Instance != this || musicSource == null) return;

        try 
        {
            if (musicSource.clip == clip) return;
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.volume = 1f; // Reset volume in case it was fading
            musicSource.Play();
        }
        catch (UnityEngine.MissingReferenceException)
        {
            // Catch-all for rare race conditions during scene disposal
        }
    }

    private IEnumerator FadeBGM(AudioClip newClip, float duration)
    {
        if (musicSource == null || Instance != this) yield break;

        float startVolume = musicSource.volume;
        
        // Fade out
        if (musicSource.isPlaying)
        {
            for (float t = 0; t < duration / 2; t += Time.deltaTime)
            {
                if (musicSource == null) yield break;
                musicSource.volume = Mathf.Lerp(startVolume, 0, t / (duration / 2));
                yield return null;
            }
            if (musicSource != null)
            {
                musicSource.volume = 0;
                musicSource.Stop();
            }
        }

        if (musicSource == null || Instance != this) yield break;

        // Switch and Fade in
        musicSource.clip = newClip;
        musicSource.Play();
        
        for (float t = 0; t < duration / 2; t += Time.deltaTime)
        {
            if (musicSource == null) yield break;
            musicSource.volume = Mathf.Lerp(0, 1f, t / (duration / 2));
            yield return null;
        }
        
        if (musicSource != null) musicSource.volume = 1f;
        musicFadeCoroutine = null;
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null || Instance != this) return;
        sfxSource.PlayOneShot(clip);
    }

    public void PlayHazardSFX(AudioClip clip)
    {
        if (clip == null || hazardSource == null || !IsHazardEnabled() || Instance != this) return;
        hazardSource.PlayOneShot(clip);
    }

    public void PlaySFXAtPosition(AudioClip clip, Vector3 position)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, GetSFXVolume());
    }

    public float GetMasterVolume()
    {
        return PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, 0.75f);
    }

    public float GetMusicVolume()
    {
        return PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 0.75f);
    }

    public float GetSFXVolume()
    {
        return PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 0.75f);
    }

    public float GetHazardVolume()
    {
        return PlayerPrefs.GetFloat(HAZARD_VOLUME_KEY, 0.75f);
    }

    public bool IsHazardEnabled()
    {
        return PlayerPrefs.GetInt(HAZARD_ENABLED_KEY, 1) == 1;
    }

    private float VolumeToDecibels(float volume)
    {
        if (volume <= 0.0001f)
            return -80f;
        return Mathf.Log10(volume) * 20f;
    }
}


