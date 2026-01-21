using UnityEngine;
using UnityEngine.UI;

public class UnitActionPanel : MonoBehaviour
{
    public static UnitActionPanel Instance;
    public GameObject panel;
    public Button constructButton;
    public Button buildWireButton;

    private Unit currentUnit;

    private void Awake()
    {
        Instance = this;
        panel.SetActive(false);
    }

    public void Open(Unit unit)
    {
        currentUnit = unit;
        panel.SetActive(true);

        if (constructButton != null) constructButton.gameObject.SetActive(unit is BuilderUnit);
        if (buildWireButton != null) buildWireButton.gameObject.SetActive(unit is WireSpecialist);
    }

    public void Close()
    {
        currentUnit = null;
        panel.SetActive(false);
    }

    public void OnClickConstruct()
    {
        if (currentUnit is BuilderUnit builder)
        {
            builder.ConstructAdjacentTower();
            Close();
        }
    }

    public void OnClickBuildWire()
    {
        if (currentUnit is WireSpecialist specialist)
        {
            WirePlacementManager.Instance.StartWirePlacement(specialist);
            Close();
        }
    }
}
