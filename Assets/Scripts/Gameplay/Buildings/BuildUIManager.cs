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
    public GameObject towerPlacementDisabledHelper;

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

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (currentBusiness != null && hit.collider.gameObject != currentBusiness.businessBuilding)
                {
                    CloseBuildMenu();
                    UnitPurchaseUI.Instance.Close();
                }
                else if (currentUnit != null)
                {
                    if (!placementManager.IsPlacing && !WirePlacementManager.Instance.IsPlacing)
                    {
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
        currentUnit = null;
        buildPanel.SetActive(true);

        UpdateBuildButtons();
        UnitPurchaseUI.Instance.Open(business);
    }

    public void OpenBuildMenuForUnit(Unit unit)
    {
        currentUnit = unit;
        currentBusiness = null;
        buildPanel.SetActive(true);
        ignoreNextClick = true;
        
        UpdateBuildButtons();
    }

    public void UpdateBuildButtons()
    {
        bool isBuilder = currentUnit is BuilderUnit;
        bool isSpecialist = currentUnit is WireSpecialist;
        bool isSignalNode = currentBusiness != null;

        if (constructButton != null) 
        {
            constructButton.gameObject.SetActive(isBuilder);
            if (isBuilder) constructButton.interactable = currentUnit.CanAct;
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
                bool canPlace = currentBusiness.towersPlacedCount < currentBusiness.maxTowers;
                
                if (towerPlacementDisabledHelper != null)
                {
                    towerPlacementButton.gameObject.SetActive(canPlace);
                    towerPlacementDisabledHelper.SetActive(!canPlace);
                }
                else
                {
                    towerPlacementButton.interactable = canPlace;
                    if (!canPlace && EventSystem.current.currentSelectedGameObject == towerPlacementButton.gameObject)
                        EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }

        bool isNextToInfrastructure = CheckAdjacency();
    }

    bool CheckAdjacency()
    {
        if (currentUnit == null) return true;

        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentUnit.currentTile))
        {
            if (neighbor.placedNode != null || neighbor.placedTower != null || neighbor.placedWire != null)
                return true;
        }
        return false;
    }

    public void BuildTower()
    {
        if (towerPlacementButton != null && !towerPlacementButton.interactable) return;
        
        if (currentBusiness != null)
        {
            if (!currentBusiness.CanPlaceTower())
            {
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
        if (currentUnit is WireSpecialist specialist && specialist.CanAct)
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
