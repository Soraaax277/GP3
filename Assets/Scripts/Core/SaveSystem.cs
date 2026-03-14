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
            state.currentPlayerIndex = TurnManager.Instance.currentPlayerIndex;
            state.currentEra = TurnManager.Instance.GetCurrentEra();
            
            if (TurnManager.Instance.players.Count >= 2)
            {
                var p1 = TurnManager.Instance.players[0];
                var p2 = TurnManager.Instance.players[1];
                
                state.playerResources = p1.resources;
                state.playerResearchPoints = p1.researchPoints;
                state.playerHardwareEra = (int)p1.hardwareEra;
                state.playerWorkforceEra = (int)p1.workforceEra;
                
                state.enemyResources = p2.resources;
                state.enemyResearchPoints = p2.researchPoints;
                state.enemyHardwareEra = (int)p2.hardwareEra;
                state.enemyWorkforceEra = (int)p2.workforceEra;
            }
        }

        if (TechManager.Instance != null)
        {
            state.playerUnlockedTechs = TechManager.Instance.GetUnlockedNodes(TurnManager.Instance.players[0])
                .Select(n => n.techName).ToList();
            state.enemyUnlockedTechs = TechManager.Instance.GetUnlockedNodes(TurnManager.Instance.players[1])
                .Select(n => n.techName).ToList();
                
            var mDict = TechManager.Instance.GetInfraMultipliers();
            state.infraMultiplierKeys = mDict.Keys.ToList();
            state.infraMultiplierValues = mDict.Values.ToList();
            
            var fDict = TechManager.Instance.GetInfraFlatStats();
            state.infraFlatKeys = fDict.Keys.ToList();
            state.infraFlatValues = fDict.Values.ToList();

            // --- ACTIVE (IN-PROGRESS) RESEARCH ---
            // Serialize both players' research queues as parallel name/turns lists.
            var p1Research = TechManager.Instance.GetActiveResearchFor(TurnManager.Instance.players[0]);
            state.playerActiveResearchNames  = p1Research.Keys.Select(n => n.techName).ToList();
            state.playerActiveResearchTurns  = p1Research.Values.ToList();

            var p2Research = TechManager.Instance.GetActiveResearchFor(TurnManager.Instance.players[1]);
            state.enemyActiveResearchNames   = p2Research.Keys.Select(n => n.techName).ToList();
            state.enemyActiveResearchTurns   = p2Research.Values.ToList();
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

        if (TurnManager.Instance != null && TurnManager.Instance.players != null && TurnManager.Instance.players.Count >= 2)
        {
            var p1 = TurnManager.Instance.players[0];
            var p2 = TurnManager.Instance.players[1];
            
            p1.resources = state.playerResources;
            p1.researchPoints = state.playerResearchPoints;
            p1.hardwareEra = (TurnManager.PlayerEra)state.playerHardwareEra;
            p1.workforceEra = (TurnManager.PlayerEra)state.playerWorkforceEra;
            
            p2.resources = state.enemyResources;
            p2.researchPoints = state.enemyResearchPoints;
            p2.hardwareEra = (TurnManager.PlayerEra)state.enemyHardwareEra;
            p2.workforceEra = (TurnManager.PlayerEra)state.enemyWorkforceEra;
        }

        if (TechManager.Instance != null)
        {
            TechManager.Instance.LoadInfraStats(state.infraMultiplierKeys, state.infraMultiplierValues, state.infraFlatKeys, state.infraFlatValues);
            TechManager.Instance.LoadTechState(TurnManager.Instance.players[0], state.playerUnlockedTechs);
            TechManager.Instance.LoadTechState(TurnManager.Instance.players[1], state.enemyUnlockedTechs);

            // --- ACTIVE (IN-PROGRESS) RESEARCH ---
            // Restore queued research so ticking continues seamlessly from the save point.
            TechManager.Instance.LoadActiveResearch(
                TurnManager.Instance.players[0],
                state.playerActiveResearchNames,
                state.playerActiveResearchTurns);

            TechManager.Instance.LoadActiveResearch(
                TurnManager.Instance.players[1],
                state.enemyActiveResearchNames,
                state.enemyActiveResearchTurns);
        }

        LoadUnits(state);
        LoadBuildings(state);
        LoadTowers(state);
        LoadWires(state);
        LoadInfluence(state);

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.currentTurn = state.currentTurn;
            TurnManager.Instance.ResumeFromSave(state.currentPlayerIndex);
        }

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

            int charges = 0;
            if (unit is BuilderUnit builder) charges = builder.buildsRemaining;
            else if (unit is Technician tech) charges = tech.actionCharges;

            UnitData data = new UnitData
            {
                unitType = unit.GetType().Name,
                tileX = unit.currentTile.cubeCoords.x,
                tileY = unit.currentTile.cubeCoords.y,
                canAct = unit.canAct,
                movementRemaining = unit.movementRemaining,
                specialCharges = charges
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
                    parentNodeY = tower.parentNode != null ? tower.parentNode.ParentTile.cubeCoords.y : -1,
                    currentDurability = tower.currentDurability
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
                    isPlayerOwned = wire.owner == TurnManager.Instance.players[0],
                    currentDurability = wire.currentDurability
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
            unit.canAct = data.canAct;
            
            if (unit is BuilderUnit builder) builder.buildsRemaining = data.specialCharges;
            else if (unit is Technician tech) tech.actionCharges = data.specialCharges;
        }
    }

    private static GameObject GetUnitPrefab(string unitType)
    {
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        if (spawner == null) return null;

        switch (unitType)
        {
            case "WireSpecialist":  return spawner.wireSpecialistPrefab;
            case "BuilderUnit":     return spawner.builderPrefab;
            case "SalesMarketer":   return spawner.salesMarketerPrefab;
            case "Technician":      return spawner.technicianPrefab;
            case "ScoutUnit":       return spawner.scoutPrefab;
            case "MaintenanceCrew": return spawner.maintenanceCrewPrefab;
            case "Foremen":         return spawner.foremenPrefab;
            case "ITPersonnel":     return spawner.itPersonnelPrefab;
            case "Businessman":     return spawner.businessmanPrefab;
            case "RoboMarshall":    return spawner.roboMarshallPrefab;
            case "RoboWorker":      return spawner.roboWorkerPrefab;
            case "Saboteurs":       return spawner.saboteurPrefab;
            default:                return null;
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
                if (tower != null)
                {
                    if (towerData.state == "Built") tower.SetBuilt();
                    tower.currentDurability = towerData.currentDurability;
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
                WireNode wire = wireManager.PlaceWireDirect(tile, owner);
                if (wire != null)
                {
                    wire.currentDurability = wireData.currentDurability;
                }
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