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
        if (gameObject.GetComponent<UIButtonSounds>() == null)
            gameObject.AddComponent<UIButtonSounds>();
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

            // SERVICE CENTER RESTRICTIONS
            if (UnitPurchaseUI.Instance != null && UnitPurchaseUI.Instance.GetServiceCenter() != null)
            {
                bool isWorkforce = unitPrefab.name.Contains("Foremen") || unitPrefab.name.Contains("Maintenance") || unitPrefab.name.Contains("ITPersonnel");
                if (!isWorkforce)
                {
                    gameObject.SetActive(false);
                    return;
                }
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

        PlayerData owner = UnitPurchaseUI.Instance.GetCurrentOwner();
        HexTile spawnTile = UnitPurchaseUI.Instance.GetSpawnTile();

        if (owner == null || spawnTile == null) return;

        Unit spawned = UnitSpawner.Instance.SpawnUnit(unitPrefab, spawnTile, owner);
        if (spawned != null)
        {
            UpdateUI(); // Refresh after purchase
        }
    }
}
