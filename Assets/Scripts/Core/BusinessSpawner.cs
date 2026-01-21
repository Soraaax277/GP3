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

        Vector3 spawnPos = chosenTile.transform.position + new Vector3(0f, 1.51f, 0f);
        GameObject businessObj = Instantiate(businessPrefab, spawnPos, businessPrefab.transform.rotation);

        SignalNode node = businessObj.GetComponent<SignalNode>();
        if (node == null)
        {
            Debug.LogError("Business prefab missing SignalNode component!");
            Destroy(businessObj);
            return null;
        }

        node.Initialize(chosenTile, player);

        return node;
    }
}
