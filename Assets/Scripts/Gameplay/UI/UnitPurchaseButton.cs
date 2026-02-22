using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnitPurchaseButton : MonoBehaviour
{
    public GameObject unitPrefab;
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
        if (unitPrefab == null || UnitSpawner.Instance == null) return;

        int cost = UnitSpawner.Instance.GetRecruitmentCost(unitPrefab);
        if (costText != null)
        {
            costText.text = $"{cost}G";
        }

        if (button != null && TurnManager.Instance != null && TurnManager.Instance.currentPlayer != null)
        {
            // TECH UNLOCK CHECK
            bool isRestricted = unitPrefab.name.Contains("SalesMarketer"); // Add more names here if needed
            bool isUnlocked = !isRestricted || (TechManager.Instance != null && TechManager.Instance.unlockedUnitNames.Contains(unitPrefab.name));

            // Also check for "Freelance Brand/Service Promoter" specifically as per README
            if (unitPrefab.name.Contains("SalesMarketer") && TechManager.Instance != null)
            {
                isUnlocked = TechManager.Instance.unlockedUnitNames.Contains("SalesMarketer");
            }

            gameObject.SetActive(isUnlocked);
            if (!isUnlocked) return;

            bool canAfford = TurnManager.Instance.currentPlayer.resources >= cost;
            button.interactable = canAfford;
            
            if (costText != null)
            {
                costText.color = canAfford ? Color.white : Color.red;
            }
        }
    }

    public void OnClickPurchase()
    {
        if (unitPrefab == null)
        {
            Debug.LogError("Unit Prefab is not assigned to this button!");
            return;
        }

        SignalNode business = UnitPurchaseUI.Instance.GetBusiness();
        if (business == null) return;

        Unit spawned = UnitSpawner.Instance.SpawnUnit(unitPrefab, business);
        if (spawned != null)
        {
            UpdateUI(); // Refresh after purchase
        }
    }
}
