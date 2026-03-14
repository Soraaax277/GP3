using UnityEngine;
using System.Collections.Generic;

public class BusinessSpawner : MonoBehaviour
{
    public GameObject businessPrefab;

    public SignalNode SpawnInitialBusiness(PlayerData player)
    {
        if (GridManager.Instance == null || !GridManager.Instance.IsReady)
        {
            Debug.LogError("GridManager not ready!");
            return null;
        }
        if (businessPrefab == null)
        {
            Debug.LogError("Business prefab not assigned!");
            return null;
        }

        List<HexTile> freeTiles = new List<HexTile>();
        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (!tile.IsOccupied())
                freeTiles.Add(tile);
        }

        if (freeTiles.Count == 0)
        {
            Debug.LogError("No free tiles available to spawn a business!");
            return null;
        }

        HexTile chosenTile = freeTiles[Random.Range(0, freeTiles.Count)];
        return SpawnBusiness(chosenTile, player);
    }

    public SignalNode SpawnBusiness(HexTile tile, PlayerData player)
    {
        if (tile == null || businessPrefab == null) return null;

        Vector3 spawnPos = new Vector3(
            tile.transform.position.x,
            GetBusinessPlacementY(tile, businessPrefab),
            tile.transform.position.z
        );

        GameObject businessObj = Instantiate(businessPrefab, spawnPos, businessPrefab.transform.rotation);

        SignalNode node = businessObj.GetComponent<SignalNode>();
        if (node == null)
        {
            Debug.LogError("Business prefab missing SignalNode component!");
            Destroy(businessObj);
            return null;
        }

        node.Initialize(tile, player);
        return node;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Collider-based placement helpers
    //  Mirrors TowerPlacementManager so buildings sit flush on the tile surface
    //  regardless of the hex tile's world-space Y, scale, or collider offset.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the world-space Y of the top surface of the tile's BoxCollider.
    /// Falls back to the tile's pivot Y if no BoxCollider is found.
    /// </summary>
    private float GetTileSurfaceY(HexTile tile)
    {
        BoxCollider box = tile.GetComponent<BoxCollider>();
        if (box == null) return tile.transform.position.y;

        float halfHeight = box.size.y * 0.5f * tile.transform.lossyScale.y;
        float centerY    = box.center.y  * tile.transform.lossyScale.y;
        return tile.transform.position.y + centerY + halfHeight;
    }

    /// <summary>
    /// Temporarily instantiates the prefab to measure the distance from its
    /// pivot down to the bottom of its MeshCollider. This offset is added to
    /// the tile surface Y so the mesh base lands exactly on the tile surface.
    /// Falls back to 0 if the prefab has no MeshCollider.
    /// </summary>
    private float GetBusinessPlacementY(HexTile tile, GameObject prefab)
    {
        float surfaceY = GetTileSurfaceY(tile);

        GameObject temp = Instantiate(prefab);
        float bottomOffset = 0f;

        MeshCollider mc = temp.GetComponentInChildren<MeshCollider>();
        if (mc != null)
            bottomOffset = temp.transform.position.y - mc.bounds.min.y;

        Destroy(temp);
        return surfaceY + bottomOffset;
    }
}