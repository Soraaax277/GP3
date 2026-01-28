using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameState
{
    public int playerResources;
    public int enemyResources;
    public int currentTurn;
    public string currentEra;
    
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
}

[Serializable]
public class WireData
{
    public int tileX;
    public int tileY;
    public bool isPlayerOwned;
}

[Serializable]
public class TileInfluenceData
{
    public int tileX;
    public int tileY;
    public int playerInfluence;
    public int enemyInfluence;
}
