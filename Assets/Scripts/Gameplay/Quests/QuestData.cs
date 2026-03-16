using UnityEngine;

[System.Serializable]
public class QuestData
{
    public string id;
    public string description;
    public QuestTier tier;
    public TurnManager.GameEra era;
    public int startTurn;
    public int endTurn; 
    public int goldReward;
    public int rpReward;

    public QuestData(string id, string desc, QuestTier tier, TurnManager.GameEra era, int start, int end, int gold, int rp)
    {
        this.id = id;
        this.description = desc;
        this.tier = tier;
        this.era = era;
        this.startTurn = start;
        this.endTurn = end;
        this.goldReward = gold;
        this.rpReward = rp;
    }
}

public enum QuestTier { Mini, Main, Major }
