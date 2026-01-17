using UnityEngine;

public abstract class Unit : MonoBehaviour
{
    public HexTile currentTile;
    public PlayerData owner;

    public bool canAct;
    public bool isFresh;
    public bool testingMode = false;

    private Renderer[] renderers;
    private Material[] originalMaterials;
    private Material outlineMaterial;

    public bool forceCanAct = false;

    public bool CanAct => canAct;
    public bool CanSelect => owner == TurnManager.Instance.currentPlayer;
    public bool IsFresh => isFresh;

    public virtual void Initialize(HexTile spawnTile, PlayerData player)
    {
        currentTile = spawnTile;
        owner = player;

        transform.position = spawnTile.transform.position + Vector3.up * 1f;

        isFresh = true;
        canAct = false;

        spawnTile.placedUnit = this;

        TurnManager.Instance.RegisterUnit(this);

        renderers = GetComponentsInChildren<Renderer>();

        originalMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i].material;
        }

        outlineMaterial = new Material(Shader.Find("Sprites/Default"));
        outlineMaterial.color = new Color(0f, 1f, 0f, 0.7f);

        if (testingMode)
        {
            canAct = true;
            isFresh = false;
        }
        else
        {
            canAct = false;
            isFresh = true;
        }
    }

    public void SetSelected(bool selected)
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material = selected ? outlineMaterial : originalMaterials[i];
        }
    }

    public void OnTurnStart(PlayerData activePlayer)
    {
        if (owner != activePlayer) return;

        isFresh = false;
        canAct = true;
    }

    public void ConsumeAction()
    {
        canAct = false;
    }

    public void MoveTo(HexTile tile, int allowedRange)
    {
        if (TurnManager.Instance.currentPlayer != owner && !testingMode)
        {
            Debug.Log("Wait for your turn again!");
            return;
        }

        if (!CanMoveTo(tile, allowedRange))
        {
            Debug.Log("Cannot move there!");
            return;
        }

        if (currentTile != null)
            currentTile.placedUnit = null;

        currentTile = tile;
        transform.position = tile.transform.position + Vector3.up * 1f;

        tile.placedUnit = this;

        ConsumeAction();

        SetSelected(false);
        PlayerInput.Instance.ClearHighlights();
    }

    public bool CanMoveTo(HexTile tile, int allowedRange)
    {
        if (!canAct && !testingMode)
            return false;

        int dist = GridManager.Instance.CubeDistance(currentTile.cubeCoords, tile.cubeCoords);

        Debug.Log($"CanMoveTo check: dist={dist}, allowed={allowedRange}, occupied={tile.IsOccupied()}, hasTower={tile.HasTower()}");

        if (dist > allowedRange) return false;
        if (tile == currentTile) return true;
        if (tile.IsOccupied() || tile.HasTower()) return false;

        return true;
    }
}
