using UnityEngine;

public class UnitSpawner : MonoBehaviour
{
    public static UnitSpawner Instance;

    public GameObject wireSpecialistPrefab;
    public GameObject builderPrefab;
    public GameObject salesMarketerPrefab;
    public GameObject technicianPrefab;

    private void Awake()
    {
        Instance = this;
    }

    public Unit SpawnUnit(GameObject unitPrefab, SignalNode business)
    {
        HexTile spawnTile = GetAdjacentFreeTile(business.tile);

        if (spawnTile == null)
        {
            Debug.LogWarning("No free tile to spawn unit!");
            return null;
        }

        // Calculate recruitment cost with tech modifier
        int recruitmentCost = GetRecruitmentCost(unitPrefab);
        
        if (business.owner.resources < recruitmentCost)
        {
            Debug.LogWarning($"Not enough gold to recruit unit! Need {recruitmentCost}, have {business.owner.resources}");
            return null;
        }

        // Deduct recruitment cost
        business.owner.resources -= recruitmentCost;
        Debug.Log($"[UnitSpawner] Unit recruited for {recruitmentCost} gold. Remaining: {business.owner.resources}");

        GameObject unitObj = Instantiate(unitPrefab);
        Unit unit = unitObj.GetComponent<Unit>();
        unit.Initialize(spawnTile, business.owner);

        if (TechManager.Instance != null)
        {
            TechManager.Instance.ApplyEffectsToNewUnit(unit);
        }

        return unit;
    }

    public int GetRecruitmentCost(GameObject unitPrefab)
    {
        // Base costs per unit type
        int baseCost = 50; // Default
        
        Unit unitComponent = unitPrefab.GetComponent<Unit>();
        if (unitComponent != null)
        {
            string unitType = unitComponent.GetType().Name;
            
            // Set base costs based on unit type
            switch (unitType)
            {
                case "BuilderUnit":
                    baseCost = 50;
                    break;
                case "WireSpecialist":
                    baseCost = 40;
                    break;
                case "Technician":
                    baseCost = 60;
                    break;
                case "SalesMarketer":
                    baseCost = 70;
                    break;
                case "MaintenanceCrew":
                    baseCost = 80;
                    break;
                case "Foremen":
                    baseCost = 100;
                    break;
                case "ITPersonnel":
                    baseCost = 120;
                    break;
                case "RoboWorker":
                    baseCost = 150;
                    break;
                case "RoboMarshall":
                    baseCost = 180;
                    break;
                default:
                    baseCost = 50;
                    break;
            }
        }
        
        // Apply tech modifier
        if (TechManager.Instance != null)
        {
            float multiplier = TechManager.Instance.GetInfraMultiplier("RecruitmentCost");
            return Mathf.Max(0, Mathf.RoundToInt(baseCost * multiplier));
        }
        
        return baseCost;
    }

    HexTile GetAdjacentFreeTile(HexTile centerTile)
    {
        var tiles = GridManager.Instance.GetTilesInRange(centerTile, 1);
        foreach (HexTile tile in tiles)
        {
            if (!tile.IsOccupied() && !tile.HasTower())
                return tile;
        }
        return null;
    }
}