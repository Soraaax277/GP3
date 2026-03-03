using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class SaveSystem
{
    private const string SAVE_KEY = "GameSaveData";

    public static void SaveGame()
    {
        GameState state = new GameState();

        if (TurnManager.Instance != null)
        {
            state.currentTurn = TurnManager.Instance.currentTurn;
            state.currentEra = TurnManager.Instance.GetCurrentEra();
        }

        if (TurnManager.Instance != null && TurnManager.Instance.players != null && TurnManager.Instance.players.Count >= 2)
        {
            state.playerResources = TurnManager.Instance.players[0].resources;
            state.enemyResources = TurnManager.Instance.players[1].resources;
        }

        SaveUnits(state);
        SaveBuildings(state);
        SaveTowers(state);
        SaveWires(state);
        SaveInfluence(state);

        string json = JsonUtility.ToJson(state);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();

        Debug.Log("Game saved successfully");
    }

    public static bool LoadGame()
    {
        if (!HasSaveData())
        {
            Debug.LogWarning("No save data found");
            return false;
        }

        string json = PlayerPrefs.GetString(SAVE_KEY);
        GameState state = JsonUtility.FromJson<GameState>(json);

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.currentTurn = state.currentTurn;
        }

        if (TurnManager.Instance != null && TurnManager.Instance.players != null && TurnManager.Instance.players.Count >= 2)
        {
            TurnManager.Instance.players[0].resources = state.playerResources;
            TurnManager.Instance.players[1].resources = state.enemyResources;
        }

        LoadUnits(state);
        LoadBuildings(state);
        LoadTowers(state);
        LoadWires(state);
        LoadInfluence(state);

        Debug.Log("Game loaded successfully");
        return true;
    }

    public static bool HasSaveData()
    {
        return PlayerPrefs.HasKey(SAVE_KEY);
    }

    public static void DeleteSave()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();
        Debug.Log("Save data deleted");
    }

    private static void SaveUnits(GameState state)
    {
        if (TurnManager.Instance == null) return;

        foreach (var unit in TurnManager.Instance.GetAllUnits())
        {
            if (unit == null || unit.currentTile == null) continue;

            UnitData data = new UnitData
            {
                unitType = unit.GetType().Name,
                tileX = unit.currentTile.cubeCoords.x,
                tileY = unit.currentTile.cubeCoords.y,
                canAct = unit.CanAct,
                movementRemaining = unit.movementRemaining
            };

            if (unit.owner == TurnManager.Instance.players[0])
                state.playerUnits.Add(data);
            else
                state.enemyUnits.Add(data);
        }
    }

    private static void SaveBuildings(GameState state)
    {
        if (GridManager.Instance == null) return;

        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.placedNode != null)
            {
                SignalNode node = tile.placedNode;
                BuildingData data = new BuildingData
                {
                    buildingType = "SignalNode",
                    tileX = tile.cubeCoords.x,
                    tileY = tile.cubeCoords.y,
                    towersPlaced = node.towersPlacedCount,
                    isPlayerOwned = node.owner == TurnManager.Instance.players[0]
                };
                state.buildings.Add(data);
            }
        }
    }

    private static void SaveTowers(GameState state)
    {
        if (GridManager.Instance == null) return;

        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.placedTower != null)
            {
                TowerNode tower = tile.placedTower;
                TowerData data = new TowerData
                {
                    tileX = tile.cubeCoords.x,
                    tileY = tile.cubeCoords.y,
                    state = tower.state.ToString(),
                    isPlayerOwned = tower.owner == TurnManager.Instance.players[0],
                    parentNodeX = tower.parentNode != null ? tower.parentNode.ParentTile.cubeCoords.x : -1,
                    parentNodeY = tower.parentNode != null ? tower.parentNode.ParentTile.cubeCoords.y : -1
                };
                state.towers.Add(data);
            }
        }
    }

    private static void SaveWires(GameState state)
    {
        if (GridManager.Instance == null) return;

        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.placedWire != null)
            {
                WireNode wire = tile.placedWire;
                WireData data = new WireData
                {
                    tileX = tile.cubeCoords.x,
                    tileY = tile.cubeCoords.y,
                    isPlayerOwned = wire.owner == TurnManager.Instance.players[0]
                };
                state.wires.Add(data);
            }
        }
    }

    private static void SaveInfluence(GameState state)
    {
        if (GridManager.Instance == null || TurnManager.Instance == null) return;

        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            int playerInfluence = tile.GetInfluence(TurnManager.Instance.players[0]);
            int enemyInfluence = tile.GetInfluence(TurnManager.Instance.players[1]);

            if (playerInfluence > 0 || enemyInfluence > 0)
            {
                TileInfluenceData data = new TileInfluenceData
                {
                    tileX = tile.cubeCoords.x,
                    tileY = tile.cubeCoords.y,
                    playerInfluence = playerInfluence,
                    enemyInfluence = enemyInfluence
                };
                state.tileInfluences.Add(data);
            }
        }
    }

    private static void LoadUnits(GameState state)
    {
        ClearAllUnits();

        if (TurnManager.Instance == null || UnitSpawner.Instance == null) return;
        if (TurnManager.Instance.players == null || TurnManager.Instance.players.Count < 2) 
        {
            Debug.LogWarning("SaveSystem: Could not load units because players are not yet initialized.");
            return;
        }

        foreach (var unitData in state.playerUnits)
        {
            SpawnUnitFromData(unitData, TurnManager.Instance.players[0]);
        }

        foreach (var unitData in state.enemyUnits)
        {
            SpawnUnitFromData(unitData, TurnManager.Instance.players[1]);
        }
    }

    private static void SpawnUnitFromData(UnitData data, PlayerData owner)
    {
        HexTile tile = GridManager.Instance.GetTile(data.tileX, data.tileY);
        if (tile == null) return;

        GameObject unitPrefab = GetUnitPrefab(data.unitType);
        if (unitPrefab == null)
        {
            Debug.LogWarning($"Could not find prefab for unit type: {data.unitType}");
            return;
        }

        GameObject unitObj = Object.Instantiate(unitPrefab);
        Unit unit = unitObj.GetComponent<Unit>();
        if (unit != null)
        {
            unit.Initialize(tile, owner);
            unit.movementRemaining = (int)data.movementRemaining;
            if (!data.canAct)
            {
                unit.canAct = false;
            }
        }
    }

    private static GameObject GetUnitPrefab(string unitType)
    {
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        if (spawner == null) return null;

        switch (unitType)
        {
            case "WireSpecialist":
                return spawner.wireSpecialistPrefab;
            case "BuilderUnit":
                return spawner.builderPrefab;
            case "SalesMarketer":
                return spawner.salesMarketerPrefab;
            case "Technician":
                return spawner.technicianPrefab;
            default:
                return null;
        }
    }

    private static void LoadBuildings(GameState state)
    {
        ClearAllBuildings();

        foreach (var buildingData in state.buildings)
        {
            HexTile tile = GridManager.Instance.GetTile(buildingData.tileX, buildingData.tileY);
            if (tile == null) continue;

            PlayerData owner = buildingData.isPlayerOwned ? 
                TurnManager.Instance.players[0] : TurnManager.Instance.players[1];

            BusinessSpawner spawner = Object.FindFirstObjectByType<BusinessSpawner>();
            if (spawner != null)
            {
                SignalNode node = spawner.SpawnBusiness(tile, owner);
                if (node != null)
                {
                    node.towersPlacedCount = buildingData.towersPlaced;
                }
            }
        }
    }

    private static void LoadTowers(GameState state)
    {
        ClearAllTowers();

        foreach (var towerData in state.towers)
        {
            HexTile tile = GridManager.Instance.GetTile(towerData.tileX, towerData.tileY);
            if (tile == null) continue;

            PlayerData owner = towerData.isPlayerOwned ? 
                TurnManager.Instance.players[0] : TurnManager.Instance.players[1];

            HexTile parentTile = GridManager.Instance.GetTile(towerData.parentNodeX, towerData.parentNodeY);
            SignalNode parentNode = parentTile?.placedNode;

            TowerPlacementManager towerManager = Object.FindFirstObjectByType<TowerPlacementManager>();
            if (towerManager != null)
            {
                TowerNode tower = towerManager.PlaceTowerDirect(tile, owner, parentNode);
                if (tower != null && towerData.state == "Built")
                {
                    tower.SetBuilt();
                }
            }
        }
    }

    private static void LoadWires(GameState state)
    {
        ClearAllWires();

        foreach (var wireData in state.wires)
        {
            HexTile tile = GridManager.Instance.GetTile(wireData.tileX, wireData.tileY);
            if (tile == null) continue;

            PlayerData owner = wireData.isPlayerOwned ? 
                TurnManager.Instance.players[0] : TurnManager.Instance.players[1];

            WirePlacementManager wireManager = Object.FindFirstObjectByType<WirePlacementManager>();
            if (wireManager != null)
            {
                wireManager.PlaceWireDirect(tile, owner);
            }
        }
    }

    private static void LoadInfluence(GameState state)
    {
        if (GridManager.Instance == null || TurnManager.Instance == null) return;

        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            tile.ClearInfluence();
        }

        foreach (var influenceData in state.tileInfluences)
        {
            HexTile tile = GridManager.Instance.GetTile(influenceData.tileX, influenceData.tileY);
            if (tile != null)
            {
                tile.SetInfluence(TurnManager.Instance.players[0], influenceData.playerInfluence);
                tile.SetInfluence(TurnManager.Instance.players[1], influenceData.enemyInfluence);
            }
        }
    }

    private static void ClearAllUnits()
    {
        if (TurnManager.Instance == null) return;

        foreach (var unit in TurnManager.Instance.GetAllUnits().ToArray())
        {
            if (unit != null)
            {
                Object.Destroy(unit.gameObject);
            }
        }
    }

    private static void ClearAllBuildings()
    {
        if (GridManager.Instance == null) return;

        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.placedNode != null)
            {
                Object.Destroy(tile.placedNode.gameObject);
                tile.placedNode = null;
            }
        }
    }

    private static void ClearAllTowers()
    {
        if (GridManager.Instance == null) return;

        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.placedTower != null)
            {
                Object.Destroy(tile.placedTower.gameObject);
                tile.placedTower = null;
            }
        }
    }

    private static void ClearAllWires()
    {
        if (GridManager.Instance == null) return;

        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.placedWire != null)
            {
                Object.Destroy(tile.placedWire.gameObject);
                tile.placedWire = null;
            }
        }
    }
}
