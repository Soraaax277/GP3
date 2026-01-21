using UnityEngine;

public class EndTurnButton : MonoBehaviour
{
    public void OnClickEndTurn()
    {
        TurnManager.Instance.EndTurn();
    }
}
