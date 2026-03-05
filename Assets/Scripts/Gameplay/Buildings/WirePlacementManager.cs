using UnityEngine;

public class WirePlacementManager : MonoBehaviour
{
    public static WirePlacementManager Instance;

    public GameObject wirePrefab;
    private GameObject hologram;
    private HexTile hoveredTile;
    private bool isPlacing;
    private WireSpecialist currentSpecialist;
    private float currentYRotation = 0f;
    private float lastStartTime;
    private bool isTileValid;

    public bool IsPlacing => isPlacing;

    //  WIRE LENGTH  ("WireLength" flat bonus tech)
    //  Base = 1 hex from the specialist unit.
    //  Increased by the "WireLength" TechEffect flat bonus (e.g. +2 from Coaxial Cables).
    public int MaxWireLength
    {
        get
        {
            int baseLen = 1;
            if (TechManager.Instance == null) return baseLen;
            int bonus = Mathf.RoundToInt(TechManager.Instance.GetInfraFlatBonus("WireLength"));
            return baseLen + bonus;
        }
    }

    //  WIRE COST  ("WireCost" multiplier tech)
    //  Base = 10 gold per wire tile.
    //  Reduced by the "WireCost" TechEffect multiplier (set value to -0.1 for 10% off).
    //  Default GetInfraMultiplier returns 1.0, so: 10 * 1.0 = 10 gold (no tech).
    //  With -0.1 applied: 10 * 0.9 = 9 gold.
    public int baseCost = 10;

    public int GetCurrentWireCost()
    {
        if (TechManager.Instance == null) return baseCost;
        float multiplier = TechManager.Instance.GetInfraMultiplier("WireCost");
        return Mathf.Max(0, Mathf.RoundToInt(baseCost * multiplier));
    }

    void Awake() => Instance = this;

    void Update()
    {
        if (!isPlacing) return;

        FollowMouse();
        HandleRotation();

        if (Time.time < lastStartTime + 0.1f) return;

        if (Input.GetMouseButtonDown(0) && hoveredTile != null && isTileValid)
        {
            PlaceWire();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
        }
    }

    void HandleRotation()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            currentYRotation += 60f;
            hologram.transform.rotation = Quaternion.Euler(0, currentYRotation, 90);
        }
    }

    public void StartWirePlacement(WireSpecialist specialist)
    {
        if (isPlacing || specialist == null) return;

        isPlacing = true;
        currentSpecialist = specialist;
        currentYRotation = 0f;
        lastStartTime = Time.time;

        if (BuildingUIManager.Instance != null)
             BuildingUIManager.Instance.ignoreNextClick = true;

        if (wirePrefab != null)
        {
            hologram = Instantiate(wirePrefab, specialist.transform.position, Quaternion.Euler(0, 0, 90));
            HologramUtil.MakeHologram(hologram, new Color(1f, 1f, 0f, 0.4f));
        }
        else
        {
            hologram = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hologram.transform.localScale = new Vector3(0.2f, 0.05f, 0.2f);
            Destroy(hologram.GetComponent<Collider>());
            
            Renderer rend = hologram.GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = new Color(1f, 1f, 0f, 0.4f);
        }
    }

    void FollowMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        HexTile tile = hit.collider.GetComponent<HexTile>();
        if (tile == null) return;

        hoveredTile = tile;
        hologram.transform.position = tile.transform.position + Vector3.up * 0.84f;

        // VALIDITY CHECK
        int distFromUnit = GridManager.Instance.CubeDistance(
            currentSpecialist.currentTile.cubeCoords, tile.cubeCoords);

        // Check wire length against tech limit
        bool isWithinReach = distFromUnit <= MaxWireLength;

        // Must be adjacent to an owned network tile (node / wire / powered tower)
        // Previously accepted ANY player's infrastructure. Now checks ownership
        // so wires must connect to the specialist's own network.
        bool isNextToPower = false;
        if (GridManager.Instance != null)
        {
            foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(tile))
            {
                bool ownedNode  = neighbor.placedNode  != null && neighbor.placedNode.owner  == currentSpecialist.owner;
                bool ownedTower = neighbor.placedTower != null && neighbor.placedTower.owner == currentSpecialist.owner;
                bool ownedWire  = neighbor.placedWire  != null && neighbor.placedWire.owner  == currentSpecialist.owner;

                if (ownedNode || ownedTower || ownedWire)
                {
                    isNextToPower = true;
                    break;
                }
            }
        }

        // Player must be able to afford it
        int cost = GetCurrentWireCost();
        bool canAfford = currentSpecialist.owner != null && currentSpecialist.owner.resources >= cost;

        // Block wires over water
        bool environmentBlocked = tile.type == HexTile.TileType.Water;

        bool valid = isWithinReach && isNextToPower && !tile.IsOccupied() && !tile.HasWire() && canAfford && !environmentBlocked;
        isTileValid = valid;

        // Colour the hologram: yellow = valid, red = invalid
        Color holoColor = valid ? new Color(1f, 1f, 0f, 0.4f) : new Color(1f, 0f, 0f, 0.4f);
        
        if (wirePrefab != null)
            HologramUtil.MakeHologram(hologram, holoColor);
        else
            hologram.GetComponent<Renderer>().material.color = holoColor;
    }

    void PlaceWire()
    {
        if (hoveredTile == null || !isTileValid) return;

        // DEDUCT WIRE COST from player resources
        int cost = GetCurrentWireCost();
        if (currentSpecialist.owner.resources < cost)
        {
            Debug.Log($"[WirePlacement] Not enough gold! Need {cost}, have {currentSpecialist.owner.resources}");
            CancelPlacement();
            return;
        }

        currentSpecialist.owner.resources -= cost;
        Debug.Log($"[WirePlacement] Wire placed for {cost} gold. " +
                  $"Remaining: {currentSpecialist.owner.resources}");

        currentSpecialist.BuildWire(hoveredTile, currentYRotation);
        CancelPlacement();
    }

    void CancelPlacement()
    {
        if (hologram != null) Destroy(hologram);
        isPlacing = false;
        currentSpecialist = null;
    }

    public WireNode PlaceWireDirect(HexTile tile, PlayerData owner)
    {
        if (tile == null || wirePrefab == null) return null;

        GameObject wireObj = Instantiate(
            wirePrefab,
            tile.transform.position + Vector3.up * 0.84f,
            Quaternion.Euler(0, 0, 90)
        );

        HologramUtil.MakeSolid(wireObj);

        WireNode wire = wireObj.GetComponent<WireNode>();
        wire.Initialize(tile, owner);

        // Register with TurnManager so it participates in decay each turn
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RegisterWire(wire);
        }

        return wire;
    }
}