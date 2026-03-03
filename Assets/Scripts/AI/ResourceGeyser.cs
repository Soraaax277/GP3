using UnityEngine;

public class ResourceGeyser : MonoBehaviour
{
    public HexTile currentTile;
    public int goldBonus = 100;
    public int rpBonus = 50;
    public int duration = 5;

    public void Initialize(HexTile tile)
    {
        currentTile = tile;
    }

    public void ProcessTurn()
    {
        duration--;

        // Check if any unit is on this tile
        if (currentTile.placedUnit != null)
        {
            PlayerData owner = currentTile.placedUnit.owner;
            if (EconomyManager.Instance != null)
            {
                owner.resources += goldBonus;
                owner.researchPoints += rpBonus;
                Debug.Log($"[Geyser] {owner.playerName} collected {goldBonus} Gold and {rpBonus} RP from Geyser!");
            }
        }

        if (duration <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (HazardManager.Instance != null)
        {
            HazardManager.Instance.activeGeysers.Remove(this);
        }
        Destroy(gameObject);
    }
}
