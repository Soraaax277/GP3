using UnityEngine;

public class UnitSpawner : MonoBehaviour
{
    public static UnitSpawner Instance;

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

        GameObject unitObj = Instantiate(unitPrefab);
        Unit unit = unitObj.GetComponent<Unit>();
        unit.Initialize(spawnTile, business.owner);
        return unit;
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
