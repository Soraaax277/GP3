using UnityEngine;

public class BlueprintButton : MonoBehaviour
{
    public void OnClickPlaceBlueprint()
    {
        Debug.Log("[BlueprintButton] Clicked");
        SignalNode business = UnitPurchaseUI.Instance.GetBusiness();
        if (business == null) 
        {
            Debug.LogError("[BlueprintButton] No business context found!");
            return;
        }

        if (business.CanPlaceTower())
        {
            Debug.Log("[BlueprintButton] Starting placement...");
            BuildUIManager.Instance.BuildTower();
            BuildUIManager.Instance.CloseBuildMenu();
        }
        else
        {
            Debug.Log("[Business] Tower limit reached (2 max)!");
        }
    }
}
