using UnityEngine;

public class BuildTowerButton : MonoBehaviour
{
    public TowerPlacementManager placementManager;

    public void Show(bool show)
    {
        gameObject.SetActive(show);
    }

    public void OnClickBuildTower()
    {
        SignalNode business = BuildingUIManager.Instance.GetCurrentBusiness();
        placementManager.StartTowerPlacement(business);
    }
}