using UnityEngine;

public class UnitSpawner : MonoBehaviour
{
    public static UnitSpawner Instance;

    public GameObject wireSpecialistPrefab;
    public GameObject builderPrefab;
    public GameObject salesMarketerPrefab;
    public GameObject technicianPrefab;
    public GameObject scoutPrefab;
    public GameObject maintenanceCrewPrefab;
    public GameObject foremenPrefab;
    public GameObject itPersonnelPrefab;
    public GameObject businessmanPrefab;
    public GameObject roboMarshallPrefab;
    public GameObject roboWorkerPrefab;
    public GameObject saboteurPrefab;

    private void Awake()
    {
        Instance = this;
    }

    public Unit SpawnUnit(GameObject unitPrefab, HexTile centerTile, PlayerData owner)
    {
        HexTile spawnTile = GetAdjacentFreeTile(centerTile);

        if (spawnTile == null)
        {
            Debug.LogWarning("No free tile to spawn unit!");
            return null;
        }

        // Calculate recruitment cost with tech modifier
        int recruitmentCost = GetRecruitmentCost(unitPrefab);
        
        if (owner.resources < recruitmentCost)
        {
            Debug.LogWarning($"Not enough gold to recruit unit! Need {recruitmentCost}, have {owner.resources}");
            return null;
        }

        // Deduct recruitment cost
        owner.resources -= recruitmentCost;
        Debug.Log($"[UnitSpawner] Unit recruited for {recruitmentCost} gold. Remaining: {owner.resources}");

        GameObject unitObj = Instantiate(unitPrefab);
        Unit unit = unitObj.GetComponent<Unit>();
        unit.Initialize(spawnTile, owner);

        if (TechManager.Instance != null)
        {
            TechManager.Instance.ApplyEffectsToNewUnit(unit);
        }

        return unit;
    }

    public int GetRecruitmentCost(GameObject unitPrefab, PlayerData player = null)
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
                case "ScoutUnit":
                    baseCost = 45;
                    break;
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
                case "Businessman":
                    baseCost = 90;
                    break;
                case "Saboteurs":
                    baseCost = 110;
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
            float multiplier = TechManager.Instance.GetInfraMultiplier(player, "RecruitmentCost");
            return Mathf.Max(0, Mathf.RoundToInt(baseCost * multiplier));
        }
        
        return baseCost;
    }

    HexTile GetAdjacentFreeTile(HexTile centerTile)
    {
        // Check all neighboring tiles (radius 1)
        var neighbors = GridManager.Instance.GetNeighbors(centerTile);
        foreach (HexTile tile in neighbors)
        {
            if (tile == null) continue;
            
            // A tile is valid if it has no unit, no tower, and no structure
            // (Note: HexTile.IsWalkable() might be too strict as it excludes any tile with hasStructure=true,
            // even if the structure was just cleared. Checking raw occupation flags is safer here.)
            bool isOccupied = tile.IsOccupied() || tile.HasTower() || tile.hasStructure || tile.placedUnit != null;
            
            if (!isOccupied && tile.type == HexTile.TileType.Land)
                return tile;
        }

        // If neighbors are full, try a slightly wider search (radius 2) to prevent softlocks
        var widerArea = GridManager.Instance.GetTilesInRange(centerTile, 2);
        foreach (HexTile tile in widerArea)
        {
            if (tile == centerTile || neighbors.Contains(tile)) continue;
            
            bool isOccupied = tile.IsOccupied() || tile.HasTower() || tile.hasStructure || tile.placedUnit != null;
            if (!isOccupied && tile.type == HexTile.TileType.Land)
                return tile;
        }

        return null;
    }
}