using UnityEngine;

public class HexTile : MonoBehaviour
{
    public Vector3Int cubeCoords;
    public SignalNode placedNode;
    public TowerNode placedTower;
    public WireNode placedWire;
    public Unit placedUnit;

    public int baseInfluence;
    public int influence;

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
        influence = baseInfluence;

        Debug.Log($"{name} influence: {influence}");
    }


    public bool IsOccupied()
    {
        return placedNode != null || placedUnit != null || placedWire != null || placedTower != null;
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
}
