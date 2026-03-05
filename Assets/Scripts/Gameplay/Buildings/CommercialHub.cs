using UnityEngine;

public class CommercialHub : StructureNode
{
    [Header("Auto-Spawn Settings")]
    [Tooltip("When true, this hub automatically attempts to recruit a valid worker each turn if the player can afford it.")]
    public bool autoSpawnEnabled = false;

    public override void OnTurnStart()
    {
        if (!IsPowered) return;
        if (!autoSpawnEnabled) return;
        if (UnitSpawner.Instance == null) return;

        // Auto-spawn attempts to recruit the cheapest available unit the player can afford.
        // Currently targets BuilderUnit as the default auto-spawn worker type.
        // To expand: add more unit types here in priority order and iterate until one is affordable.
        //
        // NOTE: To add more worker types to auto-spawn, add additional TryAutoSpawn calls below,
        // e.g. TryAutoSpawn(UnitSpawner.Instance.wireSpecialistPrefab);
        TryAutoSpawn(UnitSpawner.Instance.builderPrefab);
    }

    private void TryAutoSpawn(GameObject prefab)
    {
        if (prefab == null) return;

        int cost = UnitSpawner.Instance.GetRecruitmentCost(prefab);
        if (owner.resources < cost)
        {
            Debug.Log($"[CommercialHub] Auto-spawn: cannot afford {prefab.name} ({cost}G).");
            return;
        }

        Unit spawned = UnitSpawner.Instance.SpawnUnit(prefab, ParentTile, owner);
        if (spawned != null)
            Debug.Log($"[CommercialHub] Auto-spawned {spawned.GetType().Name} for {cost}G.");
    }

    private void OnMouseDown()
    {
        if (owner != TurnManager.Instance.currentPlayer || owner.isAI) return;
        BuildingUIManager.Instance?.Open(this);
    }

    public void ToggleAutoSpawn()
    {
        autoSpawnEnabled = !autoSpawnEnabled;
        Debug.Log($"[CommercialHub] Auto-spawn is now {(autoSpawnEnabled ? "ON" : "OFF")}.");
    }

    public override string GetRequiredTechFeature() => "CommercialHubs";
}