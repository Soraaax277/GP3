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
            state.currentEra = TurnManager.Instance.GetCurrentEra().ToString();
            
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

        if (GridManager.Instance != null)
        {
            state.mapSeedX = GridManager.Instance.mapOffsetX;
            state.mapSeedY = GridManager.Instance.mapOffsetY;
        }

        SaveUnits(state);
        SaveBuildings(state);
        SaveStructures(state);
        SaveTowers(state);
        SaveWires(state);
        SaveInfluence(state);

        if (QuestManager.Instance != null)
        {
            state.questState = QuestManager.Instance.GetQuestState();
        }

        string json = JsonUtility.ToJson(state);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();

        Debug.Log("Game saved successfully");
    }

    /// <summary>
    /// Peek at the save file to extract only the seeds so the map can be seeded before total load.
    /// </summary>
    public static bool TryPeekMapSeeds(out float x, out float y)
    {
        x = 0; y = 0;
        if (!PlayerPrefs.HasKey(SAVE_KEY)) return false;
        try
        {
            string json = PlayerPrefs.GetString(SAVE_KEY);
            GameState state = JsonUtility.FromJson<GameState>(json);
            x = state.mapSeedX;
            y = state.mapSeedY;
            return true;
        }
        catch { return false; }
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

        try { LoadUnits(state); } catch (System.Exception e) { Debug.LogError("Error loading units: " + e.Message); }
        try { LoadBuildings(state); } catch (System.Exception e) { Debug.LogError("Error loading buildings: " + e.Message); }
        try { LoadStructures(state); } catch (System.Exception e) { Debug.LogError("Error loading structures: " + e.Message); }
        try { LoadTowers(state); } catch (System.Exception e) { Debug.LogError("Error loading towers: " + e.Message); }
        try { LoadWires(state); } catch (System.Exception e) { Debug.LogError("Error loading wires: " + e.Message); }
        try { LoadInfluence(state); } catch (System.Exception e) { Debug.LogError("Error loading influence: " + e.Message); }

        if (QuestManager.Instance != null && state.questState != null)
        {
            try { QuestManager.Instance.LoadQuestState(state.questState); } catch (System.Exception e) { Debug.LogError("Error loading quests: " + e.Message); }
        }

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.currentTurn = state.currentTurn;
            TurnManager.Instance.ResumeFromSave(state.currentPlayerIndex);
        }

        Debug.Log("Game loaded successfully (with safety checks)");
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
                specialCharges = charges,
                level = unit.level
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

    private static void SaveStructures(GameState state)
    {
        if (GridManager.Instance == null) return;

        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.placedStructure != null)
            {
                StructureNode node = tile.placedStructure;
                StructureData data = new StructureData
                {
                    structureType = node.GetType().Name,
                    featureKey = node.GetRequiredTechFeature(),
                    tileX = tile.cubeCoords.x,
                    tileY = tile.cubeCoords.y,
                    isPlayerOwned = node.owner == TurnManager.Instance.players[0],
                    isBuilt = node.IsBuilt,
                    isBroken = node.IsBroken,
                    currentDurability = node.currentDurability
                };
                state.structures.Add(data);
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
                    rotationY = wire.transform.rotation.eulerAngles.y,
                    
                    posX = wire.transform.position.x,
                    posY = wire.transform.position.y,
                    posZ = wire.transform.position.z,
                    
                    rotX = wire.transform.rotation.x,
                    rotY = wire.transform.rotation.y,
                    rotZ = wire.transform.rotation.z,
                    rotW = wire.transform.rotation.w,
                    
                    sclX = wire.transform.localScale.x,
                    sclY = wire.transform.localScale.y,
                    sclZ = wire.transform.localScale.z,
                    
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
                    enemyInfluence = enemyInfluence,
                    influenceSuppression = tile.influenceSuppression
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
            unit.level = data.level; // Restore veterancy
            
            if (unit is BuilderUnit builder) builder.buildsRemaining = data.specialCharges;
            else if (unit is Technician tech) tech.actionCharges = data.specialCharges;
            else if (unit is RoboWorker robo) robo.buildsRemaining = data.specialCharges;
            else if (unit is SalesMarketer marketer) marketer.marketingCharges = data.specialCharges;
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

    private static void LoadStructures(GameState state)
    {
        ClearAllStructures();

        foreach (var structureData in state.structures)
        {
            HexTile tile = GridManager.Instance.GetTile(structureData.tileX, structureData.tileY);
            if (tile == null) continue;

            PlayerData owner = structureData.isPlayerOwned ? 
                TurnManager.Instance.players[0] : TurnManager.Instance.players[1];

            GameObject prefab = GetStructurePrefab(structureData.structureType);
            if (prefab == null) continue;

            GameObject structureObj = Object.Instantiate(prefab);
            StructureNode structure = structureObj.GetComponent<StructureNode>();
            if (structure != null)
            {
                tile.ClearEnvironmentalStructures(); // Prevent random decor clipping loaded buildings
                
                structure.Initialize(tile, owner);
                if (structureData.isBuilt) structure.Build();
                structure.currentDurability = structureData.currentDurability;
                // Note: isBroken state could be further restored if StructureNode has set/load logic
            }
        }
    }

    private static GameObject GetStructurePrefab(string structureType)
    {
        var spm = StructurePlacementManager.Instance;
        if (spm == null) spm = Object.FindFirstObjectByType<StructurePlacementManager>();
        if (spm == null) return null;

        switch (structureType)
        {
            case "ServiceCenter": return spm.serviceCenterPrefab;
            case "AdvancedServiceCenter": return spm.advancedServiceCenterPrefab;
            case "BPOCenter": return spm.bpoCenterPrefab;
            case "Tesseract": return spm.tesseractPrefab;
            case "SignalBooster": return spm.signalBoosterPrefab;
            case "SignalJammer": return spm.signalJammerPrefab;
            case "PowerBox": return spm.powerBoxPrefab;
            case "CommercialHub": return spm.commercialHubPrefab;
            case "BusinessCenter": return spm.businessCenterPrefab;
            case "AdvancedBusinessCenter": return spm.advancedBusinessCenterPrefab;
            case "WorkerFactory": return spm.workerFactoryPrefab;
            case "DroneFactory": return spm.droneFactoryPrefab;
            case "Rocketship": return spm.rocketshipPrefab;
            case "Canteen": return spm.canteenPrefab;
            default: return null;
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
                tile.ClearEnvironmentalStructures(); // Prevent random decor clipping loaded towers

                TowerNode tower = towerManager.PlaceTowerDirect(tile, owner, parentNode);
                if (tower != null)
                {
                    // Call Build() to transition it out of Hologram state natively
                    if (towerData.state == "Powered" || towerData.state == "Constructed") 
                    {
                        tower.Build();
                    }
                    tower.currentDurability = towerData.currentDurability;
                    // If powered, grid refresh will sync its visuals/status on the first turn 
                }
            }
        }
    }

    private static void LoadWires(GameState state)
    {
        ClearAllWires();

        WirePlacementManager wireManager = Object.FindFirstObjectByType<WirePlacementManager>();
        
        foreach (var wireData in state.wires)
        {
            HexTile tile = GridManager.Instance.GetTile(wireData.tileX, wireData.tileY);
            if (tile == null) continue;

            PlayerData owner = wireData.isPlayerOwned ? 
                TurnManager.Instance.players[0] : TurnManager.Instance.players[1];

            if (wireManager != null)
            {
                tile.ClearEnvironmentalStructures(); // Prevent random decor clipping loaded wires

                WireNode wire = wireManager.PlaceWireDirect(tile, owner);
                if (wire != null)
                {
                    // If exact rotation/position isn't 0 (from an old save), restore absolute transforms
                    bool hasLegacySave = (wireData.sclX == 0f && wireData.sclY == 0f && wireData.sclZ == 0f);
                    
                    if (!hasLegacySave)
                    {
                        wire.transform.position = new Vector3(wireData.posX, wireData.posY, wireData.posZ);
                        wire.transform.rotation = new Quaternion(wireData.rotX, wireData.rotY, wireData.rotZ, wireData.rotW);
                        wire.transform.localScale = new Vector3(wireData.sclX, wireData.sclY, wireData.sclZ);
                    }
                    else if (wireData.rotationY != 0f)
                    {
                        // Fallback to previous patch rotation logic
                        wire.transform.rotation = Quaternion.Euler(0f, wireData.rotationY, 90f);
                    }

                    wire.currentDurability = wireData.currentDurability;
                    wire.gameObject.SetActive(true); // Force visibility in case prefab was disabled
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
                tile.influenceSuppression = influenceData.influenceSuppression;
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
        TurnManager.Instance.GetAllUnits().Clear();
    }

    private static void ClearAllBuildings()
    {
        if (GridManager.Instance == null || TurnManager.Instance == null) return;

        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.placedNode != null)
            {
                Object.Destroy(tile.placedNode.gameObject);
                tile.placedNode = null;
            }
        }
        
        foreach (var player in TurnManager.Instance.players)
        {
            player.ownedNodes.Clear();
        }
    }

    private static void ClearAllStructures()
    {
        if (GridManager.Instance == null || TurnManager.Instance == null) return;

        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.placedStructure != null)
            {
                Object.Destroy(tile.placedStructure.gameObject);
                tile.placedStructure = null;
            }
        }
        TurnManager.Instance.GetAllStructures().Clear();
    }

    private static void ClearAllTowers()
    {
        if (GridManager.Instance == null || TurnManager.Instance == null) return;

        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.placedTower != null)
            {
                Object.Destroy(tile.placedTower.gameObject);
                tile.placedTower = null;
            }
        }
        TurnManager.Instance.GetAllTowers().Clear();
    }

    private static void ClearAllWires()
    {
        if (GridManager.Instance == null || TurnManager.Instance == null) return;

        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.placedWire != null)
            {
                Object.Destroy(tile.placedWire.gameObject);
                tile.placedWire = null;
            }
        }
        TurnManager.Instance.GetAllWires().Clear();
    }
}