using UnityEngine;

public class HexTile : MonoBehaviour
{
    public Vector3Int cubeCoords;
    public SignalNode placedNode;
    public TowerNode placedTower;
    public Unit placedUnit;

    public void Initialize(Vector3Int coords)
    {
        cubeCoords = coords;
        name = $"Hex {coords.x},{coords.y},{coords.z}";
    }

    public bool IsOccupied()
    {
        return placedNode != null || placedUnit != null;
    }

    public bool HasTower()
    {
        return placedTower != null;
    }
}
