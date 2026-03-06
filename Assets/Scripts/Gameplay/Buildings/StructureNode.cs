using UnityEngine;

public abstract class StructureNode : MonoBehaviour, IInfrastructure, IPowerable
{
    public HexTile ParentTile { get; protected set; }
    public PlayerData owner { get; protected set; }
    public bool IsPowered { get; set; }
    public bool IsTechnicianActivated { get; set; }

    [Header("Base Stats")]
    public float baseDurability = 100f;
    public float currentDurability;
    public int goldUpkeep = 10;

    [Header("Hidden Stats")]
    public float hiddenDurability = 50f;
    public float currentHiddenDurability;
    public bool IsBroken { get; protected set; }

    public virtual void Initialize(HexTile tile, PlayerData player)
    {
        ParentTile = tile;
        owner = player;
        currentDurability = baseDurability;
        currentHiddenDurability = hiddenDurability;
        IsBroken = false;
        
        tile.hasStructure = true; 
        tile.placedStructure = this;
        
        if (TurnManager.Instance != null)
            TurnManager.Instance.RegisterStructure(this);
            
        UpdatePowerState(false);
    }

    public virtual void UpdatePowerState(bool powered)
    {
        IsPowered = powered;
        // Visual feedback for power state can be implemented in subclasses
    }

    public virtual void OnTurnStart() 
    {
        // Subclasses can implement income, production, etc.
    }

    public virtual void TakeDamage(float amount)
    {
        if (IsBroken)
        {
            currentHiddenDurability -= amount;
            if (currentHiddenDurability <= 0) DestroyStructure();
        }
        else
        {
            currentDurability -= amount;
            if (currentDurability <= 0) 
            {
                BreakStructure();
            }
        }
    }

    protected virtual void BreakStructure()
    {
        IsBroken = true;
        currentDurability = 0;
        IsPowered = false;
        Debug.Log($"[Structure] {name} is BROKEN and needs repair!");
        
        // Visual feedback: darkening
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null) rend.material.color = Color.Lerp(rend.material.color, Color.black, 0.5f);
    }

    public virtual void Repair(float amount)
    {
        if (IsBroken)
        {
            IsBroken = false;
            currentHiddenDurability = hiddenDurability;
            Debug.Log($"[Structure] {name} has been REPAIRED!");
        }
        currentDurability = Mathf.Min(currentDurability + amount, baseDurability);
        
        // Reset color if subclass doesn't handle visuals
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null) rend.material.color = Color.white; 
    }

    protected virtual void DestroyStructure()
    {
        if (ParentTile != null) ParentTile.hasStructure = false;
        Destroy(gameObject);
    }

    public virtual int GetCurrentUpkeep() => goldUpkeep;

    // Optional: Tech check for unlocking the building
    public abstract string GetRequiredTechFeature();
}
