using UnityEngine;
using TMPro;

public class GameStatusUI : MonoBehaviour
{
    public TextMeshProUGUI influenceText;
    public TextMeshProUGUI turnText;
    public TextMeshProUGUI eraText;

    private void Start()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnGameStatusChanged += UpdateUI;
        }
        UpdateUI();
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnGameStatusChanged -= UpdateUI;
        }
    }

    private void UpdateUI()
    {
        if (TurnManager.Instance == null) return;

        if (turnText != null)
            turnText.text = $"Turn: {TurnManager.Instance.currentTurnNumber} / {TurnManager.MAX_TURNS}";

        if (eraText != null)
        {
            string eraName = FormatEraName(TurnManager.Instance.currentEra);
            eraText.text = $"Era: {eraName}";
        }

        PlayerData humanPlayer = GetHumanPlayer();
        if (humanPlayer != null && influenceText != null)
        {
            influenceText.text = $"Influence: {humanPlayer.GetTotalInfluence()}";
        }
    }

    private string FormatEraName(TurnManager.GameEra era)
    {
        switch (era)
        {
            case TurnManager.GameEra.Industrial: return "Industrial";
            case TurnManager.GameEra.EarlyEighties: return "Early 80's";
            case TurnManager.GameEra.Retro: return "Retro";
            case TurnManager.GameEra.Futuristic: return "Futuristic";
            default: return "Unknown";
        }
    }

    private PlayerData GetHumanPlayer()
    {
        if (TurnManager.Instance == null) return null;
        
        var players = TurnManager.Instance.GetPlayers();
        if (players == null) return null;

        foreach (var p in players)
        {
            if (!p.isAI) return p;
        }
        
        return null;
    }
}
