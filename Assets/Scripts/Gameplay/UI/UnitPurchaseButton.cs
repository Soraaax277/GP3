using UnityEngine;

public class UnitPurchaseButton : MonoBehaviour
{
    public GameObject unitPrefab;

    public void OnClickPurchase()
    {
        SignalNode business = UnitPurchaseUI.Instance.GetBusiness();
        if (business == null) return;

        UnitSpawner.Instance.SpawnUnit(unitPrefab, business);
    }
}
