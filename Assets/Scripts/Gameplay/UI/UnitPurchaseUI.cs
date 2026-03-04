using UnityEngine;

public class UnitPurchaseUI : MonoBehaviour
{
    public static UnitPurchaseUI Instance;

    private SignalNode currentBusiness;
    private ServiceCenter currentServiceCenter;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    public void Open(SignalNode business)
    {
        Debug.Log($"[UnitPurchaseUI] Opening for business: {business.name}");
        currentBusiness = business;
        currentServiceCenter = null;
        gameObject.SetActive(true);
        RefreshButtons();
    }

    public void OpenForServiceCenter(ServiceCenter serviceCenter)
    {
        Debug.Log($"[UnitPurchaseUI] Opening for Service Center: {serviceCenter.name}");
        currentServiceCenter = serviceCenter;
        currentBusiness = null;
        gameObject.SetActive(true);
        RefreshButtons();
    }

    public void Close()
    {
        currentBusiness = null;
        currentServiceCenter = null;
        gameObject.SetActive(false);
    }

    private void RefreshButtons()
    {
        foreach (var btn in GetComponentsInChildren<UnitPurchaseButton>(true))
        {
            btn.UpdateUI();
        }
    }

    public SignalNode GetBusiness() => currentBusiness;
    public ServiceCenter GetServiceCenter() => currentServiceCenter;
    public PlayerData GetCurrentOwner() 
    {
        if (currentBusiness != null) return currentBusiness.owner;
        if (currentServiceCenter != null) return currentServiceCenter.owner;
        return null;
    }
    public HexTile GetSpawnTile()
    {
        if (currentBusiness != null) return currentBusiness.tile;
        if (currentServiceCenter != null) return currentServiceCenter.ParentTile;
        return null;
    }
}
