using UnityEngine;
using System.Collections.Generic;

public class ResearchProjectHandler : MonoBehaviour
{
    public static ResearchProjectHandler Instance;

    public class ActiveProject
    {
        public string techID;
        public Technician assignedTechnician;
        public int turnsRemaining;
        public int goldPerTurn;
        public int rpPerTurn;
    }

    private List<ActiveProject> activeProjects = new List<ActiveProject>();

    private void Awake()
    {
        Instance = this;
    }

    public void StartProject(Technician tech, string techID, int duration = 3, int goldCost = 50, int rpCost = 100)
    {
        ActiveProject project = new ActiveProject
        {
            techID = techID,
            assignedTechnician = tech,
            turnsRemaining = duration,
            goldPerTurn = goldCost,
            rpPerTurn = rpCost
        };
        
        activeProjects.Add(project);
        tech.isResearching = true;
        Debug.Log($"[Research] Started {techID} project with Technician {tech.name}");
    }

    public void OnTurnEnd(PlayerData player)
    {
        for (int i = activeProjects.Count - 1; i >= 0; i--)
        {
            var p = activeProjects[i];
            if (p.assignedTechnician.owner == player)
            {
                // Drain resources
                player.resources -= p.goldPerTurn;
                player.researchPoints -= p.rpPerTurn; // Assuming rp exist in PlayerData
                
                p.turnsRemaining--;
                
                if (p.turnsRemaining <= 0)
                {
                    CompleteProject(p);
                    activeProjects.RemoveAt(i);
                }
            }
        }
    }

    private void CompleteProject(ActiveProject p)
    {
        p.assignedTechnician.isResearching = false;
        
        if (TechManager.Instance != null)
        {
            // Assuming TechManager has a way to force unlock or similar
            // For now just log
            Debug.Log($"<color=cyan>[Research] Project COMPLETED: {p.techID}</color>");
            TechManager.Instance.UnlockTechExplicitly(p.techID);
        }
    }
}
