using UnityEngine;
using UnityEngine.UI;

public class UnitActionPanel : MonoBehaviour
{
    public static UnitActionPanel Instance;
    
    [Header("UI References")]
    public GameObject panel;
    public Button constructButton;
    public Button buildWireButton;
    public Button repairButton;
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
            if (isBuilder) constructButton.interactable = unit.CanAct;
        }
        
        if (buildWireButton != null) 
        {
            bool isWireSpecialist = unit is WireSpecialist;
            buildWireButton.gameObject.SetActive(isWireSpecialist);
            if (isWireSpecialist) buildWireButton.interactable = unit.CanAct;
        }
        
        if (repairButton != null)
        {
            bool isTechnician = unit is Technician;
            repairButton.gameObject.SetActive(isTechnician);
            if (isTechnician) repairButton.interactable = unit.CanAct;
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
}