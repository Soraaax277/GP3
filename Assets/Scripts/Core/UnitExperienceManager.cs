using UnityEngine;

public class UnitExperienceManager : MonoBehaviour
{
    public static UnitExperienceManager Instance;

    [Header("XP Settings")]
    public int refillsPerLevel = 3;
    
    [Header("Level 2 Perks")]
    public int bonusMoveRange = 1;

    [Header("Level 3 Perks")]
    public float freeActionChance = 0.20f;

    private void Awake()
    {
        Instance = this;
    }

    public void NotifyLevelUp(Unit unit)
    {
        Debug.Log($"<color=yellow>[Veterancy]</color> {unit.name} has reached Level {unit.level}!");
        
        // Potential for UI popup or floating text here
        if (FeedbackController.Instance != null)
        {
             // FeedbackController.Instance.ShowLevelUpInfo(unit);
        }
    }
}
