using UnityEngine;

public class PowerBox : StructureNode
{
    public override void OnTurnStart()
    {
        if (!IsPowered || !IsMannedBy<Technician>()) return;

        // ENERGY TRADING: High risk / High reward
        if (Random.value < 0.10f) // 10% chance to blow a fuse
        {
            ActionLogUI.PostFiltered(owner, "Power Box: Trading failed! Blew a fuse.", ActionLogUI.Colors.Neutral);
            // Optionally could disable power here
            return;
        }

        int income = Random.Range(20, 101); // 20G to 100G
        owner.resources += income;
        ActionLogUI.PostFiltered(owner, $"Energy Trading: Sold surplus power for {income}G.", ActionLogUI.Colors.Neutral);
    }

    public override string GetRequiredTechFeature() => "PowerBoxes";
}
