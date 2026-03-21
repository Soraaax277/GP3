using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

// Attach this to any persistent GameObject in your scene (e.g. GameManager).
// Drag each Renderer Feature asset into the matching slot in the Inspector.
public class EraRendererController : MonoBehaviour
{
    public static EraRendererController Instance;

    [Header("Assign each Renderer Feature from your URP Renderer asset")]
    public FilmFilterFeature   industrialFeature;    // Era: Industrial    (turns 1-25)
    public CRTTVFilterFeature  eightiesFeature;      // Era: EarlyEighties (turns 26-50)
    public NightGradeFeature   retroFeature;         // Era: Retro         (turns 51-75)
    public CyberpunkFeature    futuristicFeature;    // Era: Futuristic    (turns 76-100)

    TurnManager.GameEra _lastEra = (TurnManager.GameEra)(-1); // force first apply

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // TurnManager.Instance may not exist yet in OnEnable (depends on script
        // execution order). Defer subscription until it is guaranteed to be ready,
        // using the same pattern DebugCheatManager uses.
        StartCoroutine(SubscribeWhenReady());
    }

    void OnDisable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStarted -= OnTurnStarted;
    }

    private IEnumerator SubscribeWhenReady()
    {
        // Wait until TurnManager exists and the player list is populated
        while (TurnManager.Instance == null ||
               TurnManager.Instance.players == null ||
               TurnManager.Instance.players.Count == 0)
            yield return null;

        // Extra frame so TurnManager.StartGame() fully completes
        yield return null;

        // Avoid double-subscribe if the coroutine somehow runs twice
        TurnManager.Instance.OnTurnStarted -= OnTurnStarted;
        TurnManager.Instance.OnTurnStarted += OnTurnStarted;

        // Immediately apply whatever era is current so the feature is correct
        // from turn 1 without waiting for the next OnTurnStarted event
        ForceSync();

        Debug.Log("[EraRendererController] Subscribed to OnTurnStarted and synced initial era.");
    }

    void OnTurnStarted(PlayerData _)
    {
        TurnManager.GameEra era = TurnManager.Instance.currentEra;

        // Only do work when the era actually changes
        if (era == _lastEra) return;
        _lastEra = era;

        ApplyEra(era);
    }

    void ApplyEra(TurnManager.GameEra era)
    {
        SetFeature(industrialFeature,  era == TurnManager.GameEra.Industrial);
        SetFeature(eightiesFeature,    era == TurnManager.GameEra.EarlyEighties);
        SetFeature(retroFeature,       era == TurnManager.GameEra.Retro);
        SetFeature(futuristicFeature,  era == TurnManager.GameEra.Futuristic);

        Debug.Log($"[EraRendererController] Era changed to {era} - renderer feature updated.");
    }

    void SetFeature(ScriptableRendererFeature feature, bool active)
    {
        if (feature == null)
        {
            Debug.LogWarning("[EraRendererController] A renderer feature slot is not assigned.");
            return;
        }
        feature.SetActive(active);
    }

    // Force-syncs the active feature to the current era immediately.
    // Called on startup and by DebugCheatManager.CheatForceEra().
    public void ForceSync()
    {
        if (TurnManager.Instance == null) return;
        _lastEra = (TurnManager.GameEra)(-1); // reset so ApplyEra always fires
        ApplyEra(TurnManager.Instance.currentEra);
    }
}