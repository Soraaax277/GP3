using UnityEngine;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    public void ProcessTurnIncome(PlayerData player)
    {
        // GET DATA
        int totalInfluence = InfluenceManager.Instance.GetTotalInfluence(player);
        int activeTowers   = player.GetActiveTowerCount();
        int ownedBases     = player.ownedNodes.Count;

        // CALCULATE GOLD
        // Formula: (Total Influence) + (Active Towers * 50) + Base(100)
        float baseGold      = totalInfluence + (activeTowers * 50) + 100;
        float goldMultiplier = 1.0f;
        float finalRevenueMultiplier = 1.0f;

        if (TechManager.Instance != null)
        {
            // "TowerRevenue" — multiplier on base tower/influence income
            goldMultiplier = TechManager.Instance.GetInfraMultiplier("TowerRevenue");

            // "FinalRevenueGain" — applied AFTER TowerRevenue, stacks on top of everything.
            // Era Progression nodes, Broadband (+10%), and State-of-the-Art Telecom Hardware
            // all feed into this. Set TechEffect: isMultiplier=✅.
            finalRevenueMultiplier = TechManager.Instance.GetInfraMultiplier("FinalRevenueGain");
        }

        // Apply both multipliers: base income first, then the final gain on top
        int finalGold = Mathf.RoundToInt(baseGold * goldMultiplier * finalRevenueMultiplier);
        player.resources += finalGold;

        // CALCULATE RESEARCH POINTS (RP)
        // Formula: (Signal Nodes * 15) + Base(5)
        float baseRP = (ownedBases * 15) + 5;
        // float baseRP = (ownedBases * 15) + 99999999; // TESTING: Massive RP income to test tech unlocking
        float rpMultiplier = 1.0f;

        if (TechManager.Instance != null)
        {
            // "ResearchGain" — multiplier on RP income.
            // Retro Tech Progression adds +30% (infraValueMod: 0.3, isMultiplier: ✅).
            rpMultiplier = TechManager.Instance.GetInfraMultiplier("ResearchGain");
        }

        // Add flat RP bonus from all unlocked TechNodes that have rpBonusPerTurn set
        int rpFlatBonus = TechManager.Instance != null ? TechManager.Instance.GetTotalRPBonus() : 0;

        int finalRP = Mathf.RoundToInt(baseRP * rpMultiplier) + rpFlatBonus;
        player.researchPoints += finalRP;

        // CALCULATE & SUBTRACT UPKEEP (System 3)
        int totalUpkeep = CalculateTotalUpkeep(player);
        player.resources -= totalUpkeep;

        // LOGGING
        Debug.Log($"[Economy] {player.playerName} Income:");
        Debug.Log($" > Gold Income : +{finalGold} " +
                  $"(Base: {baseGold} x TowerRevenue: {goldMultiplier} x FinalGain: {finalRevenueMultiplier})");
        Debug.Log($" > RP          : +{finalRP} (Base: {baseRP} x ResearchGain: {rpMultiplier} + FlatBonus: {rpFlatBonus})");
        Debug.Log($" > Upkeep      : -{totalUpkeep}");
        Debug.Log($" > Net Gold    : {player.resources}");
    }

    //  UPKEEP  (System 3)
    private int CalculateTotalUpkeep(PlayerData player)
    {
        if (TurnManager.Instance == null) return 0;

        float upkeepMultiplier = TurnManager.Instance.GetUpkeepMultiplier(player);

        int raw = 0;

        foreach (TowerNode tower in TurnManager.Instance.GetAllTowers())
        {
            if (tower != null && tower.owner == player)
                raw += tower.GetCurrentUpkeep();
        }

        foreach (WireNode wire in TurnManager.Instance.GetAllWires())
        {
            if (wire != null && wire.owner == player)
                raw += wire.GetCurrentUpkeep();
        }

        foreach (Unit unit in TurnManager.Instance.GetAllUnits())
        {
            if (unit != null && unit.owner == player)
                raw += unit.goldUpkeep;
        }

        int total = Mathf.RoundToInt(raw * upkeepMultiplier);

        if (upkeepMultiplier > 1f)
        {
            Debug.Log($"[Economy] {player.playerName} Labor Mismatch! Upkeep x{upkeepMultiplier} " +
                      $"(Hardware: {player.hardwareEra}, Workforce: {player.workforceEra})");
        }

        Debug.Log($"[Economy] {player.playerName} Total Upkeep: {total} " +
                  $"(Raw: {raw} x Multiplier: {upkeepMultiplier})");

        return total;
    }

    //  PROJECTIONS (for UI breakdown)
    
    public int GetProjectedGoldIncome(PlayerData player)
    {
        int totalInfluence = InfluenceManager.Instance != null ? InfluenceManager.Instance.GetTotalInfluence(player) : 0;
        int activeTowers   = player.GetActiveTowerCount();
        
        float baseGold = totalInfluence + (activeTowers * 50) + 100;
        float goldMultiplier = 1.0f;
        float finalRevenueMultiplier = 1.0f;

        if (TechManager.Instance != null)
        {
            goldMultiplier = TechManager.Instance.GetInfraMultiplier("TowerRevenue");
            finalRevenueMultiplier = TechManager.Instance.GetInfraMultiplier("FinalRevenueGain");
        }

        return Mathf.RoundToInt(baseGold * goldMultiplier * finalRevenueMultiplier);
    }

    public int GetProjectedRPIncome(PlayerData player)
    {
        int ownedBases = player.ownedNodes.Count;
        float baseRP = (ownedBases * 15) + 5;
        float rpMultiplier = 1.0f;

        if (TechManager.Instance != null)
        {
            rpMultiplier = TechManager.Instance.GetInfraMultiplier("ResearchGain");
        }

        int rpFlatBonus = TechManager.Instance != null ? TechManager.Instance.GetTotalRPBonus() : 0;
        return Mathf.RoundToInt(baseRP * rpMultiplier) + rpFlatBonus;
    }

    public int GetProjectedUpkeep(PlayerData player)
    {
        return CalculateTotalUpkeep(player);
    }
}