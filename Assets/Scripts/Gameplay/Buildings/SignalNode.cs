using UnityEngine;
using System.Collections.Generic;

public class SignalNode : MonoBehaviour
{
    //  IDENTITY
    public PlayerData owner { get; private set; }
    public HexTile    tile  { get; private set; }

    public HexTile ParentTile => tile;

    [Header("Visual")]
    public GameObject businessBuilding;

    //  TOWER CAPACITY
    [Header("Tower Settings")]
    [Tooltip("Base number of towers this HQ can support before any tech upgrades.")]
    public int maxTowers = 2;

    public int CurrentMaxTowers
    {
        get
        {
            int bonus = 0;
            if (TechManager.Instance != null)
                bonus = Mathf.RoundToInt(TechManager.Instance.GetInfraFlatBonus("TowerCapacity"));
            return maxTowers + bonus;
        }
    }

    public int towersPlacedCount = 0;

    public bool CanPlaceTower()
    {
        return towersPlacedCount < CurrentMaxTowers;
    }

    // -----------------------------------------------------------------------
    //  INFLUENCE / PLACEMENT RADIUS
    //  Supports BOTH flat bonus AND multiplier via the same "InfluenceRadius" key.
    //
    //  Flat  (isMultiplier ☐): adds directly to the base radius.
    //         e.g. base 3 + bonus 1 = 4 tiles
    //
    //  Mult  (isMultiplier ✅): scales the result after flat bonus is added.
    //         e.g. (base 3 + 0) * 1.1 = 3.3 → 3 tiles  (at +10%)
    //         e.g. (base 3 + 0) * 0.8 = 2.4 → 2 tiles  (Modern SatComm -20%)
    //
    //  Both can be stacked — flat first, then the multiplier.
    // -----------------------------------------------------------------------
    [Header("Influence Settings")]
    [Tooltip("Base hex radius within which towers can be placed from this HQ.")]
    public int baseInfluenceRadius = 3;

    public int CurrentInfluenceRadius
    {
        get
        {
            if (TechManager.Instance == null) return baseInfluenceRadius;

            float flatBonus  = TechManager.Instance.GetInfraFlatBonus("InfluenceRadius");
            float multiplier = TechManager.Instance.GetInfraMultiplier("InfluenceRadius");

            // Safety: prevent zero/negative multiplier collapsing the radius
            if (multiplier <= 0f) multiplier = 1f;

            int result = Mathf.RoundToInt((baseInfluenceRadius + flatBonus) * multiplier);
            return Mathf.Max(1, result);
        }
    }

    //  SIGNAL  (System 2)
    [Header("Signal")]
    [Tooltip("Base signal strength broadcast from this HQ each turn.")]
    public float baseSignalStrength = 50f;

    public List<TowerNode> connectedTowers { get; private set; } = new List<TowerNode>();

    //  INITIALIZATION
    public void Initialize(HexTile hexTile, PlayerData player)
    {
        tile  = hexTile;
        owner = player;

        tile.placedNode       = this;
        tile.placedSignalNode = this;

        if (!player.ownedNodes.Contains(this))
            player.ownedNodes.Add(this);

        if (businessBuilding == null)
            businessBuilding = gameObject;

        Debug.Log($"[SignalNode] Initialized for {player.playerName} at {hexTile.name}");
    }

    //  BASE SIGNAL
    public float GetBaseSignalStrength()
    {
        float techBoost = 0f;
        if (TechManager.Instance != null)
            techBoost = TechManager.Instance.GetInfraFlatBonus("BaseSignalBoost");
        return baseSignalStrength + techBoost;
    }

    //  SIGNAL PROPAGATION  (System 2)
    public void PropagateSignal()
    {
        if (GridManager.Instance == null || tile == null) return;

        foreach (TowerNode tower in TurnManager.Instance.GetAllTowers())
        {
            if (tower != null && tower.owner == owner)
                tower.receivedSignalStrength = 0f;
        }

        float decayRate = 0.50f;
        if (TechManager.Instance != null)
        {
            float reduction = TechManager.Instance.GetInfraFlatBonus("SignalDecayReduction");
            decayRate = Mathf.Max(0.05f, decayRate - reduction);
        }

        // "MicrowaveRelays" feature: HQ broadcasts directly to all owned towers
        // without needing a wire chain — signal still decays per hop distance.
        bool microwaveRelays = TechManager.Instance != null &&
                               TechManager.Instance.IsFeatureUnlocked("MicrowaveRelays");

        float startSignal = GetBaseSignalStrength();

        var queue   = new Queue<(HexTile tile, float signal)>();
        var visited = new HashSet<HexTile>();

        visited.Add(tile);

        if (microwaveRelays)
        {
            // Bypass wire requirement — seed directly from all owned towers regardless
            // of wire connectivity. Distance from HQ determines hop count.
            foreach (TowerNode tower in TurnManager.Instance.GetAllTowers())
            {
                if (tower == null || tower.owner != owner || tower.tile == null) continue;
                int hops = GridManager.Instance.CubeDistance(tile.cubeCoords, tower.tile.cubeCoords);
                float signal = startSignal * Mathf.Pow(1f - decayRate, hops);
                if (signal > tower.receivedSignalStrength)
                    tower.receivedSignalStrength = signal;
                if (!connectedTowers.Contains(tower))
                    connectedTowers.Add(tower);
            }
        }
        else
        {
            // Standard BFS through owned wires
            foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(tile))
            {
                if (neighbor == null || visited.Contains(neighbor)) continue;

                bool hasOwnedWire  = neighbor.placedWire  != null && neighbor.placedWire.owner  == owner;
                bool hasOwnedTower = neighbor.placedTower != null && neighbor.placedTower.owner == owner;

                if (hasOwnedWire || hasOwnedTower)
                    queue.Enqueue((neighbor, startSignal * (1f - decayRate)));
            }

            connectedTowers.Clear();

            while (queue.Count > 0)
            {
                var (current, signal) = queue.Dequeue();

                if (visited.Contains(current)) continue;
                visited.Add(current);

                if (current.placedTower != null && current.placedTower.owner == owner)
                {
                    TowerNode tower = current.placedTower;
                    if (signal > tower.receivedSignalStrength)
                    {
                        tower.receivedSignalStrength = signal;
                        Debug.Log($"[Signal] {owner.playerName}'s {tower.name} receives: {signal:F2}");
                    }
                    if (!connectedTowers.Contains(tower))
                        connectedTowers.Add(tower);
                }

                if (current.placedWire != null && current.placedWire.owner == owner)
                {
                    foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(current))
                    {
                        if (neighbor == null || visited.Contains(neighbor)) continue;

                        bool hasOwnedWire  = neighbor.placedWire  != null && neighbor.placedWire.owner  == owner;
                        bool hasOwnedTower = neighbor.placedTower != null && neighbor.placedTower.owner == owner;

                        if (hasOwnedWire || hasOwnedTower)
                            queue.Enqueue((neighbor, signal * (1f - decayRate)));
                    }
                }
            }
        }

        Debug.Log($"[Signal] {owner.playerName}'s HQ propagated " +
                  $"(base: {startSignal}, decay: {decayRate * 100:F0}%/hop, " +
                  $"microwave: {microwaveRelays}, connected towers: {connectedTowers.Count})");
    }
}