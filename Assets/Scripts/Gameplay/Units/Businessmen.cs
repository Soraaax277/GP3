using UnityEngine;

public class Businessman: Unit
{
    public int recruitCharges = 3;
    public int maxRecruitCharges = 3;

    public override int CurrentCharges { get => recruitCharges; set => recruitCharges = value; }
    public override int MaxCharges => maxRecruitCharges;
    
    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        base.Initialize(spawnTile, player);
    }

    public override void CheckTechStatus()
    {
        if (TechManager.Instance == null || owner == null) return;

        // 1. ERA SPECIFIC UPGRADES (Futuristic)
        if (owner.hardwareEra == TurnManager.PlayerEra.Futuristic)
        {
            maxRecruitCharges = 5;
            recruitCharges = Mathf.Max(recruitCharges, 5); 
        }
        else
        {
            moveRange = 2;
        }
    }
    
    public void RecruitNearestWorker()
    {
        if (!canAct && !testingMode)
        {
            Debug.Log("Businessman cannot act (turn/action used)");
            return;
        }
        
        Unit targetUnit = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            // Recruit enemy units (player's)
            if (neighbor.placedUnit != null && neighbor.placedUnit.owner != owner)
            {
                targetUnit = neighbor.placedUnit;
                break;
            }
        }

        if (targetUnit == null)
        {
            Debug.Log("No units adjacent!");
            return;
        }

        // 50% chance to recruit unit
        if (Random.value >= 0.5f) 
        { 
            if (AudioManager.Instance != null && AudioManager.Instance.recruitSFX != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.recruitSFX);

            targetUnit.Recruit(owner);
            ActionLogUI.PostFiltered(owner, "Businessman recruited a worker!", owner.isAI ? ActionLogUI.Colors.Enemy : ActionLogUI.Colors.Player);
            Debug.Log($"[Businessman] Successfully recruited {targetUnit.name}!");
        }
        else
        {
            if (AudioManager.Instance != null && AudioManager.Instance.denySFX != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.denySFX);

            Debug.Log("[Businessman] Recruitment failed.");
        }

        if (ShouldConsumeCharge())
            recruitCharges--;
            
        ConsumeAction();
    }
}
