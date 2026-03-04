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

    public virtual void Initialize(HexTile tile, PlayerData player)
    {
        ParentTile = tile;
        owner = player;
        currentDurability = baseDurability;
        
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
        currentDurability -= amount;
        if (currentDurability <= 0) DestroyStructure();
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
