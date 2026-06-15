using UnityEngine;
using System.Collections.Generic;

public class HexTile : MonoBehaviour
{
    public enum TileType { Land, Water, City, Road }

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
    public StructureNode placedStructure;
    public Unit placedUnit;

    public bool isHyperinflated; // Flag for EconomyManager boost
    
    public int baseInfluence;
    public Dictionary<PlayerData, int> influenceByPlayer = new Dictionary<PlayerData, int>();
    public int influenceSuppression;
    public float hazardImpact; // 0.0 to 1.0 percentage for environmental hazards
    public bool isExplored;   // For Fog of War: Has the player ever seen this?
    public bool isVisible;    // For Fog of War: Is the player currently seeing this?

    /// <summary>
    /// Calculates the exact world Y-coordinate of the topmost face of this tile's collider.
    /// Used to perfectly snap units, towers, and indicators to the ground regardless of tile height.
    /// </summary>
    public float GetSurfaceY()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return transform.position.y;

        float halfHeight = box.size.y * 0.5f * transform.lossyScale.y;
        float centerY    = box.center.y * transform.lossyScale.y;
        return transform.position.y + centerY + halfHeight;
    }

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
        hazardImpact = Random.value; // Random impact percentage for this tile
        isExplored = false;
        isVisible = false;
        
        UpdateAppearance();
    }

    private Color targetFowColor;
    public float transitionSpeed = 2f;

    private void Update()
    {
        // Custom shaders (HexRoad, HexConcrete) don't expose _Color — skip the
        // FOW colour lerp for those tiles to avoid every-frame warning spam.
        if (rend != null && rend.material.HasProperty("_Color"))
        {
            rend.material.color = Color.Lerp(rend.material.color, targetFowColor, Time.deltaTime * transitionSpeed);
        }
    }

    public void UpdateAppearance()
    {
        if (rend == null) rend = GetComponent<Renderer>();
        
        // --- FOG OF WAR VISUALS ---
        Color baseTypeColor;
        switch (type)
        {
            case TileType.Water:
                baseTypeColor = new Color(0.1f, 0.3f, 0.8f, 1f);
                break;
            case TileType.City:
                // Concrete gray — always uses material color set by GridManager
                baseTypeColor = new Color(0.54f, 0.54f, 0.56f, 1f);
                break;
            case TileType.Road:
                // Dark tarmac
                baseTypeColor = new Color(0.35f, 0.33f, 0.30f, 1f);
                break;
            default: // Land (grass)
                baseTypeColor = baseColor;
                break;
        }

        if (!isExplored)
        {
            // The Shroud: Dark gray — fog clouds sit on top
            targetFowColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        }
        else if (!isVisible)
        {
            // Explored but not currently visible: slightly dimmed real color, no clouds
            targetFowColor = Color.Lerp(baseTypeColor, Color.white, 0.5f);
        }
        else
        {
            // Fully visible: real color
            targetFowColor = baseTypeColor;
        }

        UpdateStructureVisibility();
    }

    public void UpdateStructureVisibility()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            // Environmental buildings — fully hide in fog
            if (child.name.Contains("Env_Structure"))
            {
                child.gameObject.SetActive(isVisible);
            }

            // Nature props (trees, rocks, etc.) — always visible but dimmed in fog
            // so they still read as environment even through the shroud
            if (child.name.Contains("Env_Nature"))
            {
                child.gameObject.SetActive(true);
                float dimAmount = isExplored ? (isVisible ? 1f : 0.55f) : 0.25f;
                foreach (var r in child.GetComponentsInChildren<Renderer>(true))
                {
                    // Tint every material on the prop toward dark to simulate fog
                    // without destroying the saved material — we work on the
                    // instance material Unity creates per-renderer automatically.
                    Color c = r.material.color;
                    r.material.color = new Color(c.r * dimAmount, c.g * dimAmount, c.b * dimAmount, c.a);
                }
            }
        }
    }

    public int GetTotalInfluence(PlayerData forPlayer)
    {
        int raw = baseInfluence;
        if (influenceByPlayer.ContainsKey(forPlayer))
            raw += influenceByPlayer[forPlayer];
        
        return Mathf.Max(0, raw - influenceSuppression);
    }

    /// <summary>
    /// Returns the player who currently "owns" this tile based on dominant influence.
    /// Returns null if no player has influence or if there's a tie (wilderness).
    /// </summary>
    public PlayerData GetOwner()
    {
        if (influenceByPlayer == null || influenceByPlayer.Count == 0) return null;

        PlayerData bestOwner = null;
        int maxInf = 0;
        bool tie = false;

        foreach (var kvp in influenceByPlayer)
        {
            int effectiveInf = Mathf.Max(0, kvp.Value - influenceSuppression);
            if (effectiveInf > maxInf)
            {
                maxInf = effectiveInf;
                bestOwner = kvp.Key;
                tie = false;
            }
            else if (effectiveInf == maxInf && maxInf > 0)
            {
                tie = true;
            }
        }

        if (maxInf <= 0 || tie) return null;
        return bestOwner;
    }

    /// <summary>
    /// Adds influence for a player. 
    /// If bypassDominance is false, the addition is BLOCKED if the tile is already owned by someone else.
    /// This represents the "first come, first served" territorial rule.
    /// </summary>
    public void AddInfluence(PlayerData player, int amount, bool bypassDominance = false)
    {
        if (player == null) return;

        if (!bypassDominance)
        {
            PlayerData currentOwner = GetOwner();
            // If the tile is owned by an enemy, normal building influence is ignored.
            if (currentOwner != null && currentOwner != player)
            {
                return;
            }
        }

        if (!influenceByPlayer.ContainsKey(player))
            influenceByPlayer[player] = 0;

        PlayerData oldOwner = GetOwner();
        influenceByPlayer[player] += amount;
        PlayerData newOwner = GetOwner();

        if (oldOwner != newOwner && newOwner != null)
        {
            if (oldOwner != null)
            {
                // Flipping territory
                ActionLogUI.PostFiltered(newOwner, $"Captured {oldOwner.playerName}'s territory!", newOwner.isAI ? ActionLogUI.Colors.Enemy : ActionLogUI.Colors.Player, !newOwner.isAI || !oldOwner.isAI);
            }
        }

        if (QuestManager.Instance != null && oldOwner != player && newOwner == player)
        {
            // QUEST HOOK: Claimed Chokepoint
            // Heuristic: Tile is strategically significant if it has many neighbors that aren't yours yet
            int foreignNeighbors = 0;
            var neighbors = GridManager.Instance.GetNeighbors(this);
            foreach (var n in neighbors)
            {
                if (n.GetOwner() != player) foreignNeighbors++;
            }

            if (foreignNeighbors >= 5)
            {
                QuestManager.Instance.SetQuestFlag(player, "ClaimedChokepoint");
            }
        }
    }

    public void RemoveInfluence(PlayerData player, int amount)
    {
        if (player == null) return;
        if (influenceByPlayer.ContainsKey(player))
        {
            influenceByPlayer[player] -= amount;
            if (influenceByPlayer[player] < 0) influenceByPlayer[player] = 0;
        }
    }


    public bool IsOccupied()
    {
        // Road and Water tiles can never host player-placed structures or units.
        return type == TileType.Water || type == TileType.Road ||
               placedNode != null || placedUnit != null || placedTower != null || placedStructure != null;
    }

    public bool IsBuildingBlocked()
    {
        // Road and Water tiles block all placement (env and player buildings alike).
        return type == TileType.Water || type == TileType.Road ||
               placedNode != null || placedTower != null || placedStructure != null;
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
        // Units can cross structures but not land on them. 
        // We allow walking if it's land and no other UNIT is blocking.
        return type == TileType.Land && placedUnit == null;
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