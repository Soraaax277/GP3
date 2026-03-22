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
    public float mapSeedX;
    public float mapSeedY;
    
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
    public List<StructureData> structures;
    public List<TowerData> towers;
    public List<WireData> wires;
    public List<TileInfluenceData> tileInfluences;
    public QuestState questState;

    public GameState()
    {
        playerUnits = new List<UnitData>();
        enemyUnits = new List<UnitData>();
        buildings = new List<BuildingData>();
        structures = new List<StructureData>();
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
        questState = new QuestState();
    }
}

[Serializable]
public class StructureData
{
    public string structureType;
    public string featureKey;
    public int tileX;
    public int tileY;
    public bool isPlayerOwned;
    public bool isBuilt;
    public bool isBroken;
    public float currentDurability;
}

[Serializable]
public class QuestState
{
    public List<string> playerActiveQuestIds = new List<string>();
    public List<string> playerCompletedQuestIds = new List<string>();
    public List<string> playerQuestFlags = new List<string>();
    
    public List<string> enemyActiveQuestIds = new List<string>();
    public List<string> enemyCompletedQuestIds = new List<string>();
    public List<string> enemyQuestFlags = new List<string>();
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
    public int level;
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
    public float rotationY; // Legacy compatibility
    
    // Explicit Transform Values
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ, rotW;
    public float sclX, sclY, sclZ;

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
    public int influenceSuppression;
}