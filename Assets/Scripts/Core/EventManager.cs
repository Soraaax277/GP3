using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance;

    public enum EventType { None, AcidRain, SolarFlare, PowerOutage, HyperInflation, TechBoom }
    
    [Header("Current Status")]
    [Range(0f, 1f)] public float eventSpawnChance = 0.2f;
    public EventType activeEvent = EventType.None;
    public int eventDurationLeft = 0;

    [Header("Visual Symbols (Assign in Inspector)")]
    public GameObject rainParticlePrefab;
    public GameObject solarFlareParticlePrefab;
    public GameObject powerOutageParticlePrefab;

    private GameObject currentParticleSystem;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ProcessTurnEvents()
    {
        if (eventDurationLeft > 0)
        {
            eventDurationLeft--;
            if (eventDurationLeft <= 0)
            {
                EndCurrentEvent();
                TryStartRandomEvent();
            }
        }
        else
        {
            TryStartRandomEvent();
        }

        ApplyEventEffects();
    }

    private void TryStartRandomEvent()
    {
        // Use the inspector-controlled chance
        if (Random.value < eventSpawnChance)
        {
            StartRandomEvent();
        }
    }

    [Header("Single Tile Target")]
    public HexTile targetTile;

    private void StartRandomEvent()
    {
        // Pick random event except None
        activeEvent = (EventType)Random.Range(1, (int)EventType.TechBoom + 1);
        eventDurationLeft = Random.Range(3, 6); // Lasts 3-5 turns

        // Pick a random target tile
        List<HexTile> allTiles = new List<HexTile>(GridManager.Instance.GetAllTiles());
        if (allTiles.Count > 0)
        {
            targetTile = allTiles[Random.Range(0, allTiles.Count)];
        }

        Debug.Log($"[EventManager] NEW EVENT: {activeEvent} targeting tile {targetTile?.cubeCoords} for {eventDurationLeft} turns!");

        SpawnEventParticles();
    }

    private void EndCurrentEvent()
    {
        Debug.Log($"[EventManager] Event {activeEvent} at {targetTile?.cubeCoords} has ended.");
        activeEvent = EventType.None;
        targetTile = null;
        
        if (currentParticleSystem != null)
        {
            Destroy(currentParticleSystem);
        }
    }

    private void ApplyEventEffects()
    {
        if (activeEvent == EventType.None || targetTile == null) return;

        // Each tile's hazardImpact determines how strongly it is hit
        float impact = targetTile.hazardImpact;

        switch (activeEvent)
        {
            case EventType.AcidRain:
                if (targetTile.placedTower != null) targetTile.placedTower.currentDurability -= 5f * impact;
                if (targetTile.placedWire != null) targetTile.placedWire.currentDurability -= 3f * impact;
                break;
            case EventType.SolarFlare:
                targetTile.influenceSuppression += Mathf.RoundToInt(10 * impact);
                break;
            case EventType.PowerOutage:
                if (targetTile.placedWire != null && Random.value < 0.2f * impact)
                {
                    targetTile.placedWire.TakeDamage(10f);
                }
                break;
        }
    }

    private void SpawnEventParticles()
    {
        if (currentParticleSystem != null) Destroy(currentParticleSystem);
        if (targetTile == null) return;

        GameObject prefab = null;
        switch (activeEvent)
        {
            case EventType.AcidRain: prefab = rainParticlePrefab; break;
            case EventType.SolarFlare: prefab = solarFlareParticlePrefab; break;
            case EventType.PowerOutage: prefab = powerOutageParticlePrefab; break;
        }

        if (prefab != null)
        {
            currentParticleSystem = Instantiate(prefab, targetTile.transform.position + Vector3.up * 5f, Quaternion.identity, transform);
        }
        else
        {
            CreateProceduralParticle();
        }
    }

    private void CreateProceduralParticle()
    {
        GameObject particles = new GameObject("Procedural_Disaster_Particles");
        particles.transform.SetParent(transform);
        particles.transform.position = targetTile.transform.position + Vector3.up * 5f;
        
        var ps = particles.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 2f;
        main.startSpeed = 5f;
        main.startSize = 0.3f;
        main.maxParticles = 500;
        
        var emission = ps.emission;
        emission.rateOverTime = 100f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(2f, 2f, 1f); // Cover roughly one hex
        shape.rotation = new Vector3(90, 0, 0);

        switch (activeEvent)
        {
            case EventType.AcidRain:
                main.startColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);
                break;
            case EventType.SolarFlare:
                main.startColor = new Color(1f, 0.5f, 0f, 0.5f);
                break;
            case EventType.PowerOutage:
                main.startColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
                break;
        }

        currentParticleSystem = particles;
        ps.Play();
    }
}
