using UnityEngine;
using System.Collections.Generic;

public class HexTile : MonoBehaviour
{
    public enum TileType { Land, Water }

    public Vector3Int cubeCoords;
    public TileType type = TileType.Land;
    public bool hasStructure;
    public SignalNode placedNode;

    //  SIGNAL NODE REFERENCE  (System 2)
    //  Stores a direct reference to the SignalNode (HQ) placed on this tile.
    //  Used by SignalNode.PropagateSignal() to identify the HQ's home tile,
    //  and by other systems that need to check whether a tile hosts an HQ.
    //  NOTE: placedNode already exists above — placedSignalNode is the typed
    //  reference that avoids casts and makes intent explicit.
    public SignalNode placedSignalNode;

    public TowerNode placedTower;
    public WireNode placedWire;
    public Unit placedUnit;

    public int baseInfluence;
    public Dictionary<PlayerData, int> influenceByPlayer = new Dictionary<PlayerData, int>();
    public int influenceSuppression;

    private Renderer rend;
    private Color baseColor;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        baseColor = rend.material.color;
    }

    public void Initialize(Vector3Int coords, TileType tileType = TileType.Land)
    {
        cubeCoords = coords;
        type = tileType;
        name = $"Hex {coords.x},{coords.y},{coords.z} ({type})";

        baseInfluence = Random.Range(1, 11);
        
        UpdateAppearance();
    }

    public void UpdateAppearance()
    {
        if (rend == null) rend = GetComponent<Renderer>();
        
        if (type == TileType.Water)
        {
            rend.material.color = new Color(0.1f, 0.3f, 0.8f, 1f); // Darker Blue
            baseColor = rend.material.color;
        }
        else
        {
            // Keep original logic or set a default land color if needed
            // baseColor is captured in Awake, so we just revert to it
            rend.material.color = baseColor;
        }
    }

    public int GetTotalInfluence(PlayerData forPlayer)
    {
        int raw = baseInfluence;
        if (influenceByPlayer.ContainsKey(forPlayer))
            raw += influenceByPlayer[forPlayer];
        
        return Mathf.Max(0, raw);
    }

    public void AddInfluence(PlayerData player, int amount)
    {
        if (!influenceByPlayer.ContainsKey(player))
            influenceByPlayer[player] = 0;
        influenceByPlayer[player] += amount;
    }

    public void RemoveInfluence(PlayerData player, int amount)
    {
        if (influenceByPlayer.ContainsKey(player))
        {
            influenceByPlayer[player] -= amount;
            if (influenceByPlayer[player] < 0) influenceByPlayer[player] = 0;
        }
    }


    public bool IsOccupied()
    {
        return type == TileType.Water || placedNode != null || placedUnit != null || placedTower != null;
    }

    public void ClearEnvironmentalStructures()
    {
        if (!hasStructure) return;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.Contains("Env_Structure"))
            {
                Destroy(child.gameObject);
            }
        }
        hasStructure = false;
    }

    public bool HasWire()
    {
        return placedWire != null;
    }

    public bool IsWalkable()
    {
        // Units cannot swim and cannot walk through structures
        return type == TileType.Land && !hasStructure && placedNode == null && placedUnit == null && placedTower == null;
    }

    public bool HasTower()
    {
        return placedTower != null;
    }

    public void HighlightWalkable()
    {
        rend.material.color = new Color(0f, 1f, 0f, 0.4f);
    }

    public void HighlightBlocked()
    {
        rend.material.color = new Color(1f, 0f, 0f, 0.4f);
    }

    public void ClearHighlight()
    {
        rend.material.color = baseColor;
    }

    public int GetInfluence(PlayerData player)
    {
        if (influenceByPlayer.ContainsKey(player))
            return influenceByPlayer[player];
        return 0;
    }

    public void SetInfluence(PlayerData player, int amount)
    {
        influenceByPlayer[player] = amount;
    }

    public void ClearInfluence()
    {
        influenceByPlayer.Clear();
    }
}