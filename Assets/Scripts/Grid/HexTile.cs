using UnityEngine;
using System.Collections.Generic;

public class HexTile : MonoBehaviour
{
    public Vector3Int cubeCoords;
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

    public void Initialize(Vector3Int coords)
    {
        cubeCoords = coords;
        name = $"Hex {coords.x},{coords.y},{coords.z}";

        baseInfluence = Random.Range(1, 11);
        Debug.Log($"{name} base influence: {baseInfluence}");
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
        return placedNode != null || placedUnit != null || placedTower != null;
    }

    public bool HasWire()
    {
        return placedWire != null;
    }

    public bool IsWalkable()
    {
        return placedNode == null && placedUnit == null && placedTower == null;
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