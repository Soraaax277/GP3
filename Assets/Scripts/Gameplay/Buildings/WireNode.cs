using UnityEngine;

public class WireNode : MonoBehaviour, IInfrastructure, IPowerable
{
    public HexTile    ParentTile { get; private set; }
    public PlayerData owner      { get; private set; }
    public bool       IsPowered  { get; set; }
    public bool       IsTechnicianActivated { get; set; }
    public bool       isDigital { get; private set; }

    [Header("Stats")]
    public float baseDurability = 100f;
    
    // Current health of the wire
    public float currentDurability;

    //  UPKEEP  (System 3)
    [Header("Upkeep")]
    public int goldUpkeep = 5; // Base gold subtracted from player per turn

    // Returns the base upkeep for this wire.
    // The era-mismatch multiplier is applied globally in EconomyManager.
    public int GetCurrentUpkeep()
    {
        return goldUpkeep;
    }

    //  DYNAMIC MAX DURABILITY  ("WireDurability" multiplier tech)
    //  TechEffect setup: infraStatName="WireDurability", isMultiplier=CHECKED
    //  e.g. value=0.5 → MaxDurability = 100 * 1.5 = 150 HP
    public float MaxDurability
    {
        get
        {
            if (TechManager.Instance == null) return baseDurability;
            return baseDurability * TechManager.Instance.GetInfraMultiplier(owner, "WireDurability");
        }
    }

    private Renderer[] visualRenderers;

    //  INITIALIZATION
    public void Initialize(HexTile tile, PlayerData player)
    {
        ParentTile      = tile;
        owner           = player;
        tile.placedWire = this;
       
        // Start with full health based on current Tech
        currentDurability = MaxDurability;

        CacheRenderers();
        UpdatePowerState(false);

        // Register with TurnManager here so ALL placed wires participate
        // in the decay system — not only those placed via PlaceWireDirect().
        // Previously, wires placed by a WireSpecialist (BuildWire → Initialize) were
        // never registered, so they silently skipped decay each turn.
        if (TurnManager.Instance != null)
            TurnManager.Instance.RegisterWire(this);

        if (PowerGridManager.Instance != null)
            PowerGridManager.Instance.RefreshGrid();

        // QUEST HOOKS
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.SetQuestFlag(owner, "LaidWire");
            if (tile.type == HexTile.TileType.Water)
                QuestManager.Instance.SetQuestFlag(owner, "WireDifficultTerrain");
        }
    }

    void CacheRenderers()
    {
        visualRenderers = GetComponentsInChildren<Renderer>();
        
        // If no children discovered (e.g. freshly instantiated without child mesh yet), 
        // create the emergency cylinder
        if (visualRenderers == null || visualRenderers.Length == 0)
        {
            GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.transform.SetParent(transform);
            cyl.transform.localPosition = Vector3.zero;
            cyl.transform.localScale = new Vector3(0.3f, 0.05f, 0.3f);
            Destroy(cyl.GetComponent<Collider>());
            visualRenderers = new Renderer[] { cyl.GetComponent<Renderer>() };
        }
    }

    //  POWER STATE
    public void UpdatePowerState(bool powered)
    {
        if (isDestroyed) return;
        
        bool wasPoweredAlready = IsPowered;
        IsPowered = powered;
        
        if (visualRenderers != null)
        {
            Color targetColor = Color.gray;
            if (powered && IsTechnicianActivated)
            {
                targetColor = Color.yellow; // Grid power + Licensed tech = Active
            }
            else if (IsTechnicianActivated)
            {
                targetColor = new Color(0.4f, 0.4f, 1.0f); // Blue/Cyan = Licensed but NO GRID power
            }
            else if (powered)
            {
                targetColor = isDigital ? new Color(0.5f, 0.9f, 0.9f) : new Color(0.75f, 0.75f, 0.75f); // Grid power but NO license
            }
            
            if (isDigital && powered && IsTechnicianActivated)
            {
                targetColor = new Color(0f, 0.8f, 1f); // Neon Cyan for Digital Active
            }
            
            foreach (Renderer r in visualRenderers)
            {
                if (r != null) r.material.color = targetColor;
            }

            // SFX: Play power-up sound when grid power reaches this wire segment
            if (powered && !wasPoweredAlready && owner != null && !owner.isAI && 
                AudioManager.Instance != null && AudioManager.Instance.powerSFX != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.powerSFX);
            }
        }
    }

    //  DAMAGE & DECAY
    // Called for immediate damage (explosions, sabotage)
    public void TakeDamage(float amount)
    {
        // FIX (Bug 3): Guard against damage on already-destroyed wires, consistent
        // with TowerNode.TakeDamage which returns early on TowerState.Destroyed.
        if (isDestroyed) return;

        currentDurability -= amount;
        CheckDestruction();
    }

    // Called every turn by TurnManager for natural rot
    public void DecayWire()
    {
        float degradationRate = 5f; // Base HP lost per turn

        // Changed from GetInfraMultiplier → GetInfraFlatBonus to match
        // TowerNode.ProcessTurnDecay and the TechNode Inspector setup.
        // TechEffect: infraStatName="WireDegradation", isMultiplier=UNCHECKED.
        //
        // Was (degradationRate * multiplier) — a multiplier > 1.0 made wires
        // decay FASTER (inverted). Now SUBTRACTED so more bonus = less decay.
        // e.g. base 5 HP/turn - bonus 1.0 = 4 HP/turn lost.
        float degradationReduction = 0f;
        if (TechManager.Instance != null)
            degradationReduction = TechManager.Instance.GetInfraFlatBonus(owner, "WireDegradation");

        float finalRate = Mathf.Max(0f, degradationRate - degradationReduction);

        currentDurability -= finalRate;

        Debug.Log($"[WireDecay] {name}: base {degradationRate} - reduction {degradationReduction} " +
                  $"= -{finalRate} HP | Remaining: {currentDurability}/{MaxDurability}");

        CheckDestruction();
    }

    public bool isDestroyed { get; private set; }

    private void CheckDestruction()
    {
        if (currentDurability <= 0 && !isDestroyed)
        {
            isDestroyed = true;
            IsPowered = false;
            Debug.Log($"[WireDecay] {name} destroyed!");
            
            if (visualRenderers != null)
            {
                foreach (Renderer r in visualRenderers)
                {
                    if (r != null) r.material.color = Color.black;
                }
            }

            // Notify grid to recalculate
            if (PowerGridManager.Instance != null)
                PowerGridManager.Instance.RefreshGrid();
        }
    }

    // -----------------------------------------------------------------------
    //  Called by a Technician unit to restore a destroyed wire.
    //  Mirrors the pattern used by StructureNode.Repair() and TowerNode.Repair().
    //  efficiencyMultiplier: 1.0 = full HP restore; higher values from tech bonuses
    //  (e.g. TechEffect infraStatName="RepairEfficiency") allow overheal up to cap.
    // -----------------------------------------------------------------------
    public void Repair(float efficiencyMultiplier = 1.0f)
    {
        if (!isDestroyed) return;

        isDestroyed = false;
        currentDurability = Mathf.Min(MaxDurability * efficiencyMultiplier, MaxDurability);

        Debug.Log($"[Wire] {name} repaired. Durability: {currentDurability}/{MaxDurability}");

        // Restore visual to unpowered (grey) state — power is re-evaluated by the grid
        if (visualRenderers != null)
        {
            foreach (Renderer r in visualRenderers)
            {
                if (r != null) r.material.color = Color.gray;
            }
        }

        // Re-enter the power grid so connected towers/wires can become active again
        if (PowerGridManager.Instance != null)
            PowerGridManager.Instance.RefreshGrid();
    }

    public void UpgradeToDigital()
    {
        if (isDigital) return;
        isDigital = true;
        
        if (owner != null && QuestManager.Instance != null)
        {
            // We use a specific flag structure for counting: 'DigitalWireCount_{playerId}'
            string key = "DigitalWireCount_" + owner.playerId;
            int count = PlayerPrefs.GetInt(key, 0) + 1;
            PlayerPrefs.SetInt(key, count);
            
            if (count >= 3)
            {
                QuestManager.Instance.SetQuestFlag(owner, "Upgraded3WiresDigital");
            }
        }
        
        UpdatePowerState(IsPowered);
    }

    // -----------------------------------------------------------------------
    //  WIRE LENGTH  ("WireLength" flat bonus tech)
    //  Controls how far from the existing network a new wire can be placed.
    //  TechEffect setup: infraStatName="WireLength", isMultiplier=UNCHECKED, value=2
    //
    //  NOTE (Bug 4): This static method measures distance from the nearest owned
    //  network tile. WirePlacementManager instead measures from the specialist unit
    //  via its own MaxWireLength property — those are two different validations.
    //  This method is provided for any future system that needs the network-distance
    //  version (e.g. AI wire-chain logic). Do NOT use this to validate specialist reach.
    // -----------------------------------------------------------------------
    // Returns the maximum allowed hex distance between a new wire tile and the
    // nearest existing owned network tile (node / wire / powered tower).
    // Base = 1. Increased by the "WireLength" flat bonus tech upgrade.
    public static int GetMaxWireLengthFromNetwork(PlayerData player)
    {
        int baseLength = 1;
        if (TechManager.Instance == null) return baseLength;
        return baseLength + Mathf.RoundToInt(TechManager.Instance.GetInfraFlatBonus(player, "WireLength"));
    }

    // -----------------------------------------------------------------------
    //  WIRE COST  ("WireCost" multiplier tech)
    //  Controls the gold cost per wire placement.
    //  TechEffect setup: infraStatName="WireCost", isMultiplier=CHECKED, value=-0.1
    //  (negative = cheaper). The base multiplier is 1.0, so -0.1 → 0.9 = 10% cheaper.
    //  Hook this into WirePlacementManager's purchase logic to take effect.
    // -----------------------------------------------------------------------
    // Returns the actual gold cost to place one wire tile after tech discounts.
    // Pass WirePlacementManager's base wire cost as the argument.
    // e.g. GetPlacementCost(20) with a -0.1 WireCost tech applied = 18 gold.
    public static int GetPlacementCost(PlayerData player, int baseCost)
    {
        if (TechManager.Instance == null) return baseCost;
        float multiplier = TechManager.Instance.GetInfraMultiplier(player, "WireCost");
        return Mathf.Max(0, Mathf.RoundToInt(baseCost * multiplier));
    }
}