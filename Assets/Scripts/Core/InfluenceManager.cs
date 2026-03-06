using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class InfluenceManager : MonoBehaviour
{
    public static InfluenceManager Instance;

    // Stores the "Real-time" score for the current turn
    private Dictionary<PlayerData, int> playerTotalInfluence = new Dictionary<PlayerData, int>();

    private void Awake()
    {
        Instance = this;
    }

    // Performs a full audit of the map to calculate scores.
    public void RecalculateGlobalInfluence(List<PlayerData> allPlayers)
    {
        // Reset scores
        playerTotalInfluence.Clear();
        foreach (var p in allPlayers)
        {
            playerTotalInfluence[p] = 0;
        }

        // We iterate through every tile to see who owns the influence there.
        if (GridManager.Instance != null)
        {
            foreach (var tile in GridManager.Instance.GetAllTiles()) // Assuming GridManager has this method
            {
                foreach (var player in allPlayers)
                {
                    // Add the influence this player has on this specific tile
                    int tileInf = tile.GetInfluence(player);
                    if (tileInf > 0)
                    {
                        // ERA INFLUENCE PENALTY (System 1)
                        // If the player's Hardware Era lags behind the World Era, their influence
                        // generation is penalised. Gap of 0 = no penalty (multiplier 1.0).
                        // Each era gap reduces influence by 25%, floored at 25% of base.
                        // e.g. World: Retro, Player HW: Industrial → gap 2 → multiplier 0.5
                        float eraMultiplier = 1.0f;
                        if (TurnManager.Instance != null)
                            eraMultiplier = TurnManager.Instance.GetEraInfluenceMultiplier(player);

                        float hyperMultiplier = tile.isHyperinflated ? 2.0f : 1.0f;
                        playerTotalInfluence[player] += Mathf.RoundToInt(Mathf.Max(0, tileInf - tile.influenceSuppression) * eraMultiplier * hyperMultiplier);
                    }
                }
            }
        }
        else
        {
            Debug.LogError("InfluenceManager: GridManager instance not found! Cannot calculate scores.");
        }

        // Reflow visuals for all tiles now that influence has changed
        if (GridManager.Instance != null)
        {
            foreach (var tile in GridManager.Instance.GetAllTiles())
                tile.UpdateAppearance();
        }

        // Debug Report 
        Debug.Log("--- [Influence Report] ---");
        foreach (var kvp in playerTotalInfluence)
        {
            // Show era multiplier alongside the score so it's visible in the log
            float era = TurnManager.Instance != null
                ? TurnManager.Instance.GetEraInfluenceMultiplier(kvp.Key)
                : 1.0f;
            Debug.Log($"Player: {kvp.Key.playerName} | Total Influence: {kvp.Value} | Era Multiplier: {era:F2}");
        }
        Debug.Log("--------------------------");

        // Update territory borders visually
        if (InfluenceBorderRenderer.Instance != null)
        {
            InfluenceBorderRenderer.Instance.UpdateBorders();
        }
    }

    public int GetTotalInfluence(PlayerData player)
    {
        if (playerTotalInfluence.ContainsKey(player))
            return playerTotalInfluence[player];
        return 0;
    }

    public PlayerData GetWinner()
    {
        // Sorts by highest value and returns the player
        if (playerTotalInfluence.Count == 0) return null;
        return playerTotalInfluence.OrderByDescending(x => x.Value).First().Key;
    }
}