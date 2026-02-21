using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class BuildUIManager : MonoBehaviour
{
    public static BuildUIManager Instance;

    [Header("UI References")]
    public GameObject buildPanel; 
    public Camera mainCamera;      

    [Header("World Space Settings")]
    public Vector3 menuOffset = new Vector3(2f, 3f, 0f); 
    private Transform followTarget; 

    [Header("Data References")]
    private SignalNode currentBusiness;
    private Unit currentUnit;

    public TowerPlacementManager placementManager;
    public bool ignoreNextClick = false;

    [Header("Buttons")]
    public Button constructButton;
    public Button buildWireButton;
    public Button towerPlacementButton;
    public GameObject towerPlacementDisabledHelper;

    void Awake()
    {
        Instance = this;
        if (buildPanel != null) buildPanel.SetActive(false);
        
        if (mainCamera == null) mainCamera = Camera.main;
    }

    void Update()
    {
        // World Space Follow Logic
        if (buildPanel.activeSelf && followTarget != null)
        {
            if (mainCamera != null)
            {
                Vector3 cameraRelativeOffset = mainCamera.transform.rotation * menuOffset;
                buildPanel.transform.position = followTarget.position + cameraRelativeOffset;
                buildPanel.transform.rotation = mainCamera.transform.rotation;
            }
            else
            {
                buildPanel.transform.position = followTarget.position + menuOffset;
            }
        }

        // Existing Input Logic
        if (!buildPanel.activeSelf)
            return;

        if (placementManager != null && placementManager.IsPlacing)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            if (ignoreNextClick)
            {
                ignoreNextClick = false;
                return;
            }

            if (IsClickOnUIButton())
                return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // Logic: If we click something OTHER than the current target, close the menu
                if (currentBusiness != null && hit.collider.gameObject != currentBusiness.businessBuilding)
                {
                    CloseBuildMenu();
                    if (UnitPurchaseUI.Instance != null) UnitPurchaseUI.Instance.Close();
                }
                else if (currentUnit != null)
                {
                    Unit clickedUnit = hit.collider.GetComponent<Unit>();
                    if (clickedUnit != currentUnit)
                    {
                        if (!placementManager.IsPlacing && (WirePlacementManager.Instance == null || !WirePlacementManager.Instance.IsPlacing))
                        {
                            CloseBuildMenu();
                        }
                    }
                }
            }
            else
            {
                CloseBuildMenu();
            }
        }
    }

    bool IsClickOnUIButton()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            if (result.gameObject.GetComponent<Button>() != null)
                return true;
        }

        return false;
    }

    // Modified Open Functions

    public void OpenBuildMenu(SignalNode business)
    {
        // OWNERSHIP CHECK
        // If this business belongs to an AI, do NOT open the menu.
        if (business.owner != null && business.owner.isAI)
        {
            Debug.Log("Cannot access Enemy HQ.");
            return;
        }

        currentBusiness = business;
        currentUnit = null;

        if (business.businessBuilding != null)
            followTarget = business.businessBuilding.transform;
        else
            followTarget = null;

        buildPanel.SetActive(true);

        // CAMERA LOCK INTEGRATION
        if (followTarget != null && CameraController.Instance != null)
        {
            CameraController.Instance.SetBuildModeLock(true, followTarget.position);
        }

        UpdateBuildButtons();
        if (UnitPurchaseUI.Instance != null) UnitPurchaseUI.Instance.Open(business);
    }

    public void OpenBuildMenuForUnit(Unit unit)
    {
        // OWNERSHIP CHECK
        if (unit.owner != null && unit.owner.isAI)
        {
            return;
        }

        currentUnit = unit;
        currentBusiness = null;

        followTarget = unit.transform;

        buildPanel.SetActive(true);
        ignoreNextClick = true;

        if (CameraController.Instance != null)
        {
            CameraController.Instance.SetBuildModeLock(true, followTarget.position);
        }
        
        UpdateBuildButtons();
    }
    public void CloseBuildMenu()
    {
        if (buildPanel == null || !buildPanel.activeSelf) return;

        // Get the animator to play the exit transition
        UIAnimator animator = buildPanel.GetComponent<UIAnimator>();

        if (animator != null)
        {
            animator.AnimateExit(() => 
            {
                // This code runs ONLY after the animation finishes
                buildPanel.SetActive(false);
                followTarget = null;
            });
        }
        else
        {
            // Fallback if no animator is found
            buildPanel.SetActive(false);
            followTarget = null;
        }

        currentBusiness = null;
        
        // Note: CameraController logic remains handled by the panel's active state
    }
    public void UpdateBuildButtons()
    {
        bool isBuilder = currentUnit is BuilderUnit;
        bool isSpecialist = currentUnit is WireSpecialist;
        bool isSignalNode = currentBusiness != null;

        bool towersUnlocked = false;
        if (TechManager.Instance != null)
        {
            towersUnlocked = TechManager.Instance.IsFeatureUnlocked("TelecomTowers");
        }

        if (constructButton != null) 
        {
            constructButton.gameObject.SetActive(isBuilder);
            if (isBuilder) 
                constructButton.interactable = currentUnit.CanAct && towersUnlocked;
        }

        if (buildWireButton != null) 
        {
            buildWireButton.gameObject.SetActive(isSpecialist);
            if (isSpecialist) buildWireButton.interactable = currentUnit.CanAct;
        }
        
        if (towerPlacementButton != null)
        {
            towerPlacementButton.gameObject.SetActive(isSignalNode);
            
            if (towerPlacementDisabledHelper != null) 
                towerPlacementDisabledHelper.SetActive(false);

            if (isSignalNode)
            {
                bool canPlace = currentBusiness.towersPlacedCount < currentBusiness.CurrentMaxTowers;
                bool isInteractable = towersUnlocked && canPlace;

                towerPlacementButton.interactable = isInteractable;

                if (towerPlacementDisabledHelper != null)
                {
                    towerPlacementDisabledHelper.SetActive(!canPlace && towersUnlocked);
                }
                
                if (!isInteractable && EventSystem.current.currentSelectedGameObject == towerPlacementButton.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
            }
        }

        CheckAdjacency(); 
    }

    bool CheckAdjacency()
    {
        if (currentUnit == null) return true;

        if (GridManager.Instance != null)
        {
            foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentUnit.currentTile))
            {
                if (neighbor.placedNode != null || neighbor.placedTower != null || neighbor.placedWire != null)
                    return true;
            }
        }
        return false;
    }

    public void BuildTower()
    {
        if (TechManager.Instance != null && !TechManager.Instance.IsFeatureUnlocked("TelecomTowers"))
        {
            Debug.Log("Technology 'Telecom Towers' has not been researched yet!");
            return;
        }

        if (towerPlacementButton != null && !towerPlacementButton.interactable) return;
        
        if (currentBusiness != null)
        {
            if (!currentBusiness.CanPlaceTower()) return;
            placementManager.StartTowerPlacement(currentBusiness);
        }
        else if (currentUnit is BuilderUnit builder)
        {
            placementManager.StartTowerPlacement(null, builder);
        }
    }

    public void BuildWire()
    {
        if (currentUnit is WireSpecialist specialist && specialist.CanAct)
        {
            if (WirePlacementManager.Instance != null)
                WirePlacementManager.Instance.StartWirePlacement(specialist);
        }
    }

    public void OnConstructTower()
    {
        if (currentUnit is BuilderUnit builder)
        {
            builder.ConstructAdjacentTower();
        }
    }

    public SignalNode GetCurrentBusiness()
    {
        return currentBusiness;
    }
}