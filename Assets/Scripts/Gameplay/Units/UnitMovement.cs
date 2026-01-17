using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
        if (unit == null) return;
        if (PlayerInput.Instance == null) return;

        if (unit.testingMode)
        {
            PlayerInput.Instance.SelectUnit(unit);
            return;
        }

        if (!unit.CanAct) return;
        if (!unit.CanSelect) return;
        if (unit.IsFresh) return;
        if (TurnManager.Instance.currentPlayer != unit.owner) return;

        PlayerInput.Instance.SelectUnit(unit);
    }
}
