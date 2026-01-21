using UnityEngine;
using System.Collections.Generic;

public class SignalNode : MonoBehaviour
{
    public PlayerData owner;
    public HexTile tile;
    public int range = 5;
    private GameObject rangeIndicator;

    public int influenceRadius;
    public GameObject businessBuilding;

    public int towersPlacedCount = 0;
    public int maxTowers = 2;

    public bool CanPlaceTower() => towersPlacedCount < maxTowers;

    public void Initialize(PlayerData player, HexTile hexTile)
    {
        owner = player;
        tile = hexTile;

        tile.placedNode = this;
        player.ownedNodes.Add(this);

        businessBuilding = this.gameObject;

        if (!player.isAI)
            CreateRangeIndicator();

        ApplyInfluence();

        if (PowerGridManager.Instance != null)
        {
            PowerGridManager.Instance.RegisterSource(this);
            PowerGridManager.Instance.RefreshGrid();
        }
    }

    void CreateRangeIndicator()
    {
        rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        Vector3 indicatorPos = tile.transform.position;
        indicatorPos.y = 1f;
        rangeIndicator.transform.position = indicatorPos;

        float visualRadius = influenceRadius * GridManager.Instance.hexSize;

        rangeIndicator.transform.localScale =
            new Vector3(visualRadius * 2f, 0.01f, visualRadius * 2f);

        Renderer rend = rangeIndicator.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));
        rend.material.color = new Color(0f, 0.5f, 1f, 0.25f);

        Destroy(rangeIndicator.GetComponent<Collider>());
        rangeIndicator.SetActive(false);
    }

    public float GetVisualRadius()
    {
        if (rangeIndicator == null)
            return 0f;

        return rangeIndicator.transform.localScale.x / 2f;
    }

    void OnMouseEnter()
    {
        if (rangeIndicator != null)
            rangeIndicator.SetActive(true);
    }

    void OnMouseExit()
    {
        if (rangeIndicator != null)
            rangeIndicator.SetActive(false);
    }

    // OnMouseDown removed - Input handled via PlayerInput Raycast

    public bool IsTileWithinInfluence(HexTile target)
    {
        int dist = HexDistance(tile.cubeCoords, target.cubeCoords);
        return dist <= influenceRadius;
    }

    int HexDistance(Vector3Int a, Vector3Int b)
    {
        return (Mathf.Abs(a.x - b.x)
              + Mathf.Abs(a.y - b.y)
              + Mathf.Abs(a.z - b.z)) / 2;
    }

    void ApplyInfluence()
    {
        var tilesInRange = GridManager.Instance.GetTilesInRange(tile, influenceRadius);

        foreach (HexTile t in tilesInRange)
        {
            t.influence += t.baseInfluence;
            Debug.Log($"{t.name} gained +{t.baseInfluence} influence from SignalNode");
        }
    }
}
