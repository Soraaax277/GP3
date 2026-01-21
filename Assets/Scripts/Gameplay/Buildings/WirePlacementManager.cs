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

        BuildUIManager.Instance.ignoreNextClick = true;

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

        int distFromUnit = GridManager.Instance.CubeDistance(currentSpecialist.currentTile.cubeCoords, tile.cubeCoords);
        
        bool isNextToPower = false;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(tile))
        {
            if (neighbor.placedNode != null || neighbor.placedTower != null || neighbor.placedWire != null)
            {
                isNextToPower = true;
                break;
            }
        }

        bool valid = distFromUnit <= 1 && isNextToPower && !tile.IsOccupied();
        isTileValid = valid;
        
        Color holoColor = valid ? new Color(1f, 1f, 0f, 0.4f) : new Color(1f, 0f, 0f, 0.4f);
        
        if (wirePrefab != null)
            HologramUtil.MakeHologram(hologram, holoColor);
        else
            hologram.GetComponent<Renderer>().material.color = holoColor;
    }

    void PlaceWire()
    {
        if (hoveredTile == null || !isTileValid) return;

        currentSpecialist.BuildWire(hoveredTile, currentYRotation);
        
        if (currentSpecialist == null || currentSpecialist.wiresRemaining <= 0)
        {
            CancelPlacement();
        }
    }

    void CancelPlacement()
    {
        if (hologram != null) Destroy(hologram);
        isPlacing = false;
        currentSpecialist = null;
    }
}
