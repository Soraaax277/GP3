using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class EnemyAI : MonoBehaviour
{
    public static EnemyAI Instance;

    [Header("Prefabs")]
    public GameObject wireSpecialistPrefab;
    public GameObject builderPrefab;
    public GameObject towerPrefab;
    public GameObject salesMarketerPrefab;
    public GameObject technicianPrefab;

    [Header("Costs")]
    public int wireSpecialistCost  = 25;
    public int builderCost         = 50;
    public int salesMarketerCost   = 30;
    public int technicianCost      = 20;

    private void Awake()
    {
        Instance = this;
    }

    public void ExecuteTurn(PlayerData aiPlayer)
    {
        StartCoroutine(AITurnRoutine(aiPlayer));
    }

    private IEnumerator AITurnRoutine(PlayerData aiPlayer)
    {
        Debug.Log($"[EnemyAI] {aiPlayer.playerName} turn start. Resources: {aiPlayer.resources}");
        yield return new WaitForSeconds(1.0f);

        foreach (SignalNode node in aiPlayer.ownedNodes)
        {
            if (node.CanPlaceTower())
            {
                HexTile bestBlueprintTile = FindBestTowerSpot(node);
                if (bestBlueprintTile != null)
                {
                    Debug.Log($"[EnemyAI] Placing tower blueprint at {bestBlueprintTile.name}");
                    PlaceBlueprint(bestBlueprintTile, aiPlayer, node);
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }

        List<Unit> myUnits = TurnManager.Instance.GetAllUnits().Where(u => u.owner == aiPlayer).ToList();
        int specialistCount = myUnits.Count(u => u is WireSpecialist);
        int builderCount    = myUnits.Count(u => u is BuilderUnit);

        bool needsBuilder = GetUnbuiltTowers(aiPlayer).Any();
        
        if (needsBuilder && builderCount == 0 && aiPlayer.resources >= builderCost)
        {
            SignalNode spawnNode = aiPlayer.ownedNodes[Random.Range(0, aiPlayer.ownedNodes.Count)];
            Unit u = UnitSpawner.Instance.SpawnUnit(builderPrefab, spawnNode);
            if (u != null) aiPlayer.resources -= builderCost;
            yield return new WaitForSeconds(0.5f);
        }

        if (specialistCount < 2 && aiPlayer.resources >= wireSpecialistCost)
        {
            SignalNode spawnNode = aiPlayer.ownedNodes[Random.Range(0, aiPlayer.ownedNodes.Count)];
            Unit u = UnitSpawner.Instance.SpawnUnit(wireSpecialistPrefab, spawnNode);
            if (u != null) aiPlayer.resources -= wireSpecialistCost;
            yield return new WaitForSeconds(0.5f);
        }

        int marketerCount = myUnits.Count(u => u is SalesMarketer);
        if (marketerCount < 1 && aiPlayer.resources >= salesMarketerCost)
        {
            SignalNode spawnNode = aiPlayer.ownedNodes[Random.Range(0, aiPlayer.ownedNodes.Count)];
            Unit u = UnitSpawner.Instance.SpawnUnit(salesMarketerPrefab, spawnNode);
            if (u != null) aiPlayer.resources -= salesMarketerCost;
            yield return new WaitForSeconds(0.5f);
        }

        int technicianCount = myUnits.Count(u => u is Technician);
        bool needsRepair = FindObjectsByType<TowerNode>(FindObjectsSortMode.None)
            .Any(t => t.owner == aiPlayer && t.IsDestroyed());
        
        Debug.Log($"[EnemyAI] Technician Check - Count: {technicianCount}, NeedsRepair: {needsRepair}, " +
                  $"Resources: {aiPlayer.resources}, Cost: {technicianCost}, Prefab: {technicianPrefab != null}");
        
        if (needsRepair && technicianCount < 1 && aiPlayer.resources >= technicianCost)
        {
            if (technicianPrefab == null)
            {
                Debug.LogError("[EnemyAI] Technician Prefab is NULL! Please assign it in the Inspector.");
                yield return new WaitForSeconds(0.5f);
            }
            else
            {
                SignalNode spawnNode = aiPlayer.ownedNodes[Random.Range(0, aiPlayer.ownedNodes.Count)];
                Unit u = UnitSpawner.Instance.SpawnUnit(technicianPrefab, spawnNode);
                if (u != null)
                {
                    aiPlayer.resources -= technicianCost;
                    Debug.Log($"[EnemyAI] Successfully recruited Technician! Remaining resources: {aiPlayer.resources}");
                }
                else
                {
                    Debug.LogError("[EnemyAI] Failed to spawn Technician unit!");
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        myUnits = TurnManager.Instance.GetAllUnits().Where(u => u.owner == aiPlayer).ToList();

        foreach (var builder in myUnits.OfType<BuilderUnit>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleBuilder(builder));
            yield return new WaitForSeconds(0.5f);
        }

        foreach (var specialist in myUnits.OfType<WireSpecialist>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleWireSpecialist(specialist));
            yield return new WaitForSeconds(0.5f);
        }

        foreach (var marketer in myUnits.OfType<SalesMarketer>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleSalesMarketer(marketer));
            yield return new WaitForSeconds(0.5f);
        }

        foreach (var technician in myUnits.OfType<Technician>().Where(u => u.CanAct))
        {
            yield return StartCoroutine(HandleTechnician(technician));
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("[EnemyAI] Turn complete.");
        yield return new WaitForSeconds(1.0f);
        TurnManager.Instance.EndTurn();
    }

    private HexTile FindBestTowerSpot(SignalNode node)
    {
        var tilesInRange = GridManager.Instance.GetTilesInRange(node.tile, node.CurrentInfluenceRadius)
            .Where(t => !t.IsOccupied() && !t.HasTower() && t != node.tile)
            .OrderByDescending(t => t.GetTotalInfluence(node.owner));

        return tilesInRange.FirstOrDefault();
    }

    private void PlaceBlueprint(HexTile tile, PlayerData aiPlayer, SignalNode parent)
    {
        GameObject blueprint = Instantiate(towerPrefab, tile.transform.position + Vector3.up * 1.2f, Quaternion.identity);
        TowerNode node = blueprint.GetComponent<TowerNode>();
        node.Initialize(tile, aiPlayer, parent);
    }

    private List<TowerNode> GetUnbuiltTowers(PlayerData owner)
    {
        // TowerState.Unbuilt was renamed to TowerState.Hologram (System 3 three-phase build).
        // A Hologram is the newly-placed blueprint that a Builder must Construct.
        return FindObjectsByType<TowerNode>(FindObjectsSortMode.None)
            .Where(t => t.owner == owner && t.state == TowerNode.TowerState.Hologram).ToList();
    }

    private IEnumerator HandleBuilder(BuilderUnit builder)
    {
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

    private IEnumerator HandleWireSpecialist(WireSpecialist specialist)
    {
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
                {
                    isNextToOwnPower = true;
                    break;
                }
                if (n.placedTower is TowerNode tn && tn.owner == specialist.owner && tn.IsPowered)
                {
                    isNextToOwnPower = true;
                    break;
                }
                if (n.placedWire is WireNode wn && wn.owner == specialist.owner && wn.IsPowered)
                {
                    isNextToOwnPower = true;
                    break;
                }
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
                    specialist.MoveTo(moveTarget, specialist.moveRange);
                    yield return new WaitForSeconds(0.5f);
                }
                
                if (specialist.CanAct)
                {
                    specialist.BuildWire(bestWireTile);
                    Debug.Log($"[EnemyAI] Specialist built wire at {bestWireTile.name} seeking {target.name}");
                }
            }
        }
    }

    private HexTile GetCloserTile(HexTile start, HexTile goal, int range)
    {
        return GridManager.Instance.GetTilesInRange(start, range)
            .Where(t => !t.IsOccupied())
            .OrderBy(t => GridManager.Instance.CubeDistance(t.cubeCoords, goal.cubeCoords))
            .FirstOrDefault();
    }

    private IEnumerator HandleSalesMarketer(SalesMarketer marketer)
    {
        HexTile target = GridManager.Instance.tiles.Values
            .Where(t => t.influenceByPlayer.Any(kvp => kvp.Key != marketer.owner && kvp.Value > 0))
            .OrderBy(t => GridManager.Instance.CubeDistance(marketer.currentTile.cubeCoords, t.cubeCoords))
            .FirstOrDefault();

        if (target != null)
        {
            HexTile moveTarget = GetCloserTile(marketer.currentTile, target, marketer.moveRange);
            if (moveTarget != null && moveTarget != marketer.currentTile)
            {
                marketer.MoveTo(moveTarget, marketer.moveRange);
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    private IEnumerator HandleTechnician(Technician technician)
    {
        TowerNode target = FindObjectsByType<TowerNode>(FindObjectsSortMode.None)
            .Where(t => t.owner == technician.owner && t.IsDestroyed())
            .OrderBy(t => GridManager.Instance.CubeDistance(technician.currentTile.cubeCoords, t.tile.cubeCoords))
            .FirstOrDefault();

        if (target != null)
        {
            if (GridManager.Instance.GetNeighbors(technician.currentTile).Contains(target.tile))
            {
                technician.RepairAdjacentStructure();
            }
            else
            {
                HexTile moveTarget = GetCloserTile(technician.currentTile, target.tile, technician.moveRange);
                if (moveTarget != null && moveTarget != technician.currentTile)
                {
                    technician.MoveTo(moveTarget, technician.moveRange);
                    yield return new WaitForSeconds(0.5f);

                    if (technician != null && technician.CanAct && 
                        GridManager.Instance.GetNeighbors(technician.currentTile).Contains(target.tile))
                    {
                        technician.RepairAdjacentStructure();
                    }
                }
            }
        }
    }
}