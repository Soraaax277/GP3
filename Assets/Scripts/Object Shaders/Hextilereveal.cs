using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  HexTileReveal.cs
//
//  Attach to your GridManager GameObject.
//
//  WHAT IT DOES:
//    When a tile is revealed it does two things:
//      1. Swaps the tile renderer from StylizedSurface_Hidden (no DepthNormals,
//         no Outline pass) to StylizedSurface (with both passes).
//      2. Calls SetActive(true) on every direct child GameObject of the tile
//         (Env_Structure buildings, Env_Nature props). These were SetActive(false)
//         at spawn time by GridManager if the tile was unexplored.
//
//  INSPECTOR:
//    hiddenMaterial   → material using Custom/URP/StylizedSurface_Hidden
//    revealedMaterial → material using Custom/URP/StylizedSurface
// ─────────────────────────────────────────────────────────────────────────────
public class HexTileReveal : MonoBehaviour
{
    public static HexTileReveal Instance;

    [Tooltip("Assign the same material you put in GridManager's 'Grass Material' field.\n"
           + "Must use shader: Custom/URP/StylizedSurface_Hidden")]
    public Material hiddenMaterial;

    [Tooltip("The fully visible stylized material.\n"
           + "Must use shader: Custom/URP/StylizedSurface\n"
           + "Give it the same color/texture as hiddenMaterial.")]
    public Material revealedMaterial;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    // Called by HexFogRenderer for every tile that transitions from unexplored
    // to explored (both gradual reveal and cheat-reveal).
    public void RevealTile(HexTile tile)
    {
        if (tile == null) return;

        // 1. Swap the tile's own renderer material
        Renderer tileRend = tile.GetComponent<Renderer>();
        if (tileRend != null && revealedMaterial != null)
        {
            if (tileRend.sharedMaterial == hiddenMaterial)
                tileRend.material = revealedMaterial;
        }

        // 2. Re-activate all child objects (buildings, nature props)
        for (int i = 0; i < tile.transform.childCount; i++)
            tile.transform.GetChild(i).gameObject.SetActive(true);
    }

    // Called by HexFogRenderer.RevealAllInstant() for the cheat-reveal path.
    public void RevealAllTiles()
    {
        if (GridManager.Instance == null) return;
        foreach (HexTile tile in GridManager.Instance.GetAllTiles())
            RevealTile(tile);
    }
}