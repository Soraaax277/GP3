using UnityEngine;

public class UnitPurchaseButton : MonoBehaviour
{
    public GameObject unitPrefab;

    public void OnClickPurchase()
    {
        if (unitPrefab == null)
        {
            Debug.LogError("Unit Prefab is not assigned to this button!");
            return;
        }

        SignalNode business = UnitPurchaseUI.Instance.GetBusiness();
        if (business == null) return;

        UnitSpawner.Instance.SpawnUnit(unitPrefab, business);
    }
}
