using UnityEngine;

public class TowerNode : MonoBehaviour, IInfrastructure, IPowerable
{
    public HexTile ParentTile => tile;
    public bool IsPowered { get; set; }

    //  THREE-PHASE CONSTRUCTION STATES  (System 3)
    public enum TowerState
    {
        Hologram,       // Just placed — digital ghost, 5 % influence
        Constructed,    // Built by a Builder unit — 20 % influence
        Powered,        // Wired by a Technician unit — 100 % signal strength
        Destroyed       // Needs repair before it can operate again
    }

    public PlayerData owner      { get; private set; }
    public SignalNode parentNode { get; private set; }
    public HexTile    tile;
    
    [Header("Stats")]
    public int   baseRange      = 3;
    public int   baseRevenue    = 10; 
    public float baseDurability = 100f; // CONSTANT: Always 100

    [Header("Upkeep")]
    public int goldUpkeep = 25; // Base gold subtracted from player per turn (System 3)
    
    private bool isRecruited = false;
    //  SIGNAL  (System 2)
    // Set each turn by SignalNode.PropagateSignal().
    // Only meaningful when state == Powered.
    public float receivedSignalStrength { get; set; } = 0f;

    //  DYNAMIC PROPERTIES  (unchanged)
    public int CurrentRange
    {
        get
        {
            if (TechManager.Instance == null) return baseRange;

            float bonus      = TechManager.Instance.GetInfraFlatBonus("TowerRange");
            float multiplier = TechManager.Instance.GetInfraMultiplier("TowerRange");

            // FIX: Prevent multiplication by zero if tech is not researched
            if (multiplier <= 0) multiplier = 1f;

            int finalRange = Mathf.RoundToInt((baseRange + bonus) * multiplier);
            return Mathf.Max(1, finalRange);
        }
    }

    public int CurrentRevenue
    {
        get
        {
            if (TechManager.Instance == null) return baseRevenue;
            
            float multiplier = TechManager.Instance.GetInfraMultiplier("TowerRevenue");
            if (multiplier <= 0) multiplier = 1f;
            return Mathf.RoundToInt(baseRevenue * multiplier);
        }
    }
    
    public float currentDurability;

    public TowerState state { get; private set; }
    private GameObject rangeIndicator;
    
    //  REPAIR TRACKING (for First-Time Repair Bonus - Advanced Repair Tools)
    private bool hasBeenRepaired = false;
    
    public bool HasBeenRepairedBefore() => hasBeenRepaired;
    public void MarkAsRepaired() => hasBeenRepaired = true;

    //  INITIALIZATION
    void IInfrastructure.Initialize(HexTile hexTile, PlayerData player) 
        => Initialize(hexTile, player, null);

    public void Initialize(HexTile hexTile, PlayerData player, SignalNode parent = null)
    {
        tile       = hexTile;
        owner      = player;
        parentNode = parent;
        tile.placedTower = this;

        // HP is always 100. Upgrades now affect the DECAY calculation, not Max HP.
        currentDurability = baseDurability;

        if (parentNode != null)
            parentNode.towersPlacedCount++;

        TurnManager.Instance.RegisterTower(this);

        // System 3: towers begin as Holograms
        state = TowerState.Hologram;

        // VISUALS: Start as an actual hologram (transparent blue)
        HologramUtil.MakeHologram(gameObject, new Color(0f, 0.5f, 1f, 0.35f));

        CreateRangeIndicator();
        SetRangeColor(new Color(0f, 0.5f, 1f, 0.25f));
        ShowRange(false);

        if (PowerGridManager.Instance != null)
        {
            PowerGridManager.Instance.RefreshGrid();
        }
    }

    // Helper for external scripts to get revenue
    public int GetCurrentRevenue()
    {
        return CurrentRevenue;
    }

    //  UPKEEP  (System 3)
    // Returns the base upkeep for this tower.
    // The era-mismatch multiplier is applied globally in EconomyManager.
    // Destroyed towers do not incur upkeep.
    public int GetCurrentUpkeep()
    {
        return state == TowerState.Destroyed ? 0 : goldUpkeep;
    }

    // -----------------------------------------------------------------------
    //  INFLUENCE HELPERS  (System 1 + 2 + 3)
    // -----------------------------------------------------------------------

    // Returns the influence output fraction based on construction phase.
    // Hologram = 5 %, Constructed = 20 %, Powered = 100 % (uses signal).
    private float GetStateInfluenceMultiplier()
    {
        switch (state)
        {
            case TowerState.Hologram:    return 0.05f;
            case TowerState.Constructed: return 0.20f;
            case TowerState.Powered:     return 1.00f;
            default:                     return 0f;
        }
    }

    //  RANGE INDICATOR
    public void CreatePreview()
    {
        CreateRangeIndicator();
        ShowRange(true);
    }

    void CreateRangeIndicator()
    {
        if (rangeIndicator != null) return;

        rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rangeIndicator.transform.SetParent(transform);
        rangeIndicator.transform.localPosition = new Vector3(0f, 0.05f, 0f); // Reverted to original height
        rangeIndicator.transform.localRotation = Quaternion.identity;

        UpdateRangeVisuals();

        Renderer rend = rangeIndicator.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));

        Destroy(rangeIndicator.GetComponent<Collider>());
    }

    public void UpdateRangeVisuals()
    {
        if (rangeIndicator == null) return;

        // Hex spacing math (Size * 1.732)
        float hexSpacing   = GridManager.Instance.hexSize * 1.732f;
        float visualRadius = CurrentRange * hexSpacing;

        rangeIndicator.transform.localScale = 
            new Vector3(visualRadius * 2f, 0.01f, visualRadius * 2f);
    }

    //  CONSTRUCTION PHASES  (System 3)
    // Called by a Builder unit. Advances tower from Hologram → Constructed.
    public void Build()
    {
        if (state == TowerState.Constructed || state == TowerState.Powered)
            return;

        if (state == TowerState.Destroyed)
        {
            Debug.LogWarning("Cannot Build a Destroyed tower — use Repair() instead.");
            return;
        }

        state = TowerState.Constructed;

        // VISUALS: Transition from hologram to solid
        HologramUtil.MakeSolid(gameObject);
        SetRangeColor(new Color(0f, 1f, 0f, 0.25f)); // Green when built

        // Reset durability on fresh build
        currentDurability = baseDurability;

        ApplyInfluence();

        if (PowerGridManager.Instance != null)
        {
            PowerGridManager.Instance.RefreshGrid();
        }

        Debug.Log("Tower constructed (20% influence — awaiting Technician to power)");
    }

    // Called by a Technician unit. Advances tower from Constructed → Powered.
    // Powered towers transmit full Signal Strength as influence.
    public void Power()
    {
        if (state != TowerState.Constructed)
        {
            Debug.LogWarning($"Tower.Power() called in invalid state: {state}. " +
                             "Tower must be Constructed first.");
            return;
        }

        state     = TowerState.Powered;
        IsPowered = true;

        ApplyInfluence();

        if (PowerGridManager.Instance != null)
        {
            PowerGridManager.Instance.RefreshGrid();
        }

        Debug.Log("Tower powered by Technician — transmitting at full signal strength!");
    }

    /// Direct state override used by the save system or editor tooling.
    public void SetBuilt()
    {
        state = TowerState.Constructed;
    }

    /// True if the tower is at least Constructed (functional in any capacity).
    public bool IsBuilt()
    {
        return state == TowerState.Constructed || state == TowerState.Powered;
    }

    public bool IsDestroyed()
    {
        return state == TowerState.Destroyed;
    }

    //  RANGE INDICATOR HELPERS
    public void SetRangeColor(Color color)
    {
        if (rangeIndicator == null) return;
        rangeIndicator.GetComponent<Renderer>().material.color = color;
    }

    public void ShowRange(bool show)
    {
        if (rangeIndicator != null)
        {
            if (show) UpdateRangeVisuals(); 
            rangeIndicator.SetActive(show);
        }
    }

    private void OnMouseEnter()
    {
        ShowRange(true);
    }

    private void OnMouseExit()
    {
        ShowRange(false);
    }

    //  INFLUENCE  (System 1 + 2 + 3)
    void ApplyInfluence()
    {
        // Destroyed towers generate nothing
        if (state == TowerState.Destroyed) return;

        float stateMultiplier = GetStateInfluenceMultiplier();
        if (stateMultiplier <= 0f) return;

        // System 1: obsolete hardware reduces influence output
        float eraMultiplier = 1f;
        if (TurnManager.Instance != null && owner != null)
        {
            eraMultiplier = TurnManager.Instance.GetEraInfluenceMultiplier(owner);
        }

        var tilesInRange = GridManager.Instance.GetTilesInRange(tile, CurrentRange);

        foreach (HexTile t in tilesInRange)
        {
            float influenceAmount;

            if (state == TowerState.Powered && receivedSignalStrength > 0f)
            {
                // System 2: Powered towers output the actual received signal as influence.
                // stateMultiplier is 1.0 for Powered, so this is full signal * era debuff.
                influenceAmount = receivedSignalStrength * stateMultiplier * eraMultiplier;
            }
            else
            {
                // Hologram / Constructed: scale the tile's base influence value instead.
                influenceAmount = t.baseInfluence * stateMultiplier * eraMultiplier;
            }

            t.AddInfluence(owner, Mathf.RoundToInt(influenceAmount));

            Debug.Log($"{t.name} +{influenceAmount:F1} influence → {owner.playerName} " +
                      $"[{state} | era×{eraMultiplier:F2}]");
        }

        if (TurnManager.Instance != null)
            TurnManager.Instance.NotifyStatusChanged();
    }

    //  POWER STATE (called by PowerGridManager)
    public void UpdatePowerState(bool powered)
    {
        bool wasPowered = IsPowered;
        IsPowered = powered;

        // Sync TowerState with the power grid:
        //   Grid connects  → promote Constructed → Powered automatically
        //   Grid disconnects → demote Powered back to Constructed
        if (powered && state == TowerState.Constructed)
        {
            state = TowerState.Powered;
        }
        else if (!powered && state == TowerState.Powered)
        {
            state = TowerState.Constructed;
        }

        // Nothing to colour if hologram or destroyed
        if (state == TowerState.Destroyed || state == TowerState.Hologram) return;

        if (powered)
        {
            SetRangeColor(new Color(0f, 1f, 0f, 0.25f));
            if (!wasPowered) ApplyInfluence();
        }
        else
        {
            // Set to Green instead of Orange/Red as requested
            SetRangeColor(new Color(0f, 0.8f, 0f, 0.2f)); // Dimmer green for unpowered
            if (wasPowered) RemoveInfluence();
        }
    }

    void RemoveInfluence()
    {
        var tilesInRange = GridManager.Instance.GetTilesInRange(tile, CurrentRange);

        foreach (HexTile t in tilesInRange)
        {
            t.RemoveInfluence(owner, t.baseInfluence);
            Debug.Log($"{t.name} lost influence for {owner.playerName} (power cut)");
        }

        if (TurnManager.Instance != null)
            TurnManager.Instance.NotifyStatusChanged();
    }

    //  DAMAGE & DECAY
    public void TakeDamage(float amount)
    {
        if (state == TowerState.Hologram || state == TowerState.Destroyed) return;

        currentDurability -= amount;
        if (currentDurability <= 0)
        {
            DestroyTower();
        }
    }

    // <summary>
    // Deterministic per-turn HP decay.
    // Holograms are digital — they do not decay.
    // Constructed and Powered towers both degrade physically.
    // </summary>
    public void ProcessTurnDecay()
    {
        // Holograms and already-destroyed towers skip decay
        if (state == TowerState.Hologram || state == TowerState.Destroyed) return;

        // 1. BASE DECAY (50% of Max HP)
        float decayPercent = 0.50f;

        // 2. APPLY WIRE UPGRADES (reduces the %)
        if (TechManager.Instance != null)
        {
            float degradationReduction = TechManager.Instance.GetInfraFlatBonus("WireDegradation");
            decayPercent -= degradationReduction;
        }
        
        // Clamp min decay to 5 % so it never becomes immortal
        decayPercent = Mathf.Max(decayPercent, 0.05f);

        // Calculate Raw Damage
        float rawDamage = baseDurability * decayPercent;

        // 3. APPLY TOWER STRENGTH (reduces incoming damage)
        float resistance = 1.0f;
        if (TechManager.Instance != null)
        {
            resistance = TechManager.Instance.GetInfraMultiplier("TowerDurability");
        }
        
        // Safety check to prevent divide by zero
        if (resistance < 1.0f) resistance = 1.0f;

        float finalDamage = rawDamage / resistance;

        // 4. APPLY DAMAGE
        currentDurability -= finalDamage;

        Debug.Log($"[DECAY] {name}: BaseRate {decayPercent * 100}% | " +
                  $"Resistance x{resistance} | Taken: -{finalDamage} HP | " +
                  $"Remaining: {currentDurability}/100 | State: {state}");

        if (currentDurability <= 0)
        {
            DestroyTower();
        }
    }

    //  REPAIR & DESTRUCTION
    /// Repairs a Destroyed tower back to Constructed state.
    /// A Technician must call Power() again to restore full signal output.
    public void Repair(float efficiencyMultiplier = 1.0f)
    {
        if (state != TowerState.Destroyed) return;

        // Repaired to Constructed — Technician needs to re-wire for full power
        state = TowerState.Constructed;
        
        // VISUALS: Restore solid appearance
        HologramUtil.MakeSolid(gameObject);

        // Apply repair efficiency from tech upgrades
        // Base repair restores to 100 HP, efficiency upgrades increase this
        // e.g., +50% efficiency = 150 HP restored
        float restoredHP = baseDurability * efficiencyMultiplier;
        currentDurability = Mathf.Min(restoredHP, baseDurability); // Cap at max HP

        ApplyInfluence();

        if (PowerGridManager.Instance != null)
        {
            PowerGridManager.Instance.RefreshGrid();
        }

        Debug.Log($"Tower repaired to Constructed state (restored {restoredHP} HP with {efficiencyMultiplier * 100}% efficiency). Technician required to restore full power.");
    }

    public void Recruit(PlayerData newOwner)
    {
        if (!isRecruited)
        {
            owner = newOwner;
        }
        isRecruited = true;
    }

    void DestroyTower()
    {
        state     = TowerState.Destroyed;
        IsPowered = false;
        ShowRange(false);
        SetRangeColor(new Color(1f, 0f, 0f, 0.25f));
        Debug.Log($"{name} has been destroyed and needs repair!");
    }
}