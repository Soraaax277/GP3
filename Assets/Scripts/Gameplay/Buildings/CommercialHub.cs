using UnityEngine;
using System.Collections.Generic;

public class CommercialHub : StructureNode
{
    [Header("Auto-Spawn Settings")]
    [Tooltip("When true, this hub automatically attempts to recruit a valid worker each turn if the player can afford it.")]
    public bool autoSpawnEnabled = false;

    private void Awake() { tilesOccupied = 4; }

    public override void Initialize(List<HexTile> tiles, PlayerData player)
    {
        baseGoldCost = 600;
        base.Initialize(tiles, player);
    }

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

        // TAX HAVEN: 50% discount if manned by a Marketer
        bool discounted = IsMannedBy<SalesMarketer>();
        if (discounted) cost = Mathf.RoundToInt(cost * 0.5f);

        if (owner.resources < cost)
        {
            Debug.Log($"[CommercialHub] Auto-spawn: cannot afford {prefab.name} ({cost}G) {(discounted ? "(Discounted)" : "")}.");
            return;
        }

        owner.resources -= cost; // Actually spend the money for auto-spawn
        Unit spawned = UnitSpawner.Instance.SpawnUnit(prefab, ParentTile, owner);
        if (spawned != null)
        {
            string friendlyName = spawned.GetType().Name.Replace("Unit", "");
            ActionLogUI.PostFiltered(owner, $"Auto-spawned {friendlyName}", owner.isAI ? ActionLogUI.Colors.Enemy : ActionLogUI.Colors.Player);
        }
    }



    public void ToggleAutoSpawn()
    {
        autoSpawnEnabled = !autoSpawnEnabled;
        Debug.Log($"[CommercialHub] Auto-spawn is now {(autoSpawnEnabled ? "ON" : "OFF")}.");
    }

    public override string GetRequiredTechFeature() => "CommercialHubs";
}