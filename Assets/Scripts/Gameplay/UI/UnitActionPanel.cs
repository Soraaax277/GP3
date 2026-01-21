using UnityEngine;
using UnityEngine.UI;

public class UnitActionPanel : MonoBehaviour
{
    public static UnitActionPanel Instance;
    public GameObject panel;
    public Button constructButton;
    public Button buildWireButton;
    public Button repairButton;

    private Unit currentUnit;

    private void Awake()
    {
        Instance = this;
        panel.SetActive(false);
        
        if (constructButton != null)
            constructButton.gameObject.SetActive(false);
        if (buildWireButton != null)
            buildWireButton.gameObject.SetActive(false);
        if (repairButton != null)
            repairButton.gameObject.SetActive(false);
    }

    public void Open(Unit unit)
    {
        currentUnit = unit;
        panel.SetActive(true);

        if (constructButton != null) 
        {
            bool isBuilder = unit is BuilderUnit;
            constructButton.gameObject.SetActive(isBuilder);
            if (isBuilder)
                constructButton.interactable = unit.CanAct;
        }
        
        if (buildWireButton != null) 
        {
            bool isWireSpecialist = unit is WireSpecialist;
            buildWireButton.gameObject.SetActive(isWireSpecialist);
            if (isWireSpecialist)
                buildWireButton.interactable = unit.CanAct;
        }
        
        if (repairButton != null)
        {
            bool isTechnician = unit is Technician;
            repairButton.gameObject.SetActive(isTechnician);
            if (isTechnician)
                repairButton.interactable = unit.CanAct;
        }
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

    public void OnClickRepair()
    {
        if (currentUnit is Technician technician)
        {
            technician.RepairAdjacentStructure();
            Close();
        }
    }
}
