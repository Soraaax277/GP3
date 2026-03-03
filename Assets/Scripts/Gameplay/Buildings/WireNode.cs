using UnityEngine;

public class WireNode : MonoBehaviour, IInfrastructure, IPowerable
{
    public HexTile    ParentTile { get; private set; }
    public PlayerData owner      { get; private set; }
    public bool       IsPowered  { get; set; }
    public bool       IsTechnicianActivated { get; set; }

    [Header("Stats")]
    public float baseDurability = 100f;
    
    // Current health of the wire
    public float currentDurability;

    //  UPKEEP  (System 3)
    [Header("Upkeep")]
    public int goldUpkeep = 5; // Base gold subtracted from player per turn

    /// Returns the base upkeep for this wire.
    /// The era-mismatch multiplier is applied globally in EconomyManager.
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
            return baseDurability * TechManager.Instance.GetInfraMultiplier("WireDurability");
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

        if (PowerGridManager.Instance != null)
        {
            PowerGridManager.Instance.RefreshGrid();
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
                targetColor = new Color(0.75f, 0.75f, 0.75f); // Grid power but NO license
            }
            
            foreach (Renderer r in visualRenderers)
            {
                if (r != null) r.material.color = targetColor;
            }
        }
    }

    //  DAMAGE & DECAY
    // Called for immediate damage (explosions, sabotage)
    public void TakeDamage(float amount)
    {
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
            degradationReduction = TechManager.Instance.GetInfraFlatBonus("WireDegradation");

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
    //  WIRE LENGTH  ("WireLength" flat bonus tech)
    //  Controls how far from the existing network a new wire can be placed.
    //  TechEffect setup: infraStatName="WireLength", isMultiplier=UNCHECKED, value=2
    //  Hook this into WirePlacementManager's adjacency check to take effect.
    // -----------------------------------------------------------------------
    /// Returns the maximum allowed hex distance between a new wire tile and the
    /// nearest existing owned network tile (node / wire / powered tower).
    /// Base = 1. Increased by the "WireLength" flat bonus tech upgrade.
    /// Call from WirePlacementManager when validating placement.
    public static int GetMaxWireLength()
    {
        int baseLength = 1;
        if (TechManager.Instance == null) return baseLength;
        return baseLength + Mathf.RoundToInt(TechManager.Instance.GetInfraFlatBonus("WireLength"));
    }

    // -----------------------------------------------------------------------
    //  WIRE COST  ("WireCost" multiplier tech)
    //  Controls the gold cost per wire placement.
    //  TechEffect setup: infraStatName="WireCost", isMultiplier=CHECKED, value=-0.1
    //  (negative = cheaper). The base multiplier is 1.0, so -0.1 → 0.9 = 10% cheaper.
    //  Hook this into WirePlacementManager's purchase logic to take effect.
    // -----------------------------------------------------------------------
    /// Returns the actual gold cost to place one wire tile after tech discounts.
    /// Pass WirePlacementManager's base wire cost as the argument.
    /// e.g. GetPlacementCost(20) with a -0.1 WireCost tech applied = 18 gold.
    public static int GetPlacementCost(int baseCost)
    {
        if (TechManager.Instance == null) return baseCost;
        float multiplier = TechManager.Instance.GetInfraMultiplier("WireCost");
        return Mathf.Max(0, Mathf.RoundToInt(baseCost * multiplier));
    }
}