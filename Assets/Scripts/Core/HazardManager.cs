using UnityEngine;
using System.Collections.Generic;

public class HazardManager : MonoBehaviour
{
    public static HazardManager Instance;

    public GameObject saboteurPrefab; // The "Capsule" mini-human
    public GameObject geyserPrefab; // The "Hotspot"
    public List<NomadicSaboteur> activeSaboteurs = new List<NomadicSaboteur>();
    public List<ResourceGeyser> activeGeysers = new List<ResourceGeyser>();

    private void Awake()
    {
        Instance = this;
    }

    public void ProcessTurnHazards()
    {
        // 1. Spawning new Saboteurs
        if (Random.value < 0.3f) 
        {
            SpawnSaboteur();
        }

        // 2. Spawning new Geysers
        if (Random.value < 0.2f)
        {
            SpawnGeyser();
        }

        // 3. Updating existing saboteurs
        for (int i = activeSaboteurs.Count - 1; i >= 0; i--)
        {
            if (activeSaboteurs[i] == null)
            {
                activeSaboteurs.RemoveAt(i);
                continue;
            }
            activeSaboteurs[i].PerformTurnAction();
        }

        // 4. Updating existing geysers
        for (int i = activeGeysers.Count - 1; i >= 0; i--)
        {
            if (activeGeysers[i] == null)
            {
                activeGeysers.RemoveAt(i);
                continue;
            }
            activeGeysers[i].ProcessTurn();
        }
    }

    private void SpawnGeyser()
    {
        List<HexTile> allTiles = new List<HexTile>(GridManager.Instance.GetAllTiles());
        HexTile spawnTile = null;
        int attempts = 0;
        while (attempts < 20)
        {
            HexTile t = allTiles[Random.Range(0, allTiles.Count)];
            if (t.type == HexTile.TileType.Land && !t.IsOccupied())
            {
                spawnTile = t;
                break;
            }
            attempts++;
        }

        if (spawnTile != null)
        {
            GameObject obj;
            if (geyserPrefab != null)
            {
                obj = Instantiate(geyserPrefab, spawnTile.transform.position + Vector3.up * 0.1f, Quaternion.identity);
            }
            else
            {
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.name = "ResourceGeyser";
                obj.transform.position = spawnTile.transform.position + Vector3.up * 0.5f;
                obj.transform.localScale = new Vector3(0.8f, 0.2f, 0.8f);
                var rend = obj.GetComponent<Renderer>();
                rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                rend.material.color = Color.cyan;
            }

            ResourceGeyser script = obj.GetComponent<ResourceGeyser>();
            if (script == null) script = obj.AddComponent<ResourceGeyser>();
            
            script.Initialize(spawnTile);
            activeGeysers.Add(script);
            Debug.Log("[HazardManager] Spawned Resource Geyser at " + spawnTile.cubeCoords);
        }
    }

    private void SpawnSaboteur()
    {
        // Try to find a random edge tile or neutral tile
        List<HexTile> allTiles = new List<HexTile>(GridManager.Instance.GetAllTiles());
        if (allTiles.Count == 0) return;

        HexTile spawnTile = null;
        int attempts = 0;
        while (attempts < 10)
        {
            HexTile t = allTiles[Random.Range(0, allTiles.Count)];
            if (t.type == HexTile.TileType.Land && !t.IsOccupied())
            {
                spawnTile = t;
                break;
            }
            attempts++;
        }

        if (spawnTile != null)
        {
            GameObject obj;
            if (saboteurPrefab != null)
            {
                obj = Instantiate(saboteurPrefab, spawnTile.transform.position + Vector3.up, Quaternion.identity);
            }
            else
            {
                // Create a capsule mini-human procedurally
                obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                obj.name = "NomadicSaboteur";
                obj.transform.position = spawnTile.transform.position + Vector3.up;
                obj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f); // "Mini human"
                
                // Red color for visibility
                var rend = obj.GetComponent<Renderer>();
                rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                rend.material.color = Color.red;
            }

            NomadicSaboteur script = obj.GetComponent<NomadicSaboteur>();
            if (script == null) script = obj.AddComponent<NomadicSaboteur>();
            
            script.Initialize(spawnTile);
            activeSaboteurs.Add(script);
            
            Debug.Log("[HazardManager] Spawned Nomadic Saboteur at " + spawnTile.cubeCoords);
        }
    }
}
