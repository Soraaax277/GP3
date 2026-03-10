using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class EnemyAI : MonoBehaviour
{
    public static EnemyAI Instance;

    [Header("Core Prefabs")]
    public GameObject wireSpecialistPrefab;
    public GameObject builderPrefab;
    public GameObject towerPrefab;
    public GameObject salesMarketerPrefab;
    public GameObject technicianPrefab;
    public GameObject scoutPrefab;

    [Header("Workforce Prefabs (Service Center)")]
    public GameObject foremenPrefab;
    public GameObject maintenanceCrewPrefab;
    public GameObject itPersonnelPrefab;

    [Header("Marketing Prefabs")]
    public GameObject businessmanPrefab;
    public GameObject saboteurPrefab;

    [Header("Late-Game Prefabs")]
    public GameObject roboWorkerPrefab;
    public GameObject roboMarshallPrefab;

    [Header("Structure Prefabs")]
    public GameObject serviceCenterPrefab;
    public GameObject bpoCenterPrefab;
    public GameObject commercialHubPrefab;
    public GameObject businessCenterPrefab;
    public GameObject signalBoosterPrefab;
    public GameObject signalJammerPrefab;
    public GameObject tesseractPrefab;
    public GameObject workerFactoryPrefab;
    public GameObject powerBoxPrefab;
    public GameObject droneFactoryPrefab;
    public GameObject rocketshipPrefab;
    public GameObject advBusinessCenterPrefab;
    public GameObject advServiceCenterPrefab;
    public GameObject canteenPrefab;

    [Header("Costs")]
    public int wireSpecialistCost  = 25;
    public int builderCost         = 50;
    public int salesMarketerCost   = 30;
    public int technicianCost      = 20;
    public int scoutCost           = 45;
    public int foremenCost         = 100;
    public int maintenanceCrewCost = 80;
    public int itPersonnelCost     = 120;
    public int businessmanCost     = 90;
    public int saboteurCost        = 110;
    public int roboWorkerCost      = 150;
    public int roboMarshallCost    = 180;
    public int powerBoxCost        = 100;
    public int droneFactoryCost    = 400;
    public int rocketshipCost      = 1000;
    public int advBusinessCenterCost = 500;
    public int advServiceCenterCost = 450;
    public int canteenCost          = 250;

    [Header("Tech Research")]
    public TechNode[] allTechNodes;

    private void Awake()
    {
        Instance = this;
    }

    public void ExecuteTurn(PlayerData aiPlayer)
    {
        StartCoroutine(AITurnRoutine(aiPlayer));
    }

    // =========================================================================
    //  MAIN AI TURN ROUTINE
    // =========================================================================
    private IEnumerator AITurnRoutine(PlayerData aiPlayer)
    {
        Debug.Log($"[EnemyAI] {aiPlayer.playerName} turn start. Resources: {aiPlayer.resources}");
        yield return new WaitForSeconds(1.0f);

        // --- PHASE 0: RESEARCH TECH ---
        yield return StartCoroutine(ResearchPhase(aiPlayer));

        // --- PHASE 1: PLACE TOWER BLUEPRINTS ---
        TowerBlueprintPhase(aiPlayer);
        yield return new WaitForSeconds(0.5f);


        // --- PHASE 2: PLACE STRUCTURES ---
        yield return StartCoroutine(StructurePlacementPhase(aiPlayer));

        // --- PHASE 3: RECRUIT UNITS ---
        yield return StartCoroutine(RecruitmentPhase(aiPlayer));

        // --- PHASE 4: COMMAND UNITS ---
        yield return StartCoroutine(CommandPhase(aiPlayer));

        Debug.Log("[EnemyAI] Turn complete.");
        yield return new WaitForSeconds(1.0f);
        TurnManager.Instance.EndTurn();
    }

    // =========================================================================
    //  PHASE 0: TECH RESEARCH
    // =========================================================================
    private IEnumerator ResearchPhase(PlayerData aiPlayer)
    {
        if (TechManager.Instance == null || allTechNodes == null) yield break;

        // AI researches up to 2 techs per turn
        int researchedThisTurn = 0;

        // First, check for essential construction techs if not already unlocked
        bool canBuild = TechManager.Instance.IsFeatureUnlockedFor(aiPlayer, "Construction") || 
                        TechManager.Instance.IsFeatureUnlockedFor(aiPlayer, "MinimumWageContract");

        if (!canBuild)
        {
            foreach (TechNode tech in allTechNodes)
            {
                if (tech == null || TechManager.Instance.IsNodeUnlocked(aiPlayer, tech)) continue;
                if (!tech.CanUnlockFor(aiPlayer)) continue;

                // Simple check for construction-related names in tech
                bool isPriorityTech = tech.techName.Contains("Construction") || 
                                      tech.techName.Contains("Minimum Wage") ||
                                      tech.techName.Contains("Contract") ||
                                      tech.techName.Contains("Radio Broadcasting") || // Power Boxes
                                      tech.techName.Contains("Service Center");

                if (isPriorityTech && aiPlayer.researchPoints >= tech.researchCost && aiPlayer.resources >= tech.goldCost)
                {
                    TechManager.Instance.ResearchTech(tech);
                    researchedThisTurn++;
                    Debug.Log($"[EnemyAI] Prioritized Structural Research: {tech.techName}");
                    yield return new WaitForSeconds(0.3f);
                    break; 
                }
            }
        }

        // Normal research for remaining slots
        foreach (TechNode tech in allTechNodes)
        {
            if (tech == null || TechManager.Instance.IsNodeUnlocked(aiPlayer, tech)) continue;
            if (!tech.CanUnlockFor(aiPlayer)) continue;
            if (researchedThisTurn >= 2) break;

            if (aiPlayer.researchPoints >= tech.researchCost && aiPlayer.resources >= tech.goldCost)
            {
                TechManager.Instance.ResearchTech(tech);
                researchedThisTurn++;
                Debug.Log($"[EnemyAI] Researched: {tech.techName}");
                yield return new WaitForSeconds(0.3f);
            }
        }
    }

    // --- PHASE 1: PLACE TOWER BLUEPRINTS ---
    private void TowerBlueprintPhase(PlayerData aiPlayer)
    {
        bool canBuild = TechManager.Instance.IsFeatureUnlockedFor(aiPlayer, "Construction") || 
                        TechManager.Instance.IsFeatureUnlockedFor(aiPlayer, "MinimumWageContract");

        // Don't waste money/space on blueprints if we can't build them yet
        if (!canBuild) return;

        foreach (SignalNode node in aiPlayer.ownedNodes)
        {
            if (node.CanPlaceTower())
            {
                HexTile bestBlueprintTile = FindBestTowerSpot(node);
                if (bestBlueprintTile != null)
                {
                    Debug.Log($"[EnemyAI] Placing tower blueprint at {bestBlueprintTile.name}");
                    PlaceBlueprint(bestBlueprintTile, aiPlayer, node);
                }
            }
        }
    }

    // =========================================================================
    //  PHASE 2: STRUCTURE PLACEMENT
    // =========================================================================
    private IEnumerator StructurePlacementPhase(PlayerData aiPlayer)
    {
        if (TechManager.Instance == null) yield break;

        // Current counts
        var allStructures = TurnManager.Instance.GetAllStructures().Where(s => s != null && s.owner == aiPlayer).ToList();
        int serviceCenterCount = allStructures.Count(s => s is ServiceCenter);
        int hubCount          = allStructures.Count(s => s is CommercialHub);
        int bizCount          = allStructures.Count(s => s is BusinessCenter);
        int bpoCount          = allStructures.Count(s => s is BPOCenter);
        int factoryCount      = allStructures.Count(s => s is WorkerFactory);
        int tesseractCount    = allStructures.Count(s => s is Tesseract);
        int jammerCount       = allStructures.Count(s => s is SignalJammer);
        int boosterCount      = allStructures.Count(s => s is SignalBooster);
        int powerBoxCount     = allStructures.Count(s => s is PowerBox);
        int droneCount        = allStructures.Count(s => s is DroneFactory);
        int rocketCount       = allStructures.Count(s => s is Rocketship);
        int advBizCount       = allStructures.Count(s => s is AdvancedBusinessCenter);
        int advServCount      = allStructures.Count(s => s is AdvancedServiceCenter);
        int canteenCount      = allStructures.Count(s => s is Canteen);

        // Try to place up to 2 structures per turn if budget allows (3 if Futuristic)
        int builtThisTurn = 0;
        int buildLimit = (aiPlayer.hardwareEra == TurnManager.PlayerEra.Futuristic) ? 3 : 2;

        var structureOptions = new List<(GameObject prefab, string feature, int cost, int currentCount, int maxCount)>
        {
            (serviceCenterPrefab,   "ServiceCenter",          200, serviceCenterCount, 1), // One SC is usually enough for a while
            (commercialHubPrefab,   "CommercialHubs",         250, hubCount, 3),
            (businessCenterPrefab,  "BusinessCenters",        300, bizCount, 2),
            (signalBoosterPrefab,   "SignalBooster",          150, boosterCount, 4),
            (signalJammerPrefab,    "SignalJammers",          150, jammerCount, 4),
            (bpoCenterPrefab,       "BPOCenters",             400, bpoCount, 3),
            (workerFactoryPrefab,   "WorkerFactories",        500, factoryCount, 1),
            (tesseractPrefab,       "Tesseract",              600, tesseractCount, 1),
            (powerBoxPrefab,        "PowerBoxes",             powerBoxCost, powerBoxCount, 5),
            (droneFactoryPrefab,    "DroneFactories",         droneFactoryCost, droneCount, 1),
            (rocketshipPrefab,      "Rocketship",             rocketshipCost, rocketCount, 1),
            (advBusinessCenterPrefab, "AdvancedBusinessCenters", advBusinessCenterCost, advBizCount, 2),
            (advServiceCenterPrefab, "AdvancedServiceCenter",   advServCount, advServCount, 1),
            (canteenPrefab,         "Canteens",               canteenCost, canteenCount, 2),
        };

        // Shuffle list to diversify selection
        structureOptions = structureOptions.OrderBy(x => Random.value).ToList();

        foreach (var (prefab, feature, cost, currentCount, maxCount) in structureOptions)
        {
            if (prefab == null) continue;
            if (!TechManager.Instance.IsFeatureUnlockedFor(aiPlayer, feature)) continue;
            if (currentCount >= maxCount) continue;
            if (aiPlayer.resources < cost + 100) continue; // Keep reserve
            if (builtThisTurn >= buildLimit) break;

            HexTile bestTile = FindBestStructureSpot(aiPlayer);
            if (bestTile != null)
            {
                aiPlayer.resources -= cost;
                GameObject structure = Instantiate(prefab, bestTile.transform.position + Vector3.up * 1f, Quaternion.identity);
                StructureNode node = structure.GetComponent<StructureNode>();
                if (node != null) node.Initialize(bestTile, aiPlayer);
                Debug.Log($"[EnemyAI] Placed {feature} at {bestTile.name} for {cost} gold. #{currentCount+1}");
                builtThisTurn++;
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    private HexTile FindBestStructureSpot(PlayerData aiPlayer)
    {
        // Find a tile adjacent to the AI's owned network that is not occupied
        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.IsOccupied() || tile.placedStructure != null) continue;
            if (tile.type == HexTile.TileType.Water) continue;

            foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(tile))
            {
                if (neighbor.placedNode != null && neighbor.placedNode.owner == aiPlayer) return tile;
                if (neighbor.placedTower != null && neighbor.placedTower.owner == aiPlayer && neighbor.placedTower.IsBuilt()) return tile;
                if (neighbor.placedWire != null && neighbor.placedWire.owner == aiPlayer) return tile;
            }
        }
        return null;
    }

    // =========================================================================
    //  PHASE 3: UNIT RECRUITMENT
    // =========================================================================
    private IEnumerator RecruitmentPhase(PlayerData aiPlayer)
    {
        List<Unit> myUnits = TurnManager.Instance.GetAllUnits().Where(u => u != null && u.owner == aiPlayer).ToList();
        
        // Count specific types
        int builderCount     = myUnits.Count(u => u is BuilderUnit);
        int specialistCount  = myUnits.Count(u => u is WireSpecialist);
        int marketerCount    = myUnits.Count(u => u is SalesMarketer);
        int technicianCount  = myUnits.Count(u => u is Technician);
        int scoutCount       = myUnits.Count(u => u is ScoutUnit);
        int foremenCount     = myUnits.Count(u => u is Foremen);
        int maintenanceCount = myUnits.Count(u => u is MaintenanceCrew);
        int itCount          = myUnits.Count(u => u is ITPersonnel);
        int businessmanCount = myUnits.Count(u => u is Businessman);
        int saboteurCount    = myUnits.Count(u => u is Saboteurs);
        int roboWorkerCount  = myUnits.Count(u => u is RoboWorker);
        int roboMarshallCount= myUnits.Count(u => u is RoboMarshall);

        bool needsBuilder = GetUnbuiltTowers(aiPlayer).Any();
        bool needsRepair  = FindObjectsByType<TowerNode>(FindObjectsSortMode.None)
            .Any(t => t.owner == aiPlayer && t.IsDestroyed());
        bool needsActivation = FindObjectsByType<WireNode>(FindObjectsSortMode.None)
            .Any(w => w.owner == aiPlayer && !w.IsTechnicianActivated);

        var unlockedUnits = TechManager.Instance != null ? TechManager.Instance.GetUnlockedUnitNamesFor(aiPlayer) : new HashSet<string>();

        // ---- CORE UNITS (always needed) ----
        // Builder
        if (needsBuilder && (builderCount + foremenCount + roboWorkerCount) < 2)
            yield return TryRecruit(builderPrefab, aiPlayer, builderCost, "Builder");

        // Wire Specialist (keep 2)
        if (specialistCount < 2)
            yield return TryRecruit(wireSpecialistPrefab, aiPlayer, wireSpecialistCost, "WireSpecialist");

        // Sales Marketer — unlocked via CommercialHubs feature (C_ERA2 0) or explicit unit unlock
        bool salesMarketerUnlocked = TechManager.Instance != null &&
            (TechManager.Instance.IsFeatureUnlockedFor(aiPlayer, "CommercialHubs") ||
             unlockedUnits.Contains("SalesMarketer"));
        if (marketerCount < 1 && salesMarketerUnlocked)
            yield return TryRecruit(salesMarketerPrefab, aiPlayer, salesMarketerCost, "SalesMarketer");

        // Technician
        bool hasTesseract = PowerGridManager.Instance != null && PowerGridManager.Instance.HasTesseract(aiPlayer);
        if ((needsRepair || needsActivation) && (technicianCount + itCount + roboMarshallCount) < 1 && !hasTesseract)
            yield return TryRecruit(technicianPrefab, aiPlayer, technicianCost, "Technician");

        // ---- ADVANCED UNLOCKS ----

        // Scouts
        if (scoutCount < 1 && scoutPrefab != null && unlockedUnits.Contains("Scout"))
            yield return TryRecruit(scoutPrefab, aiPlayer, scoutCost, "Scout");

        // Workforce (Service Center / Canteen features)
        bool hasWorkforceBase = TechManager.Instance != null && 
                                (TechManager.Instance.IsFeatureUnlockedFor(aiPlayer, "ServiceCenter") || 
                                 TechManager.Instance.IsFeatureUnlockedFor(aiPlayer, "Canteens"));

        if (hasWorkforceBase)
        {
            // Foreman must ONLY be unlocked if Canteens is unlocked (for AI)
            bool foremenUnlocked = TechManager.Instance.IsFeatureUnlockedFor(aiPlayer, "Canteens");
            if (foremenCount < 1 && needsBuilder && foremenPrefab != null && foremenUnlocked)
                yield return TryRecruit(foremenPrefab, aiPlayer, foremenCost, "Foreman");

            if (maintenanceCount < 1 && maintenanceCrewPrefab != null && unlockedUnits.Contains("MaintenanceCrew"))
                yield return TryRecruit(maintenanceCrewPrefab, aiPlayer, maintenanceCrewCost, "MaintenanceCrew");

            // Prefab name is "ITPersonel" (one 'n', matches your asset)
            bool itUnlocked = unlockedUnits.Contains("ITPersonel") ||
                              TechManager.Instance.IsFeatureUnlockedFor(aiPlayer, "ServiceCenter");
            if (itCount < 1 && itPersonnelPrefab != null && itUnlocked)
                yield return TryRecruit(itPersonnelPrefab, aiPlayer, itPersonnelCost, "ITPersonel");
        }

        // Saboteurs
        if (saboteurCount < 1 && saboteurPrefab != null && TechManager.Instance != null && TechManager.Instance.IsSabotageTabUnlockedFor(aiPlayer))
            yield return TryRecruit(saboteurPrefab, aiPlayer, saboteurCost, "Saboteur");

        // Businessman — unlocked alongside SalesMarketer via CommercialHubs feature
        bool businessmanUnlocked = TechManager.Instance != null &&
            (TechManager.Instance.IsFeatureUnlockedFor(aiPlayer, "CommercialHubs") ||
             unlockedUnits.Contains("Businessman"));
        if (businessmanCount < 1 && businessmanPrefab != null && businessmanUnlocked)
            yield return TryRecruit(businessmanPrefab, aiPlayer, businessmanCost, "Businessman");

        // Robo Units
        if (TechManager.Instance != null && TechManager.Instance.IsFeatureUnlockedFor(aiPlayer, "WorkerFactories"))
        {
            if (roboWorkerCount < 2 && roboWorkerPrefab != null && unlockedUnits.Contains("RoboWorker"))
                yield return TryRecruit(roboWorkerPrefab, aiPlayer, roboWorkerCost, "RoboWorker");

            if (roboMarshallCount < 1 && roboMarshallPrefab != null && unlockedUnits.Contains("RoboMarshall"))
                yield return TryRecruit(roboMarshallPrefab, aiPlayer, roboMarshallCost, "RoboMarshall");
        }
    }

    private IEnumerator TryRecruit(GameObject prefab, PlayerData aiPlayer, int cost, string label)
    {
        if (prefab == null) yield break;

        // Ensure we keep some gold for building if we have unbuilt towers
        int reserve = GetUnbuiltTowers(aiPlayer).Any() ? 100 : 0;
        if (aiPlayer.resources < cost + reserve) yield break;

        // Pick a spawn location. Prioritize Canteen for workforce, Service Center for Maintenance, else HQ.
        HexTile spawnTile = null;
        if (prefab == foremenPrefab || prefab == builderPrefab || prefab == technicianPrefab)
        {
            var canteen = FindObjectsByType<Canteen>(FindObjectsSortMode.None).FirstOrDefault(c => c.owner == aiPlayer && c.IsPowered);
            if (canteen != null) spawnTile = canteen.ParentTile;
        }
        else if (prefab == maintenanceCrewPrefab || prefab == itPersonnelPrefab)
        {
            var sc = FindObjectsByType<ServiceCenter>(FindObjectsSortMode.None).FirstOrDefault(s => s.owner == aiPlayer && s.IsPowered);
            if (sc != null) spawnTile = sc.ParentTile;
        }

        // Default to a random HQ if no specialized structure found
        if (spawnTile == null && aiPlayer.ownedNodes.Count > 0)
        {
            SignalNode spawnNode = aiPlayer.ownedNodes[Random.Range(0, aiPlayer.ownedNodes.Count)];
            spawnTile = spawnNode.tile;
        }

        if (spawnTile != null)
        {
            Unit u = UnitSpawner.Instance.SpawnUnit(prefab, spawnTile, aiPlayer);
            if (u != null)
                Debug.Log($"[EnemyAI] ✓ Recruited {label} at {spawnTile.name}. Gold remaining: {aiPlayer.resources}");
            else
                Debug.LogWarning($"[EnemyAI] SpawnUnit returned null for {label} at {spawnTile.name} (tile occupied?)");
        }
        else
        {
            Debug.LogWarning($"[EnemyAI] No spawn tile found for {label}!");
        }

        yield return new WaitForSeconds(0.5f);
    }

    // =========================================================================
    //  PHASE 4: COMMAND ALL UNITS
    // =========================================================================
    private IEnumerator CommandPhase(PlayerData aiPlayer)
    {
        List<Unit> myUnits = TurnManager.Instance.GetAllUnits().Where(u => u != null && u.owner == aiPlayer).ToList();

        // --- Builders ---
        foreach (var builder in myUnits.OfType<BuilderUnit>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleBuilder(builder));
            yield return new WaitForSeconds(0.5f);
        }

        // --- Foremen (also construct towers) ---
        foreach (var foreman in myUnits.OfType<Foremen>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleForemen(foreman));
            yield return new WaitForSeconds(0.5f);
        }

        // --- RoboWorkers (also construct towers) ---
        foreach (var robo in myUnits.OfType<RoboWorker>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleRoboWorker(robo));
            yield return new WaitForSeconds(0.5f);
        }

        // --- Wire Specialists ---
        foreach (var specialist in myUnits.OfType<WireSpecialist>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleWireSpecialist(specialist));
            yield return new WaitForSeconds(0.5f);
        }

        // --- Technicians (power wires + repair) ---
        foreach (var technician in myUnits.OfType<Technician>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleTechnician(technician));
            yield return new WaitForSeconds(0.5f);
        }

        // --- Sales Marketers ---
        foreach (var marketer in myUnits.OfType<SalesMarketer>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleSalesMarketer(marketer));
            yield return new WaitForSeconds(0.5f);
        }

        // --- Scouts (move toward unexplored territory) ---
        foreach (var scout in myUnits.OfType<ScoutUnit>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleScout(scout));
            yield return new WaitForSeconds(0.5f);
        }

        // --- IT Personnel (repair) ---
        foreach (var it in myUnits.OfType<ITPersonnel>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleITPersonnel(it));
            yield return new WaitForSeconds(0.5f);
        }

        // --- Maintenance Crew (repair) ---
        foreach (var crew in myUnits.OfType<MaintenanceCrew>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleMaintenanceCrew(crew));
            yield return new WaitForSeconds(0.5f);
        }

        // --- RoboMarshall (repair) ---
        foreach (var marshall in myUnits.OfType<RoboMarshall>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleRoboMarshall(marshall));
            yield return new WaitForSeconds(0.5f);
        }

        // --- Saboteurs (damage enemy structures) ---
        foreach (var sab in myUnits.OfType<Saboteurs>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleSaboteur(sab));
            yield return new WaitForSeconds(0.5f);
        }

        // --- Businessmen (recruit enemy towers) ---
        foreach (var biz in myUnits.OfType<Businessman>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleBusinessman(biz));
            yield return new WaitForSeconds(0.5f);
        }
    }

    // =========================================================================
    //  TOWER / STRUCTURE HELPERS
    // =========================================================================
    private HexTile FindBestTowerSpot(SignalNode node)
    {
        var tilesInRange = GridManager.Instance.GetTilesInRange(node.tile, node.CurrentInfluenceRadius)
            .Where(t => !t.IsOccupied() && !t.HasTower() && t != node.tile)
            .OrderByDescending(t => t.GetTotalInfluence(node.owner));

        return tilesInRange.FirstOrDefault();
    }

    private void PlaceBlueprint(HexTile tile, PlayerData aiPlayer, SignalNode parent)
    {
        if (tile.hasStructure)
            tile.ClearEnvironmentalStructures();

        // FIX: Use TowerPlacementManager.PlaceTowerDirect() for correct collider-based
        // height positioning — the same path the player uses. The old hardcoded
        // Vector3.up * 1.2f caused AI towers to sink into the ground on uneven tiles.
        //
        // PlaceTowerDirect() initialises the tower as solid/built, so we immediately
        // override the state back to Hologram so the AI's Builder units can still
        // find it via GetUnbuiltTowers() and physically construct it next turn.
        if (TowerPlacementManager.Instance != null)
        {
            TowerNode node = TowerPlacementManager.Instance.PlaceTowerDirect(tile, aiPlayer, parent);
            if (node != null)
                node.SetHologramState(); // revert to blueprint so builders can construct it
        }
        else
        {
            // Fallback if TowerPlacementManager is missing from the scene
            Debug.LogWarning("[EnemyAI] PlaceBlueprint: TowerPlacementManager.Instance is null, falling back.");
            GameObject blueprint = Instantiate(towerPrefab, tile.transform.position + Vector3.up * 1.2f, Quaternion.identity);
            TowerNode node = blueprint.GetComponent<TowerNode>();
            node.Initialize(tile, aiPlayer, parent);
        }
    }

    private List<TowerNode> GetUnbuiltTowers(PlayerData owner)
    {
        return FindObjectsByType<TowerNode>(FindObjectsSortMode.None)
            .Where(t => t.owner == owner && t.state == TowerNode.TowerState.Hologram).ToList();
    }

    private HexTile GetCloserTile(HexTile start, HexTile goal, int range)
    {
        return GridManager.Instance.GetTilesInRange(start, range)
            .Where(t => !t.IsOccupied())
            .OrderBy(t => GridManager.Instance.CubeDistance(t.cubeCoords, goal.cubeCoords))
            .FirstOrDefault();
    }

    // =========================================================================
    //  UNIT HANDLERS
    // =========================================================================

    // --- BUILDER ---
    private IEnumerator HandleBuilder(BuilderUnit builder)
    {
        if (builder == null || !builder.gameObject.activeInHierarchy || !builder.CanAct) yield break;
        TowerNode target = GetUnbuiltTowers(builder.owner)
            .OrderBy(t => GridManager.Instance.CubeDistance(builder.currentTile.cubeCoords, t.tile.cubeCoords))
            .FirstOrDefault();

        if (target == null) yield break;

        if (GridManager.Instance.GetNeighbors(builder.currentTile).Contains(target.tile))
        {
            builder.ConstructAdjacentTower();
            if (builder == null) yield break;
            Debug.Log($"[EnemyAI] Builder constructed tower at {target.tile.name}");
        }
        else
        {
            HexTile moveTarget = GetCloserTile(builder.currentTile, target.tile, builder.moveRange);
            if (moveTarget != null)
            {
                builder.MoveTo(moveTarget, builder.moveRange);
                yield return new WaitForSeconds(0.5f);
                
                if (builder != null && builder.CanAct && 
                    GridManager.Instance.GetNeighbors(builder.currentTile).Contains(target.tile))
                {
                    builder.ConstructAdjacentTower();
                }
            }
        }
    }

    // --- FOREMEN (similar to builder but with their own ConstructAdjacentTower) ---
    private IEnumerator HandleForemen(Foremen foreman)
    {
        if (foreman == null || !foreman.gameObject.activeInHierarchy || !foreman.CanAct) yield break;
        TowerNode target = GetUnbuiltTowers(foreman.owner)
            .OrderBy(t => GridManager.Instance.CubeDistance(foreman.currentTile.cubeCoords, t.tile.cubeCoords))
            .FirstOrDefault();

        if (target == null) yield break;

        if (GridManager.Instance.GetNeighbors(foreman.currentTile).Contains(target.tile))
        {
            foreman.ConstructAdjacentTower();
        }
        else
        {
            HexTile moveTarget = GetCloserTile(foreman.currentTile, target.tile, foreman.moveRange);
            if (moveTarget != null)
            {
                foreman.MoveTo(moveTarget, foreman.moveRange);
                yield return new WaitForSeconds(0.5f);

                if (foreman != null && foreman.CanAct &&
                    GridManager.Instance.GetNeighbors(foreman.currentTile).Contains(target.tile))
                {
                    foreman.ConstructAdjacentTower();
                }
            }
        }
    }

    // --- ROBOWORKER (same construct pattern) ---
    private IEnumerator HandleRoboWorker(RoboWorker robo)
    {
        if (robo == null || !robo.gameObject.activeInHierarchy || !robo.CanAct) yield break;
        TowerNode target = GetUnbuiltTowers(robo.owner)
            .OrderBy(t => GridManager.Instance.CubeDistance(robo.currentTile.cubeCoords, t.tile.cubeCoords))
            .FirstOrDefault();

        if (target == null) yield break;

        if (GridManager.Instance.GetNeighbors(robo.currentTile).Contains(target.tile))
        {
            robo.ConstructAdjacentTower();
        }
        else
        {
            HexTile moveTarget = GetCloserTile(robo.currentTile, target.tile, robo.moveRange);
            if (moveTarget != null)
            {
                robo.MoveTo(moveTarget, robo.moveRange);
                yield return new WaitForSeconds(0.5f);

                if (robo != null && robo.CanAct &&
                    GridManager.Instance.GetNeighbors(robo.currentTile).Contains(target.tile))
                {
                    robo.ConstructAdjacentTower();
                }
            }
        }
    }

    // --- WIRE SPECIALIST ---
    private IEnumerator HandleWireSpecialist(WireSpecialist specialist)
    {
        if (specialist == null || !specialist.gameObject.activeInHierarchy || !specialist.CanAct) yield break;
        TowerNode powerTarget = FindObjectsByType<TowerNode>(FindObjectsSortMode.None)
            .Where(t => t.owner == specialist.owner && (!t.IsBuilt() || !t.IsPowered))
            .OrderBy(t => GridManager.Instance.CubeDistance(specialist.currentTile.cubeCoords, t.tile.cubeCoords))
            .FirstOrDefault();

        if (powerTarget != null)
        {
            yield return StartCoroutine(MoveAndBuildWireTowards(specialist, powerTarget.tile));
        }
        else
        {
            HexTile expansionGoal = GridManager.Instance.tiles.Values
                .Where(t => !t.IsOccupied() && !t.HasWire())
                .OrderByDescending(t => t.GetTotalInfluence(specialist.owner))
                .ThenBy(t => GridManager.Instance.CubeDistance(specialist.currentTile.cubeCoords, t.cubeCoords))
                .FirstOrDefault();

            if (expansionGoal != null)
            {
                yield return StartCoroutine(MoveAndBuildWireTowards(specialist, expansionGoal));
            }
        }
    }

    private IEnumerator MoveAndBuildWireTowards(WireSpecialist specialist, HexTile target)
    {
        HexTile bestWireTile = null;
        int minTargetDist = 999;

        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.IsOccupied() || tile.HasWire()) continue;

            bool isNextToOwnPower = false;
            foreach (HexTile n in GridManager.Instance.GetNeighbors(tile))
            {
                if (n.placedNode is SignalNode sn && sn.owner == specialist.owner)
                { isNextToOwnPower = true; break; }
                if (n.placedTower is TowerNode tn && tn.owner == specialist.owner && tn.IsPowered)
                { isNextToOwnPower = true; break; }
                if (n.placedWire is WireNode wn && wn.owner == specialist.owner && wn.IsPowered)
                { isNextToOwnPower = true; break; }
            }

            if (isNextToOwnPower)
            {
                int distToSpecialist = GridManager.Instance.CubeDistance(specialist.currentTile.cubeCoords, tile.cubeCoords);
                if (distToSpecialist <= specialist.moveRange + 1)
                {
                    int distToTarget = GridManager.Instance.CubeDistance(tile.cubeCoords, target.cubeCoords);
                    if (distToTarget < minTargetDist)
                    {
                        minTargetDist = distToTarget;
                        bestWireTile  = tile;
                    }
                }
            }
        }

        if (bestWireTile != null)
        {
            HexTile moveTarget = null;
            if (GridManager.Instance.CubeDistance(specialist.currentTile.cubeCoords, bestWireTile.cubeCoords) <= 1)
            {
                moveTarget = specialist.currentTile;
            }
            else
            {
                moveTarget = GridManager.Instance.GetNeighbors(bestWireTile)
                    .Where(n => !n.IsOccupied() && 
                                GridManager.Instance.CubeDistance(specialist.currentTile.cubeCoords, n.cubeCoords) <= specialist.moveRange)
                    .OrderBy(n => GridManager.Instance.CubeDistance(specialist.currentTile.cubeCoords, n.cubeCoords))
                    .FirstOrDefault();
            }

            if (moveTarget != null)
            {
                if (moveTarget != specialist.currentTile)
                {
                    if (specialist == null || !specialist.gameObject.activeInHierarchy) yield break;
                    specialist.MoveTo(moveTarget, specialist.moveRange);
                    yield return new WaitForSeconds(0.5f);
                }
                
                if (specialist != null && specialist.gameObject.activeInHierarchy && specialist.CanAct)
                {
                    specialist.BuildWire(bestWireTile);
                    Debug.Log($"[EnemyAI] Specialist built wire at {bestWireTile.name} seeking {target.name}");
                }
            }
        }
    }

    // --- TECHNICIAN ---
    private IEnumerator HandleTechnician(Technician technician)
    {
        if (technician == null || !technician.gameObject.activeInHierarchy || !technician.CanAct) yield break;

        // Priority 1: Power the nearest unactivated wire
        WireNode activationTarget = FindObjectsByType<WireNode>(FindObjectsSortMode.None)
            .Where(w => w.owner == technician.owner && !w.IsTechnicianActivated)
            .OrderBy(w => GridManager.Instance.CubeDistance(technician.currentTile.cubeCoords, w.ParentTile.cubeCoords))
            .FirstOrDefault();

        if (activationTarget != null)
        {
            if (GridManager.Instance.GetNeighbors(technician.currentTile).Contains(activationTarget.ParentTile) || 
                technician.currentTile == activationTarget.ParentTile)
            {
                technician.PowerAdjacentStructure();
            }
            else
            {
                HexTile moveTarget = GetCloserTile(technician.currentTile, activationTarget.ParentTile, technician.moveRange);
                if (moveTarget != null && moveTarget != technician.currentTile)
                {
                    technician.MoveTo(moveTarget, technician.moveRange);
                    yield return new WaitForSeconds(0.5f);

                    if (technician != null && technician.CanAct)
                    {
                        technician.PowerAdjacentStructure();
                    }
                }
            }
        }

        if (technician == null || !technician.CanAct) yield break;

        // Priority 2: Repair adjacent structures
        TowerNode repairTarget = FindObjectsByType<TowerNode>(FindObjectsSortMode.None)
            .Where(t => t.owner == technician.owner && t.IsDestroyed())
            .OrderBy(t => GridManager.Instance.CubeDistance(technician.currentTile.cubeCoords, t.tile.cubeCoords))
            .FirstOrDefault();

        if (repairTarget != null)
        {
            if (GridManager.Instance.GetNeighbors(technician.currentTile).Contains(repairTarget.tile))
            {
                technician.RepairAdjacentStructure();
            }
            else
            {
                HexTile moveTarget = GetCloserTile(technician.currentTile, repairTarget.tile, technician.moveRange);
                if (moveTarget != null && moveTarget != technician.currentTile)
                {
                    technician.MoveTo(moveTarget, technician.moveRange);
                    yield return new WaitForSeconds(0.5f);

                    if (technician != null && technician.CanAct && 
                        GridManager.Instance.GetNeighbors(technician.currentTile).Contains(repairTarget.tile))
                    {
                        technician.RepairAdjacentStructure();
                    }
                }
            }
        }
    }

    // --- SALES MARKETER ---
    private IEnumerator HandleSalesMarketer(SalesMarketer marketer)
    {
        if (marketer == null || !marketer.gameObject.activeInHierarchy || !marketer.CanAct) yield break;

        // 1. Try to find an enemy tile to deny (must be visible to AI!)
        HexTile target = GridManager.Instance.tiles.Values
            .Where(t => IsTileVisibleToAI(t, marketer.owner) && t.influenceByPlayer.Any(kvp => kvp.Key != marketer.owner && kvp.Value > 0))
            .OrderBy(t => GridManager.Instance.CubeDistance(marketer.currentTile.cubeCoords, t.cubeCoords))
            .FirstOrDefault();

        // 2. If no visible enemy targets, expand own influence by 1 tile outwards
        if (target == null)
        {
            target = GridManager.Instance.tiles.Values
                .Where(t => t.GetInfluence(marketer.owner) == 0 && GridManager.Instance.GetNeighbors(t).Any(n => n.GetInfluence(marketer.owner) > 0))
                .OrderBy(t => GridManager.Instance.CubeDistance(marketer.currentTile.cubeCoords, t.cubeCoords))
                .FirstOrDefault();
        }

        if (target != null)
        {
            HexTile moveTarget = GetCloserTile(marketer.currentTile, target, marketer.moveRange);
            if (moveTarget != null && moveTarget != marketer.currentTile)
            {
                marketer.MoveTo(moveTarget, marketer.moveRange);
                yield return new WaitForSeconds(0.5f);
            }
        }

        if (marketer.CanAct)
        {
            marketer.ClaimInfluence();
        }
    }

    // --- SCOUT ---
    private IEnumerator HandleScout(ScoutUnit scout)
    {
        if (scout == null || !scout.gameObject.activeInHierarchy || !scout.CanAct) yield break;
        
        // Find a tile that the AI CANNOT currently see, to go explore it.
        HexTile exploreTarget = GridManager.Instance.tiles.Values
            .Where(t => !t.IsOccupied() && t.type == HexTile.TileType.Land && !IsTileVisibleToAI(t, scout.owner))
            .OrderBy(t => GridManager.Instance.CubeDistance(scout.currentTile.cubeCoords, t.cubeCoords))
            .FirstOrDefault();

        // Fallback to furthest tile if entire map is somehow visible
        if (exploreTarget == null)
        {
            exploreTarget = GridManager.Instance.tiles.Values
                .Where(t => !t.IsOccupied() && t.type == HexTile.TileType.Land)
                .OrderByDescending(t =>
                {
                    int minDist = 999;
                    foreach (var node in scout.owner.ownedNodes)
                    {
                        int d = GridManager.Instance.CubeDistance(t.cubeCoords, node.tile.cubeCoords);
                        if (d < minDist) minDist = d;
                    }
                    return minDist;
                })
                .FirstOrDefault();
        }

        if (exploreTarget != null)
        {
            HexTile moveTarget = GetCloserTile(scout.currentTile, exploreTarget, scout.moveRange);
            if (moveTarget != null && moveTarget != scout.currentTile)
            {
                if (scout == null || !scout.gameObject.activeInHierarchy) yield break;
                scout.MoveTo(moveTarget, scout.moveRange);
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    // --- IT PERSONNEL ---
    private IEnumerator HandleITPersonnel(ITPersonnel it)
    {
        if (it == null || !it.gameObject.activeInHierarchy || !it.CanAct) yield break;
        TowerNode target = FindObjectsByType<TowerNode>(FindObjectsSortMode.None)
            .Where(t => t.owner == it.owner && t.IsDestroyed())
            .OrderBy(t => GridManager.Instance.CubeDistance(it.currentTile.cubeCoords, t.tile.cubeCoords))
            .FirstOrDefault();

        if (target != null)
        {
            if (GridManager.Instance.GetNeighbors(it.currentTile).Contains(target.tile))
            {
                it.RepairAdjacentStructure();
            }
            else
            {
                HexTile moveTarget = GetCloserTile(it.currentTile, target.tile, it.moveRange);
                if (moveTarget != null && moveTarget != it.currentTile)
                {
                    it.MoveTo(moveTarget, it.moveRange);
                    yield return new WaitForSeconds(0.5f);

                    if (it != null && it.CanAct &&
                        GridManager.Instance.GetNeighbors(it.currentTile).Contains(target.tile))
                    {
                        it.RepairAdjacentStructure();
                    }
                }
            }
        }
    }

    // --- MAINTENANCE CREW ---
    private IEnumerator HandleMaintenanceCrew(MaintenanceCrew crew)
    {
        if (crew == null || !crew.gameObject.activeInHierarchy || !crew.CanAct) yield break;
        TowerNode target = FindObjectsByType<TowerNode>(FindObjectsSortMode.None)
            .Where(t => t.owner == crew.owner && t.IsDestroyed())
            .OrderBy(t => GridManager.Instance.CubeDistance(crew.currentTile.cubeCoords, t.tile.cubeCoords))
            .FirstOrDefault();

        if (target != null)
        {
            if (GridManager.Instance.GetNeighbors(crew.currentTile).Contains(target.tile))
            {
                crew.PerformMaintenance();
            }
            else
            {
                HexTile moveTarget = GetCloserTile(crew.currentTile, target.tile, crew.moveRange);
                if (moveTarget != null && moveTarget != crew.currentTile)
                {
                    crew.MoveTo(moveTarget, crew.moveRange);
                    yield return new WaitForSeconds(0.5f);

                    if (crew != null && crew.CanAct &&
                        GridManager.Instance.GetNeighbors(crew.currentTile).Contains(target.tile))
                    {
                        crew.PerformMaintenance();
                    }
                }
            }
        }
    }

    // --- ROBO MARSHALL ---
    private IEnumerator HandleRoboMarshall(RoboMarshall marshall)
    {
        if (marshall == null || !marshall.gameObject.activeInHierarchy || !marshall.CanAct) yield break;
        TowerNode target = FindObjectsByType<TowerNode>(FindObjectsSortMode.None)
            .Where(t => t.owner == marshall.owner && t.IsDestroyed())
            .OrderBy(t => GridManager.Instance.CubeDistance(marshall.currentTile.cubeCoords, t.tile.cubeCoords))
            .FirstOrDefault();

        if (target != null)
        {
            if (GridManager.Instance.GetNeighbors(marshall.currentTile).Contains(target.tile))
            {
                marshall.RepairAdjacentStructure();
            }
            else
            {
                HexTile moveTarget = GetCloserTile(marshall.currentTile, target.tile, marshall.moveRange);
                if (moveTarget != null && moveTarget != marshall.currentTile)
                {
                    marshall.MoveTo(moveTarget, marshall.moveRange);
                    yield return new WaitForSeconds(0.5f);

                    if (marshall != null && marshall.CanAct &&
                        GridManager.Instance.GetNeighbors(marshall.currentTile).Contains(target.tile))
                    {
                        marshall.RepairAdjacentStructure();
                    }
                }
            }
        }
    }

    // --- SABOTEUR ---
    private IEnumerator HandleSaboteur(Saboteurs saboteur)
    {
        if (saboteur == null || !saboteur.gameObject.activeInHierarchy || !saboteur.CanAct) yield break;
        // Find nearest enemy tower to sabotage
        TowerNode enemyTower = FindObjectsByType<TowerNode>(FindObjectsSortMode.None)
            .Where(t => t.owner != saboteur.owner && !t.IsDestroyed())
            .OrderBy(t => GridManager.Instance.CubeDistance(saboteur.currentTile.cubeCoords, t.tile.cubeCoords))
            .FirstOrDefault();

        if (enemyTower == null) yield break;

        if (GridManager.Instance.GetNeighbors(saboteur.currentTile).Contains(enemyTower.tile))
        {
            saboteur.DamageAdjacentStructure();
        }
        else
        {
            HexTile moveTarget = GetCloserTile(saboteur.currentTile, enemyTower.tile, saboteur.moveRange);
            if (moveTarget != null && moveTarget != saboteur.currentTile)
            {
                saboteur.MoveTo(moveTarget, saboteur.moveRange);
                yield return new WaitForSeconds(0.5f);

                if (saboteur != null && saboteur.CanAct &&
                    GridManager.Instance.GetNeighbors(saboteur.currentTile).Contains(enemyTower.tile))
                {
                    saboteur.DamageAdjacentStructure();
                }
            }
        }
    }

    // --- BUSINESSMAN ---
    private IEnumerator HandleBusinessman(Businessman biz)
    {
        if (biz == null || !biz.gameObject.activeInHierarchy || !biz.CanAct) yield break;

        // Only chase enemy units the AI can currently SEE 
        Unit targetEnemy = TurnManager.Instance.GetAllUnits()
            .Where(u => u != null
                     && u.gameObject.activeInHierarchy
                     && u.owner != biz.owner
                     && u.currentTile != null
                     && IsTileVisibleToAI(u.currentTile, biz.owner))
            .OrderBy(u => GridManager.Instance.CubeDistance(biz.currentTile.cubeCoords, u.currentTile.cubeCoords))
            .FirstOrDefault();

        if (targetEnemy == null)
        {
            Debug.Log("[EnemyAI] Businessman: no visible enemy units to convert.");
            yield break;
        }

        // If adjacent, recruit
        if (GridManager.Instance.GetNeighbors(biz.currentTile).Contains(targetEnemy.currentTile))
        {
             biz.RecruitNearestWorker();
        }
        else
        {
            // Move toward visible target
            HexTile moveTarget = GetCloserTile(biz.currentTile, targetEnemy.currentTile, biz.moveRange);
            if (moveTarget != null && moveTarget != biz.currentTile)
            {
                biz.MoveTo(moveTarget, biz.moveRange);
                yield return new WaitForSeconds(0.5f);

                if (biz != null && biz.CanAct &&
                    GridManager.Instance.GetNeighbors(biz.currentTile).Contains(targetEnemy.currentTile))
                {
                    biz.RecruitNearestWorker();
                }
            }
        }
    }

    // --- AI VISION HELPER ---
    // The tile.isVisible flag only tracks the human player's vision!
    // The AI needs its own logic to see if a tile is within its own FOV.
    public bool IsTileVisibleToAI(HexTile targetTile, PlayerData aiPlayer)
    {
        if (FieldOfViewManager.Instance == null) return true; // Fallback

        // Check HQs
        if (aiPlayer.ownedNodes.Any(n => n != null && GridManager.Instance.CubeDistance(n.ParentTile.cubeCoords, targetTile.cubeCoords) <= FieldOfViewManager.Instance.hqVisionRange)) 
            return true;

        // Check Towers
        if (TurnManager.Instance.GetAllTowers().Any(t => t.owner == aiPlayer && !t.IsDestroyed() && GridManager.Instance.CubeDistance(t.tile.cubeCoords, targetTile.cubeCoords) <= FieldOfViewManager.Instance.towerVisionRange)) 
            return true;

        // Check Units
        if (TurnManager.Instance.GetAllUnits().Any(u => u.owner == aiPlayer && u.currentTile != null && GridManager.Instance.CubeDistance(u.currentTile.cubeCoords, targetTile.cubeCoords) <= ((u is ScoutUnit) ? FieldOfViewManager.Instance.unitVisionRange + 2 : FieldOfViewManager.Instance.unitVisionRange))) 
            return true;

        return false;
    }
}