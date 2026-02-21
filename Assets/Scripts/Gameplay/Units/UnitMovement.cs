using UnityEngine;
using UnityEngine.EventSystems;

public class UnitMovement : MonoBehaviour
{
    private Unit unit;

    private void Awake()
    {
        unit = GetComponent<Unit>();
    }

    private void OnMouseDown()
    {
        if (EventSystem.current.IsPointerOverGameObject()) return;
        if (unit == null || PlayerInput.Instance == null) return;

        if (unit.testingMode)
        {
            PlayerInput.Instance.SelectUnit(unit);
            return;
        }

        if (!unit.CanAct || !unit.CanSelect || unit.IsFresh) return;
        if (TurnManager.Instance.currentPlayer != unit.owner) return;

        PlayerInput.Instance.SelectUnit(unit);
    }
}