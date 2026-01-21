using UnityEngine;

public class UnitPurchaseUI : MonoBehaviour
{
    public static UnitPurchaseUI Instance;

    private SignalNode currentBusiness;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    public void Open(SignalNode business)
    {
        Debug.Log($"[UnitPurchaseUI] Opening for business: {business.name}");
        currentBusiness = business;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        currentBusiness = null;
        gameObject.SetActive(false);
    }

    public SignalNode GetBusiness()
    {
        return currentBusiness;
    }
}
