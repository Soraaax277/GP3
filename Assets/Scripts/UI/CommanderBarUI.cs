using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CommanderBarUI : MonoBehaviour
{
    public static CommanderBarUI Instance;

    [Header("Ability Buttons")]
    public Button signalBoostBtn;
    public Button neutronBombToggle;
    public Button overclockBtn;

    private PlayerData LocalPlayer => TurnManager.Instance?.players[0]; // Assuming player 0

    private bool isNeutronActive = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (signalBoostBtn) 
        {
            if (signalBoostBtn.gameObject.GetComponent<UIButtonSounds>() == null)
                signalBoostBtn.gameObject.AddComponent<UIButtonSounds>();
            signalBoostBtn.onClick.AddListener(OnSignalBoost);
        }
        if (neutronBombToggle) 
        {
            if (neutronBombToggle.gameObject.GetComponent<UIButtonSounds>() == null)
                neutronBombToggle.gameObject.AddComponent<UIButtonSounds>();
            neutronBombToggle.onClick.AddListener(OnNeutronToggle);
        }
        if (overclockBtn) 
        {
            if (overclockBtn.gameObject.GetComponent<UIButtonSounds>() == null)
                overclockBtn.gameObject.AddComponent<UIButtonSounds>();
            overclockBtn.onClick.AddListener(OnOverclock);
        }
    }

    public void OnSignalBoost()
    {
        if (LocalPlayer.researchPoints >= 500)
        {
            LocalPlayer.researchPoints -= 500;
            // logic to double influence for 1 turn (maybe via a global multiplier in GridManager or similar)
            Debug.Log("[Commander] Signal Boost activated (500 RP)");
        }
    }

    public void OnNeutronToggle()
    {
        isNeutronActive = !isNeutronActive;
        Debug.Log($"[Commander] Neutron Bombs set to: {isNeutronActive}");
        // Update UI state (color etc)
    }

    public void OnOverclock()
    {
        if (LocalPlayer.resources >= 300)
        {
            LocalPlayer.resources -= 300;
            // 20% Revenue boost, 20 dmg to towers
            TechManager.Instance.ApplyInfrastructureUpgrade("TowerRevenue", 0.20f, true);
            foreach (var tower in FindObjectsByType<TowerNode>(FindObjectsSortMode.None))
            {
                if (tower.owner == LocalPlayer) tower.TakeDamage(20);
            }
            Debug.Log("[Commander] Overclock activated (300 Gold)");
        }
    }
}
