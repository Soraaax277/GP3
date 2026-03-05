using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BlueprintButton : MonoBehaviour
{
    public TextMeshProUGUI costText;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (TowerPlacementManager.Instance == null) return;

        int cost = TowerPlacementManager.Instance.GetCurrentTowerCost();
        if (costText != null)
        {
            costText.text = $"{cost}G";
        }

        if (button != null && TurnManager.Instance != null && TurnManager.Instance.currentPlayer != null)
        {
            bool canAfford = TurnManager.Instance.currentPlayer.resources >= cost;
            button.interactable = canAfford;
            
            if (costText != null)
            {
                costText.color = canAfford ? Color.white : Color.red;
            }
        }
    }

    public void OnClickPlaceBlueprint()
    {
        Debug.Log("[BlueprintButton] Clicked");
        SignalNode business = BuildingUIManager.Instance.GetCurrentBusiness();
        if (business == null) 
        {
            Debug.LogError("[BlueprintButton] No business context found!");
            return;
        }

        if (business.CanPlaceTower())
        {
            Debug.Log("[BlueprintButton] Starting placement...");
            if (TowerPlacementManager.Instance != null) TowerPlacementManager.Instance.StartTowerPlacement(business);
            BuildingUIManager.Instance.Close();
        }
        else
        {
            Debug.Log("[Business] Tower limit reached (2 max)!");
        }
    }
}