using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    public static PlayerInput Instance;
    private Unit selectedUnit;

    private void Awake() => Instance = this;

    private void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Unit unit = hit.collider.GetComponentInParent<Unit>();

                if (unit != null)
                {
                    SelectUnit(unit);
                }
                else
                {
                    DeselectUnit();
                }
            }
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (selectedUnit == null) return;

            Debug.Log("Right click move attempt: " + selectedUnit.name);

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Debug.Log("Raycast hit: " + hit.collider.name);
                HexTile tile = hit.collider.GetComponent<HexTile>();
                if (tile != null)
                {
                    int allowedRange = (selectedUnit is BuilderUnit b) ? b.moveRange : 1;
                    selectedUnit.MoveTo(tile, allowedRange);
                }
            }
        }
    }

    public void SelectUnit(Unit unit)
    {
        Debug.Log("Selected unit: " + unit.name);

        if (selectedUnit != null)
            selectedUnit.SetSelected(false);

        selectedUnit = unit;
        selectedUnit.SetSelected(true);
    }

    public void DeselectUnit()
    {
        if (selectedUnit == null) return;

        selectedUnit.SetSelected(false);
        selectedUnit = null;
    }
}
