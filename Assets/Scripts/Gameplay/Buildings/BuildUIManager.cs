using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class BuildUIManager : MonoBehaviour
{
    public static BuildUIManager Instance;

    public GameObject buildPanel;
    private SignalNode currentBusiness;

    public TowerPlacementManager placementManager;
    public bool ignoreNextClick = false;

    public Button constructButton;
    public Button buildWireButton;
    public Button towerPlacementButton;

    private Unit currentUnit;

    void Awake()
    {
        Instance = this;
        buildPanel.SetActive(false);
    }

    void Update()
    {
        if (!buildPanel.activeSelf)
            return;

        // Ignore clicks while placing a tower
        if (placementManager != null && placementManager.IsPlacing)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            // ignore the click if this flag is true
            if (ignoreNextClick)
            {
                ignoreNextClick = false;
                return;
            }

            if (IsClickOnUIButton())
                return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // If we have a business, close if clicking elsewhere. 
                // If we have a unit, clicking away also closes.
                if (currentBusiness != null && hit.collider.gameObject != currentBusiness.businessBuilding)
                {
                    CloseBuildMenu();
                    UnitPurchaseUI.Instance.Close();
                }
                else if (currentUnit != null)
                {
                    // For units, clicking a different tile or object should close the menu
                    // unless we are in selection or placement mode
                    if (!placementManager.IsPlacing && !WirePlacementManager.Instance.IsPlacing)
                    {
                        // Minor logic: if we clicked a hex tile or something else, close
                        CloseBuildMenu();
                    }
                }
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

    public void OpenBuildMenu(SignalNode business)
    {
        currentBusiness = business;
        currentUnit = null; // Reset unit context
        buildPanel.SetActive(true);

        UpdateBuildButtons();
        UnitPurchaseUI.Instance.Open(business);
    }

    public void OpenBuildMenuForUnit(Unit unit)
    {
        currentUnit = unit;
        currentBusiness = null; // Ensure no business context is active
        buildPanel.SetActive(true);
        ignoreNextClick = true; // Prevent closing in the same frame as opening
        
        // Find nearest business to show purchase UI if needed
        // For now, builders just show the build panel
        UpdateBuildButtons();
    }

    public void UpdateBuildButtons()
    {
        bool isBuilder = currentUnit is BuilderUnit;
        bool isSpecialist = currentUnit is WireSpecialist;
        bool isSignalNode = currentBusiness != null;

        if (constructButton != null) constructButton.gameObject.SetActive(isBuilder);
        if (buildWireButton != null) buildWireButton.gameObject.SetActive(isSpecialist);
        
        if (towerPlacementButton != null)
        {
            towerPlacementButton.gameObject.SetActive(isSignalNode);
            if (isSignalNode)
            {
                bool canPlace = currentBusiness.CanPlaceTower();
                towerPlacementButton.interactable = canPlace;
                Debug.Log($"[BuildUI] Tower Button State: interactable={canPlace} ({currentBusiness.towersPlacedCount}/{currentBusiness.maxTowers})");
            }
        }

        bool isNextToInfrastructure = CheckAdjacency();
        Debug.Log($"[BuildUI] Overall State: Builder={isBuilder}, Specialist={isSpecialist}, SignalNode={isSignalNode}");
    }

    bool CheckAdjacency()
    {
        if (currentUnit == null) return true; // Default to true if not unit-driven

        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentUnit.currentTile))
        {
            if (neighbor.placedNode != null || neighbor.placedTower != null || neighbor.placedWire != null)
                return true;
        }
        return false;
    }

    public void BuildTower()
    {
        if (currentBusiness != null)
        {
            if (!currentBusiness.CanPlaceTower())
            {
                Debug.LogWarning("[BuildUIManager] Cannot build tower: Limit reached.");
                UpdateBuildButtons();
                return;
            }
            placementManager.StartTowerPlacement(currentBusiness);
        }
        else if (currentUnit is BuilderUnit builder)
        {
            placementManager.StartTowerPlacement(null, builder);
        }
    }

    public void BuildWire()
    {
        if (currentUnit is WireSpecialist specialist)
        {
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

    public void CloseBuildMenu()
    {
        buildPanel.SetActive(false);
        currentBusiness = null;
    }

    public SignalNode GetCurrentBusiness()
    {
        return currentBusiness;
    }
}
