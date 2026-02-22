using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnitActionPanel : MonoBehaviour
{
    public static UnitActionPanel Instance;
    
    [Header("UI References")]
    public GameObject panel;
    public Button constructButton;
    public Button buildWireButton;
    public Button repairButton;
    public Button denyButton;
    
    [Header("Cost UI References")]
    public TextMeshProUGUI constructCostText;
    public TextMeshProUGUI buildWireCostText;
    public TextMeshProUGUI repairCostText;
    
    public Camera mainCamera; 

    [Header("World Space Settings")]
    public Vector3 menuOffset = new Vector3(3.75f, 0.25f, -3.5f); 
    private Transform followTarget; 

    private Unit currentUnit;

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
        if (mainCamera == null) mainCamera = Camera.main;
        
        // Hide buttons initially
        if (constructButton) constructButton.gameObject.SetActive(false);
        if (buildWireButton) buildWireButton.gameObject.SetActive(false);
        if (repairButton) repairButton.gameObject.SetActive(false);
        if (denyButton) denyButton.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (panel.activeSelf && followTarget != null)
        {
            if (mainCamera != null)
            {
                Vector3 cameraRelativeOffset = mainCamera.transform.rotation * menuOffset;
                panel.transform.position = followTarget.position + cameraRelativeOffset;
                panel.transform.rotation = mainCamera.transform.rotation;
            }
            else
            {
                panel.transform.position = followTarget.position + menuOffset;
            }
        }
    }

    public void Open(Unit unit)
    {
        if (unit == null) return;

        currentUnit = unit;
        followTarget = unit.transform; 
        
        panel.SetActive(true);

        // LOCK CAMERA
        if (CameraController.Instance != null)
        {
            CameraController.Instance.SetBuildModeLock(true, followTarget.position);
        }

        // Setup Buttons
        if (constructButton != null) 
        {
            bool isBuilder = unit is BuilderUnit;
            constructButton.gameObject.SetActive(isBuilder);
            if (isBuilder) 
            {
                int cost = ((BuilderUnit)unit).GetBuildingCost();
                if (constructCostText != null) constructCostText.text = $"{cost}G";
                
                bool canAfford = unit.owner.resources >= cost;
                constructButton.interactable = unit.CanAct && canAfford;
                if (constructCostText != null) constructCostText.color = canAfford ? Color.white : Color.red;
            }
        }
        
        if (buildWireButton != null) 
        {
            bool isWireSpecialist = unit is WireSpecialist;
            buildWireButton.gameObject.SetActive(isWireSpecialist);
            if (isWireSpecialist) 
            {
                int cost = WirePlacementManager.Instance != null ? WirePlacementManager.Instance.GetCurrentWireCost() : 0;
                if (buildWireCostText != null) buildWireCostText.text = $"{cost}G";

                bool canAfford = unit.owner.resources >= cost;
                buildWireButton.interactable = unit.CanAct && canAfford;
                if (buildWireCostText != null) buildWireCostText.color = canAfford ? Color.white : Color.red;
            }
        }
        
        if (repairButton != null)
        {
            bool isTechnician = unit is Technician;
            bool isBuilder = unit is BuilderUnit;
            bool canRepair = isTechnician || (isBuilder && ((BuilderUnit)unit).canRepairInfrastructure);
            
            repairButton.gameObject.SetActive(canRepair);
            if (canRepair) 
            {
                int cost = 0;
                if (isTechnician) cost = ((Technician)unit).GetRepairCost();
                else if (isBuilder) cost = ((BuilderUnit)unit).GetRepairCost();

                if (repairCostText != null) repairCostText.text = $"{cost}G";

                bool canAfford = unit.owner.resources >= cost;
                repairButton.interactable = unit.CanAct && canAfford;
                if (repairCostText != null) repairCostText.color = canAfford ? Color.white : Color.red;
            }
        }

        if (denyButton != null)
        {
            bool isMarketer = unit is SalesMarketer;
            denyButton.gameObject.SetActive(isMarketer);
            if (isMarketer) denyButton.interactable = unit.CanAct;
        }
    }

    public void Close()
    {
        if (panel == null || !panel.activeSelf) return;

        UIAnimator animator = panel.GetComponent<UIAnimator>();

        if (animator != null)
        {
            animator.AnimateExit(() => 
            {
                // Disable the panel after the tween finishes
                panel.SetActive(false);
                currentUnit = null;
                followTarget = null;
            });
        }
        else
        {
            panel.SetActive(false);
            currentUnit = null;
            followTarget = null;
        }
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

    public void OnClickDeny()
    {
        if (currentUnit is SalesMarketer marketer)
        {
            marketer.PerformDeny();
            Close();
        }
    }
}