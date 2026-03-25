using UnityEngine;

public class EndTurnButton : MonoBehaviour
{
    private void Awake()
    {
        if (gameObject.GetComponent<UIButtonSounds>() == null)
            gameObject.AddComponent<UIButtonSounds>();
    }

    public void OnClickEndTurn()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.currentPlayer != null)
        {
            // Only allow ending the turn if it's actually the human player's turn (non-AI)
            if (!TurnManager.Instance.currentPlayer.isAI)
            {
                TurnManager.Instance.EndTurn();
            }
            else
            {
                Debug.Log("[UI] Cannot end turn: The AI is still thinking!");
            }
        }
    }
}
