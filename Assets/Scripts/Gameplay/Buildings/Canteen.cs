using UnityEngine;

public class Canteen : StructureNode
{
    [Header("Era Visuals")]
    public GameObject industrialVisual;
    public GameObject early80sVisual;
    public GameObject retroVisual;
    public GameObject futuristicVisual;
    private GameObject currentVisualObj;

    public override void Initialize(HexTile tile, PlayerData player)
    {
        expansionRadius = 3; 
        baseGoldCost = 150;
        base.Initialize(tile, player);
        UpdateEraVisuals();
    }

    public override void UpdateEraVisuals()
    {
        if (TurnManager.Instance == null) return;
        TurnManager.GameEra era = TurnManager.Instance.GetCurrentEra();

        // 1. Turn OFF all visuals
        if (industrialVisual != null) industrialVisual.SetActive(false);
        if (early80sVisual != null) early80sVisual.SetActive(false);
        if (retroVisual != null) retroVisual.SetActive(false);
        if (futuristicVisual != null) futuristicVisual.SetActive(false);

        // 2. Turn ON the matching era visual
        GameObject activeVisual = industrialVisual;
        if (era == TurnManager.GameEra.EarlyEighties && early80sVisual != null) activeVisual = early80sVisual;
        else if (era == TurnManager.GameEra.Retro && retroVisual != null) activeVisual = retroVisual;
        else if (era == TurnManager.GameEra.Futuristic && futuristicVisual != null) activeVisual = futuristicVisual;

        if (activeVisual != null) 
        {
            activeVisual.SetActive(true);
            foreach (var col in activeVisual.GetComponentsInChildren<Collider>())
                Destroy(col);
        }

        // 3. Apply state normally via HologramUtil on the root
        if (!IsBuilt)

            HologramUtil.MakeHologram(gameObject, new Color(0f, 0.5f, 1f, 0.35f));
        else
            HologramUtil.MakeSolid(gameObject);

        // 4. Darken broken state on the active visual child (if any)
        if (IsBroken && activeVisual != null)
        {
            foreach (Renderer r in activeVisual.GetComponentsInChildren<Renderer>())
                r.material.color = Color.Lerp(r.material.color, Color.black, 0.5f);
        }
    }

    public override string GetRequiredTechFeature() => "Canteens";
}
