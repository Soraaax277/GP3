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
    public GameObject hyperInflationParticlePrefab;
    public GameObject techBoomParticlePrefab;

    private GameObject currentParticleSystem;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // ── HIDE PRE-PLACED PARTICLES ────────────────────────────────────────
        // Find the 'ParticleSystems' container (as seen in hierarchy) and 
        // deactivate it immediately to prevent spray from showing up at startup.
        Transform container = transform.Find("ParticleSystems");
        if (container != null) container.gameObject.SetActive(false);

        // Also check for any siblings/children with particle system names
        foreach (Transform child in transform)
        {
            if (child.name.Contains("Rain") || child.name.Contains("Sun") || child.name.Contains("Explo"))
                child.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // ── VISIBILITY GATE (REAL-TIME) ──────────────────────────────────────
        // If no event is active, ensure the particle system is deactivated 
        // to prevent any stray particles from showing up.
        if (activeEvent == EventType.None)
        {
            if (currentParticleSystem != null && currentParticleSystem.activeSelf)
                currentParticleSystem.SetActive(false);
            return;
        }

        // Sync particle visibility every frame so that if the player moves 
        // a unit (revealing/hiding the tile), the event effect responds 
        // immediately. 
        if (targetTile != null && currentParticleSystem != null)
        {
            // Ensure the object is active if an event is running
            if (!currentParticleSystem.activeSelf) currentParticleSystem.SetActive(true);

            bool visible = targetTile.isVisible;
            foreach (var r in currentParticleSystem.GetComponentsInChildren<Renderer>(true))
            {
                if (r.enabled != visible) r.enabled = visible;
            }
        }
    }

    public void ProcessTurnEvents()
    {
        bool isStartOfCycle = (TurnManager.Instance != null && TurnManager.Instance.currentPlayerIndex == 0);

        if (isStartOfCycle)
        {
            AdvanceEventClock();
        }

        // Apply status effects and recurring damage (visuals handled in Update)
        ApplyEventEffects(isStartOfCycle);
    }

    private void AdvanceEventClock()
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

        string eventFlavor = GetEventFlavor(activeEvent);
        ActionLogUI.PostFiltered(null, eventFlavor, ActionLogUI.Colors.World, true);

        SpawnEventParticles();
        PlayEventSound(activeEvent);
    }

    private void PlayEventSound(EventType type)
    {
        if (AudioManager.Instance == null) return;

        AudioClip clip = null;
        switch (type)
        {
            case EventType.AcidRain: clip = AudioManager.Instance.acidRainSFX; break;
            case EventType.SolarFlare: clip = AudioManager.Instance.solarFlareSFX; break;
            case EventType.PowerOutage: clip = AudioManager.Instance.powerOutageSFX; break;
            case EventType.HyperInflation: clip = AudioManager.Instance.hyperInflationSFX; break;
            case EventType.TechBoom: clip = AudioManager.Instance.techBoomSFX; break;
        }

        if (clip != null)
        {
            AudioManager.Instance.PlayHazardSFX(clip);
        }
    }

    private string GetEventFlavor(EventType type)
    {
        switch (type)
        {
            case EventType.AcidRain: return "Acid Rain is falling from the sky!";
            case EventType.SolarFlare: return "A Solar Flare is disrupting communications!";
            case EventType.PowerOutage: return "A major Thunderstorm has started!";
            case EventType.HyperInflation: return "Hyper-inflation is hitting the local economy!";
            case EventType.TechBoom: return "A localized Tech-Boom is occurring!";
            default: return "An unusual environmental event is starting!";
        }
    }

    private void EndCurrentEvent()
    {
        Debug.Log($"[EventManager] Event {activeEvent} at {targetTile?.cubeCoords} has ended.");
        
        if (targetTile != null) targetTile.isHyperinflated = false;

        activeEvent = EventType.None;
        targetTile = null;
        
        if (currentParticleSystem != null)
        {
            Destroy(currentParticleSystem);
        }
    }

    private void ApplyEventEffects(bool dealDamage)
    {
        if (activeEvent == EventType.None || targetTile == null) 
        {
            if (currentParticleSystem != null) Destroy(currentParticleSystem);
            return;
        }

        // Each tile's hazard impact determines how strongly it is hit
        float impact = targetTile.hazardImpact;
        PlayerData owner = targetTile.GetOwner();
        string ownerName = owner != null ? owner.playerName : "Wilderness (None)";

        // Influence/Gold suppression (Income is derived from influence)
        int suppressionPenalty = Mathf.RoundToInt(15 * impact);

        switch (activeEvent)
        {
            case EventType.AcidRain:
                if (dealDamage) Debug.Log($"[EventManager] Acid Rain hitting {targetTile.cubeCoords}. Owner: {ownerName}");
                
                // Penalty to influence (and thus gold) - Always applied, but reported on turn start
                targetTile.influenceSuppression += suppressionPenalty;
                if (owner != null && dealDamage)
                {
                    Debug.Log($" > Economic Impact: Suppressing influence by {suppressionPenalty}. Gold revenue reduced for {ownerName}.");
                }

                if (dealDamage)
                {
                    if (targetTile.placedTower != null) 
                    {
                        float dmg = 5f * impact;
                        targetTile.placedTower.currentDurability -= dmg;
                        Debug.Log($" > Corroding Tower: {targetTile.placedTower.name} (-{dmg} HP)");
                    }
                    if (targetTile.placedWire != null) 
                    {
                        float dmg = 3f * impact;
                        targetTile.placedWire.currentDurability -= dmg;
                        Debug.Log($" > Corroding Wires: {targetTile.placedWire.name} (-{dmg} HP)");
                    }
                    if (targetTile.placedStructure != null)
                    {
                        float dmg = 4f * impact;
                        targetTile.placedStructure.TakeDamage(dmg);
                        Debug.Log($" > Corroding Structure: {targetTile.placedStructure.name} (-{dmg} HP)");
                    }
                }
                break;

            case EventType.SolarFlare:
                int solarSuppression = Mathf.RoundToInt(10 * impact);
                targetTile.influenceSuppression += solarSuppression;
                if (dealDamage) Debug.Log($"[EventManager] Solar Flare hitting {targetTile.cubeCoords}. Owner: {ownerName} | Suppressing influence by {solarSuppression}.");
                break;

            case EventType.PowerOutage:
                if (dealDamage) Debug.Log($"[EventManager] Thunderstorm over {targetTile.cubeCoords}. Owner: {ownerName}");
                
                // Penalty to influence (and thus gold income)
                targetTile.influenceSuppression += suppressionPenalty;
                if (owner != null && dealDamage)
                {
                    Debug.Log($" > Economic Impact: Suppressing influence by {suppressionPenalty}. Gold revenue reduced for {ownerName}.");
                }

                if (dealDamage)
                {
                    // 50% chance to CRITICALLY DESTROY buildings/infrastructure
                    float disasterRoll = Random.value;
                    if (disasterRoll < 0.5f)
                    {
                        if (targetTile.placedTower != null)
                        {
                            Debug.Log(" > [CRITICAL] Lightning struck the Tower! It is now DESTROYED.");
                            targetTile.placedTower.TakeDamage(999f); 
                        }
                        if (targetTile.placedStructure != null)
                        {
                            Debug.Log($" > [CRITICAL] Lightning struck {targetTile.placedStructure.name}! It is now BROKEN.");
                            targetTile.placedStructure.TakeDamage(999f);
                        }
                        if (targetTile.placedWire != null)
                        {
                            Debug.Log(" > [CRITICAL] Lightning struck the Wires! Grid short-circuit.");
                            targetTile.placedWire.TakeDamage(999f);
                        }
                    }

                    // 50% chance to kill unit on tile
                    if (targetTile.placedUnit != null)
                    {
                        if (Random.value < 0.5f)
                        {
                            Debug.Log($" > [DEATH] Unit {targetTile.placedUnit.name} struck by lightning and KILLED.");
                            targetTile.placedUnit.Die();
                        }
                        else
                        {
                            Debug.Log($" > Unit {targetTile.placedUnit.name} narrowly survived the lightning strike.");
                        }
                    }
                }
                break;

            case EventType.HyperInflation:
                // Always set flag so mid-turn expansions pick it up immediately
                targetTile.isHyperinflated = true;
                
                if (owner != null && dealDamage)
                {
                    Debug.Log($"[EventManager] Hyper-Inflation hits {targetTile.cubeCoords}! LOCAL GOLD BOOST ACTIVE for {owner.playerName} (+200% revenue contribution).");
                }
                else if (dealDamage)
                {
                    Debug.Log($"[EventManager] Hyper-Inflation hits {targetTile.cubeCoords}, but it's Wilderness. No one gains gold.");
                }
                break;
            
            case EventType.TechBoom:
                if (dealDamage) Debug.Log($"[EventManager] Tech Boom at {targetTile.cubeCoords}. Owner: {ownerName} | Visual explosion effect triggering.");
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
            case EventType.HyperInflation: prefab = hyperInflationParticlePrefab; break;
            case EventType.TechBoom: prefab = techBoomParticlePrefab; break;
        }

        if (prefab != null)
        {
            // Use the prefab's authored rotation so things like the horizontal SunRay stay horizontal
            Quaternion spawnRot = prefab.transform.rotation;
            Vector3 spawnPos = targetTile.transform.position;

            if (activeEvent == EventType.PowerOutage)
                spawnPos += Vector3.up * 4.11f;
            else if (activeEvent == EventType.AcidRain)
                spawnPos += Vector3.up * 3.85f;
            else if (activeEvent == EventType.TechBoom)
                spawnPos += Vector3.up * 2.79f;
            else if (activeEvent == EventType.HyperInflation)
                spawnPos += Vector3.up * 3.22f;
            else if (activeEvent == EventType.SolarFlare)
                spawnPos += Vector3.up * 2.74f;

            currentParticleSystem = Instantiate(prefab, spawnPos, spawnRot, transform);
            
            float hexScale = GridManager.Instance != null ? GridManager.Instance.hexSize : 1f;
            float maxAuthoredSize = 0.5f;

            // Force hierarchy scaling and find the authored shape's maximum extent
            foreach (var ps in currentParticleSystem.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                var shape = ps.shape;
                if (shape.enabled)
                {
                    if (shape.shapeType == ParticleSystemShapeType.Sphere || shape.shapeType == ParticleSystemShapeType.Circle || shape.shapeType == ParticleSystemShapeType.Hemisphere)
                    {
                        maxAuthoredSize = Mathf.Max(maxAuthoredSize, shape.radius * 2f);
                    }
                    else if (shape.shapeType == ParticleSystemShapeType.Box || shape.shapeType == ParticleSystemShapeType.Rectangle)
                    {
                        float boxMax = Mathf.Max(shape.scale.x, Mathf.Max(shape.scale.y, shape.scale.z));
                        maxAuthoredSize = Mathf.Max(maxAuthoredSize, boxMax);
                    }
                }

                // Make the Thunderstorm (PowerOutage) cloud 3D-ish by forcing it to face the camera as a Billboard instead of a flat ground decal
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && activeEvent == EventType.PowerOutage)
                {
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                }
            }

            // Fix the one-sided bug on Solar Flare by generating a full "X" cross of duplicates so it's visible perfectly from all 4 3D sides!
            if (activeEvent == EventType.SolarFlare)
            {
                Instantiate(prefab, spawnPos, spawnRot * Quaternion.Euler(0, 90, 0), currentParticleSystem.transform).transform.localScale = Vector3.one;
                Instantiate(prefab, spawnPos, spawnRot * Quaternion.Euler(0, 180, 0), currentParticleSystem.transform).transform.localScale = Vector3.one;
                Instantiate(prefab, spawnPos, spawnRot * Quaternion.Euler(0, 270, 0), currentParticleSystem.transform).transform.localScale = Vector3.one;
            }

            // Target size is roughly exactly one hex tile diameter
            float targetSize = hexScale * 1.75f; 
            float dynamicScale = targetSize / maxAuthoredSize;

            if (activeEvent == EventType.TechBoom)
            {
                currentParticleSystem.transform.localScale = new Vector3(0.8414741f, 0.8414741f, 0.8414741f);
                currentParticleSystem.AddComponent<ParticleLoopTimer>();
            }
            else
            {
                currentParticleSystem.transform.localScale = new Vector3(dynamicScale, dynamicScale, dynamicScale);
            }
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

public class ParticleLoopTimer : MonoBehaviour
{
    private ParticleSystem[] particleSystems;
    private Vector3 originalPosition;

    void Start()
    {
        particleSystems = GetComponentsInChildren<ParticleSystem>();
        originalPosition = transform.position;
        InvokeRepeating(nameof(PlayParticles), 0.5f, 0.5f);
    }

    void PlayParticles()
    {
        if (particleSystems == null) return;

        // Offset the explosion slightly on a different X or Y (or Z) each time it loops to make it look scattered!
        transform.position = originalPosition + new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));

        foreach (var ps in particleSystems)
        {
            if (ps != null) ps.Play();
        }
    }
}
