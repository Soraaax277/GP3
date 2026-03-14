using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameState
{
    public int playerResources;
    public int enemyResources;
    public int currentTurn;
    public int currentPlayerIndex;
    public string currentEra;
    
    // Player Tech/State
    public int playerResearchPoints;
    public int enemyResearchPoints;
    public int playerHardwareEra;
    public int playerWorkforceEra;
    public int enemyHardwareEra;
    public int enemyWorkforceEra;
    
    public List<string> playerUnlockedTechs;
    public List<string> enemyUnlockedTechs;

    // In-progress (queued) research — parallel lists: name + turns remaining.
    // Populated by TechManager.GetActiveResearchFor; restored via LoadActiveResearch.
    public List<string> playerActiveResearchNames;
    public List<int>    playerActiveResearchTurns;
    public List<string> enemyActiveResearchNames;
    public List<int>    enemyActiveResearchTurns;
    
    // Infra Stats (Strings for serializable dict)
    public List<string> infraMultiplierKeys;
    public List<float> infraMultiplierValues;
    public List<string> infraFlatKeys;
    public List<float> infraFlatValues;
    
    public List<UnitData> playerUnits;
    public List<UnitData> enemyUnits;
    public List<BuildingData> buildings;
    public List<TowerData> towers;
    public List<WireData> wires;
    public List<TileInfluenceData> tileInfluences;

    public GameState()
    {
        playerUnits = new List<UnitData>();
        enemyUnits = new List<UnitData>();
        buildings = new List<BuildingData>();
        towers = new List<TowerData>();
        wires = new List<WireData>();
        tileInfluences = new List<TileInfluenceData>();
        playerUnlockedTechs = new List<string>();
        enemyUnlockedTechs = new List<string>();
        playerActiveResearchNames = new List<string>();
        playerActiveResearchTurns = new List<int>();
        enemyActiveResearchNames  = new List<string>();
        enemyActiveResearchTurns  = new List<int>();
        infraMultiplierKeys = new List<string>();
        infraMultiplierValues = new List<float>();
        infraFlatKeys = new List<string>();
        infraFlatValues = new List<float>();
    }
}

[Serializable]
public class UnitData
{
    public string unitType;
    public int tileX;
    public int tileY;
    public bool canAct;
    public int actionsRemaining;
    public float movementRemaining;
    public int specialCharges;
}

[Serializable]
public class BuildingData
{
    public string buildingType;
    public int tileX;
    public int tileY;
    public int towersPlaced;
    public bool isPlayerOwned;
}

[Serializable]
public class TowerData
{
    public int tileX;
    public int tileY;
    public string state;
    public bool isPlayerOwned;
    public int parentNodeX;
    public int parentNodeY;
    public float currentDurability;
}

[Serializable]
public class WireData
{
    public int tileX;
    public int tileY;
    public bool isPlayerOwned;
    public float currentDurability;
}

[Serializable]
public class TileInfluenceData
{
    public int tileX;
    public int tileY;
    public int playerInfluence;
    public int enemyInfluence;
}