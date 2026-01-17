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
                if (hit.collider.gameObject != currentBusiness.businessBuilding)
                {
                    CloseBuildMenu();
                    UnitPurchaseUI.Instance.Close();
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
        buildPanel.SetActive(true);

        UnitPurchaseUI.Instance.Open(business);
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
