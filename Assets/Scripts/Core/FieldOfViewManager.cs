using UnityEngine;
using System.Collections.Generic;

public class FieldOfViewManager : MonoBehaviour
{
    public static FieldOfViewManager Instance;

    [Header("Vision Settings")]
    public int unitVisionRange = 2;
    public int towerVisionRange = 3;
    public int hqVisionRange = 4;

    private void Awake()
    {
        Instance = this;
    }

    public void UpdateFogOfWar(PlayerData localPlayer)
    {
        if (GridManager.Instance == null) return;

        // 1. Reset current visibility
        int newlyExploredTotal = 0;
        foreach (HexTile tile in GridManager.Instance.GetAllTiles())
        {
            tile.isVisible = false;
        }

        // 2. Grant vision from SignalNodes (HQs)
        foreach (var node in localPlayer.ownedNodes)
        {
            if (node != null) newlyExploredTotal += RevealTiles(node.ParentTile, hqVisionRange);
        }

        // 3. Grant vision from Towers
        foreach (var tower in TurnManager.Instance.GetAllTowers())
        {
            if (tower.owner == localPlayer && !tower.IsDestroyed())
            {
                newlyExploredTotal += RevealTiles(tower.tile, towerVisionRange);
            }
        }

        // 3.5. Grant vision from Structures
        foreach (var structure in TurnManager.Instance.GetAllStructures())
        {
            // We check IsBroken. A structure grants vision immediately upon placement
            // (even as a blueprint/hologram), just like towers do.
            if (structure.owner == localPlayer && !structure.IsBroken)
            {
                newlyExploredTotal += RevealTiles(structure.ParentTile, structure.visionRange);
            }
        }

        // 4. Grant vision from Units
        foreach (var unit in TurnManager.Instance.GetAllUnits())
        {
            if (unit.owner == localPlayer)
            {
                // Scout units get a bonus
                int range = (unit is ScoutUnit) ? unitVisionRange + 2 : unitVisionRange;
                int revealedByUnit = RevealTiles(unit.currentTile, range);
                newlyExploredTotal += revealedByUnit;

                // QUEST HOOK: Scout vision/movement
                if (unit is ScoutUnit && revealedByUnit > 0)
                {
                    QuestManager.Instance?.SetQuestFlag(localPlayer, "ScoutEdgeVision");
                }
            }
        }

        // QUEST HOOK: Intel on Three Enemies
        if (QuestManager.Instance != null && TurnManager.Instance != null)
        {
            HashSet<PlayerData> visibleEnemies = new HashSet<PlayerData>();
            foreach (HexTile tile in GridManager.Instance.GetAllTiles())
            {
                if (tile.isVisible)
                {
                    PlayerData tileOwner = tile.GetOwner();
                    if (tileOwner != null && tileOwner != localPlayer)
                        visibleEnemies.Add(tileOwner);

                    if (tile.placedUnit != null && tile.placedUnit.owner != localPlayer)
                        visibleEnemies.Add(tile.placedUnit.owner);
                }
            }
            if (visibleEnemies.Count >= 3)
            {
                QuestManager.Instance.SetQuestFlag(localPlayer, "IntelOnThreeEnemies");
            }
        }

        if (newlyExploredTotal >= 2)
        {
            QuestManager.Instance?.SetQuestFlag(localPlayer, "RevealedTwoHexes");
        }

        // 5. Update visuals and hide enemy units
        UpdateVisibilityState();

        // 6. Update the fog cloud mesh (only covers unexplored tiles)
        if (HexFogRenderer.Instance != null)
            HexFogRenderer.Instance.UpdateFog();
    }

    private int RevealTiles(HexTile center, int range)
    {
        if (center == null) return 0;
        int newlyExploredCount = 0;
        List<HexTile> visibleTiles = GridManager.Instance.GetTilesInRange(center, range);
        foreach (HexTile tile in visibleTiles)
        {
            if (!tile.isExplored) newlyExploredCount++;
            tile.isExplored = true;
            tile.isVisible = true;
        }
        return newlyExploredCount;
    }

    private void UpdateVisibilityState()
    {
        PlayerData humanPlayer = TurnManager.Instance.players[0];

        foreach (HexTile tile in GridManager.Instance.GetAllTiles())
        {
            tile.UpdateAppearance();

            // 1. Handle Units
            if (tile.placedUnit != null)
            {
                // Only hide enemy units or neutral units if not visible
                if (tile.placedUnit.owner != humanPlayer)
                {
                    tile.placedUnit.gameObject.SetActive(tile.isVisible);
                }
            }

            // 2. Handle Infrastructure (Towers/Wires/SignalNodes)
            // If the tile is not explored, hide everything.
            // If explored but not visible, we already show the grey tile, 
            // but we should also dim the building visuals.
            
            if (tile.placedTower != null)
            {
                UpdateBuildingVisibility(tile.placedTower.gameObject, tile);
            }
            if (tile.placedWire != null)
            {
                UpdateBuildingVisibility(tile.placedWire.gameObject, tile);
            }
            if (tile.placedSignalNode != null)
            {
                UpdateBuildingVisibility(tile.placedSignalNode.gameObject, tile);

                // QUEST HOOK: Scouted Enemy HQ
                if (tile.isVisible && tile.placedSignalNode.owner != humanPlayer && QuestManager.Instance != null)
                {
                    QuestManager.Instance.SetQuestFlag(humanPlayer, "ScoutedEnemyHQ");
                }
            }
            
            // 3. Handle Resource Geysers
            ResourceGeyser geyser = tile.GetComponentInChildren<ResourceGeyser>();
            if (geyser != null)
            {
                geyser.gameObject.SetActive(tile.isVisible);
                
                // QUEST HOOK: Vision of Hidden Enemy Geyser
                if (tile.isVisible && tile.GetOwner() != null && tile.GetOwner() != humanPlayer && QuestManager.Instance != null)
                {
                    QuestManager.Instance.SetQuestFlag(humanPlayer, "VisionOfHiddenEnemyGeyser");
                }
            }

            // 4. Handle Intimidation Tactics (Enemy units near yours)
            if (tile.isVisible && tile.placedUnit != null && tile.placedUnit.owner != humanPlayer && QuestManager.Instance != null)
            {
                var nearby = GridManager.Instance.GetTilesInRange(tile, 2);
                foreach (var n in nearby)
                {
                    if (n.placedUnit != null && n.placedUnit.owner == humanPlayer)
                    {
                        QuestManager.Instance.SetQuestFlag(humanPlayer, "IntimidationTactics");
                        break;
                    }
                }
            }

            // 5. Handle Saboteurs
            NomadicSaboteur saboteur = tile.GetComponentInChildren<NomadicSaboteur>();
            if (saboteur != null)
            {
                saboteur.gameObject.SetActive(tile.isVisible);
                
                // QUEST HOOK: Intercepted Saboteur
                if (tile.isVisible && QuestManager.Instance != null)
                {
                    // If it's on a tile we just saw, it's intercepted
                    QuestManager.Instance.SetQuestFlag(humanPlayer, "InterceptedSaboteur");
                }
            }
        }
    }

    private void UpdateBuildingVisibility(GameObject obj, HexTile tile)
    {
        // If not even explored (Shroud), completely hide the building
        if (!tile.isExplored)
        {
            obj.SetActive(false);
            return;
        }

        // If explored (Fog or Visible), ensure it's active
        obj.SetActive(true);

        // Dim the mesh if it's in the Fog (explored but not currently visible)
        Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            if (r.name.Contains("RangeIndicator")) continue; // Don't dim ranges if they are showing

            Color c = r.material.color;
            if (!tile.isVisible)
            {
                // Enforce a "Fog" look on the building Mesh
                r.material.color = new Color(c.r, c.g, c.b, 0.4f); // Semi-transparent or just switch to a darker shader
            }
            else
            {
                r.material.color = new Color(c.r, c.g, c.b, 1f);
            }
        }
    }
}