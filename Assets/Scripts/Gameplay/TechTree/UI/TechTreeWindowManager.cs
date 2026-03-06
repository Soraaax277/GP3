using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections; 

public class TechTreeWindowManager : MonoBehaviour
{
    public static TechTreeWindowManager Instance;
    public static bool IsTechTreeOpen { get; private set; }

    [Header("Main UI Assignments")]
    [SerializeField] private GameObject techTreePanel;
    [SerializeField] private GameObject gameHUD;
    [SerializeField] private TextMeshProUGUI headerTitleText;

    [Header("Animation")]
    [SerializeField] private UIAnimator techTreeAnimator; 
    
    [Header("Transition Effects")]
    [Tooltip("Assign a NEW UIAnimator here that covers just the category content area.")]
    [SerializeField] private UIAnimator categoryShutter; 

    [Header("Upgrade Info Panel")]
    [SerializeField] private GameObject upgradeInfoPanel;
    [SerializeField] private TextMeshProUGUI infoTitleText;
    [SerializeField] private TextMeshProUGUI infoDescriptionText;
    [SerializeField] private Button confirmUpgradeButton;
    [SerializeField] private TextMeshProUGUI confirmButtonText;

    [Header("General Buttons")]
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    [Header("Categories")]
    [SerializeField] private GameObject panelHardware;
    [SerializeField] private GameObject panelWorkforce;
    [SerializeField] private GameObject panelServices;
    [SerializeField] private GameObject panelSabotage;

    [Header("Sidebar Buttons")]
    [SerializeField] private Button btnHardware;
    [SerializeField] private Button btnWorkforce;
    [SerializeField] private Button btnServices;
    [SerializeField] private Button btnSabotage;
    
    [Header("Click Settings")]
    [SerializeField] private GameObject[] objectsToIgnore;

    private GameObject currentActiveCategory;
    private TechNode currentSelectedNode;
    private GameObject pendingCategoryPanel; 
    private string pendingCategoryName;

    private Vector2 pointerDownPosition;
    private const float dragThreshold = 10f; 

    private void Awake()
    {
        IsTechTreeOpen = false;
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (techTreePanel != null) techTreePanel.SetActive(false);
        if (gameHUD != null) gameHUD.SetActive(true);
        if (upgradeInfoPanel != null) upgradeInfoPanel.SetActive(false);

        // Initialize Confirm Button State
        if (confirmButtonText != null) confirmButtonText.text = "PURCHASE";
        if (confirmUpgradeButton != null) confirmUpgradeButton.interactable = false;

        // Sabotage tab starts locked until the required TechNode is researched
        RefreshSabotageButton();

        // LISTENERS 
        if (btnHardware) btnHardware.onClick.AddListener(() => RequestCategorySwitch(panelHardware, "Hardware"));
        if (btnWorkforce) btnWorkforce.onClick.AddListener(() => RequestCategorySwitch(panelWorkforce, "Workforce"));
        if (btnServices) btnServices.onClick.AddListener(() => RequestCategorySwitch(panelServices, "Services"));
        if (btnSabotage) btnSabotage.onClick.AddListener(() => RequestCategorySwitch(panelSabotage, "Sabotage"));

        if (openButton) openButton.onClick.AddListener(OpenTechTree);
        if (closeButton) closeButton.onClick.AddListener(CloseTechTree);
        if (confirmUpgradeButton) confirmUpgradeButton.onClick.AddListener(ConfirmPurchase);

        // Initial Setup: Ensure everything is hidden so the first Open triggers the animation correctly
        if (panelHardware) panelHardware.SetActive(false);
        if (panelWorkforce) panelWorkforce.SetActive(false);
        if (panelServices) panelServices.SetActive(false);
        if (panelSabotage) panelSabotage.SetActive(false);
        
        currentActiveCategory = null; 
    }

    
    //  SABOTAGE BUTTON LOCK
    //  Grayed out until a TechNode with unlocksSabotageTab = true is researched.
    //  Called on: Open, ConfirmPurchase, and by TechManager when the flag flips.
    public void RefreshSabotageButton()
    {
        if (btnSabotage == null) return;

        // Ensure we check the HUMAN player's status, not just whatever the current turn is
        PlayerData humanPlayer = (GameManager.Instance != null && GameManager.Instance.players.Count > 0) 
            ? GameManager.Instance.players[0] : null;

        bool unlocked = TechManager.Instance != null && humanPlayer != null && 
                        TechManager.Instance.IsSabotageTabUnlockedFor(humanPlayer);

        btnSabotage.interactable = unlocked;

        // Gray out the button visually using the CanvasGroup if one exists,
        // otherwise the Button's own interactable state handles the tint.
        var cg = btnSabotage.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = unlocked ? 1f : 0.4f;
            cg.blocksRaycasts = unlocked;
        }

        // If the sabotage panel is currently open and we just got locked, switch away
        if (!unlocked && currentActiveCategory == panelSabotage)
        {
            RequestCategorySwitch(panelHardware, "Hardware");
        }
    }

    // TRANSITION LOGIC
    public void RequestCategorySwitch(GameObject targetPanel, string categoryName)
    {
        // Guard clause: Don't animate if we are already here
        if (currentActiveCategory == targetPanel) return;

        // Close panel immediately so it animates out INSIDE the shutter
        CloseInfoPanel(); 

        pendingCategoryPanel = targetPanel;
        pendingCategoryName = categoryName;

        if (categoryShutter != null)
        {
            categoryShutter.gameObject.SetActive(false);
            categoryShutter.onShutterClosed.RemoveAllListeners();
            categoryShutter.onShutterClosed.AddListener(ExecutePendingSwitch);
            categoryShutter.gameObject.SetActive(true);
        }
        else
        {
            ExecutePendingSwitch();
        }
    }

    private void ExecutePendingSwitch()
    {
        SwitchCategory(pendingCategoryPanel, pendingCategoryName);
    }

    private void Update()
    {
        Shader.SetGlobalFloat("_UI_UnscaledTime", Time.unscaledTime);

        if (techTreePanel != null && !techTreePanel.activeSelf)
        {
            if (upgradeInfoPanel != null && upgradeInfoPanel.activeSelf) CloseInfoPanel();
            return; 
        }

        if (IsTechTreeOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (upgradeInfoPanel != null && upgradeInfoPanel.activeSelf)
                    CloseInfoPanel();
                else
                    CloseTechTree();
            }

            if (Input.GetMouseButtonDown(0)) pointerDownPosition = Input.mousePosition;

            if (Input.GetMouseButtonUp(0))
            {
                float distance = Vector2.Distance(pointerDownPosition, Input.mousePosition);
                if (distance < dragThreshold) DetectClickOutside();
            }
        }
    }

    private void DetectClickOutside()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        bool clickedSafeZone = false;
        foreach (RaycastResult result in results)
        {
            GameObject hitObj = result.gameObject;
            if (hitObj.GetComponentInParent<TechButton>() != null) { clickedSafeZone = true; break; }
            if (upgradeInfoPanel != null && upgradeInfoPanel.activeSelf)
            {
                if (hitObj.transform.IsChildOf(upgradeInfoPanel.transform) || hitObj == upgradeInfoPanel) { clickedSafeZone = true; break; }
            }
            if (objectsToIgnore != null)
            {
                foreach (var ignoredObj in objectsToIgnore)
                {
                    if (ignoredObj != null && (hitObj == ignoredObj || hitObj.transform.IsChildOf(ignoredObj.transform)))
                    {
                        clickedSafeZone = true;
                        break;
                    }
                }
                if (clickedSafeZone) break;
            }
        }

        if (!clickedSafeZone)
        {
            // Release the locked TechNodeButton (reverts it to normal scale and original rotation)
            UIAnimator.DeactivateCurrentTechButton();
            CloseInfoPanel();
        }
    }

    public void SelectTechNode(TechNode node)
    {
        currentSelectedNode = node;

        if (upgradeInfoPanel != null)
        {
            // If the panel is already open, refresh its content in-place
            if (upgradeInfoPanel.activeSelf)
            {
                UpdateInfoPanelUI();
            }
            else
            {
                upgradeInfoPanel.SetActive(true);
                UpdateInfoPanelUI();
            }
            
            if (confirmButtonText != null && confirmButtonText.text == "PURCHASE")
            {
                TriggerButtonAnim(confirmUpgradeButton);
            }
        }
    }

    public void CloseInfoPanel()
    {
        if (upgradeInfoPanel != null && upgradeInfoPanel.activeSelf) 
        {
            var anim = upgradeInfoPanel.GetComponent<UIAnimator>();
            if (anim != null)
            {
                anim.AnimateExit(() => 
                {
                    upgradeInfoPanel.SetActive(false);
                    currentSelectedNode = null;
                });
            }
            else
            {
                upgradeInfoPanel.SetActive(false);
                currentSelectedNode = null;
            }
        }
        else
        {
            currentSelectedNode = null;
        }

        // When the info panel closes (e.g. category switch), also release the locked TechNodeButton
        UIAnimator.DeactivateCurrentTechButton();

        if (confirmUpgradeButton != null) confirmUpgradeButton.interactable = false;
    }

    private void UpdateInfoPanelUI()
    {
        if (currentSelectedNode == null) return;
        if (infoTitleText != null) infoTitleText.text = currentSelectedNode.techName;
        if (infoDescriptionText != null) 
        {
            infoDescriptionText.text = $"{currentSelectedNode.description}\n\n" +
                                       $"<color=yellow>Cost: {currentSelectedNode.researchCost} RP</color>";
        }

        // Check Player Resources (Human Player at index 0)
        PlayerData humanPlayer = (GameManager.Instance != null && GameManager.Instance.players.Count > 0) 
            ? GameManager.Instance.players[0] : null;

        bool isUnlocked = humanPlayer != null && currentSelectedNode.IsUnlockedBy(humanPlayer);
        bool canUnlock = humanPlayer != null && currentSelectedNode.CanUnlockFor(humanPlayer);
        
        int playerRP = humanPlayer != null ? humanPlayer.researchPoints : 0;
        bool canAfford = playerRP >= currentSelectedNode.researchCost;

        if (confirmUpgradeButton != null && confirmButtonText != null)
        {
            if (isUnlocked)
            {
                confirmUpgradeButton.interactable = false; 
                confirmButtonText.text = "UNLOCKED";
            }
            else if (canUnlock)
            {
                if (canAfford)
                {
                    confirmUpgradeButton.interactable = true; 
                    confirmButtonText.text = "PURCHASE";
                }
                else
                {
                    confirmUpgradeButton.interactable = false; 
                    confirmButtonText.text = "TOO EXPENSIVE";
                }
            }
            else
            {
                confirmUpgradeButton.interactable = false; 
                confirmButtonText.text = "LOCKED";
            }
        }
    }

    public void ConfirmPurchase()
    {
        if (currentSelectedNode != null)
        {
            // Uses the TechManager bridge instead of direct unlock
            // Research via the Manager (handles cost & applying effects)
            if (TechManager.Instance != null)
            {
                TechManager.Instance.ResearchTech(currentSelectedNode);
            }
            else
            {
                // Fallback just in case TechManager is missing (for safety)
                if (currentSelectedNode.CanUnlock()) currentSelectedNode.UnlockTech();
            }

            // Refresh UI (Visuals only)
            UpdateInfoPanelUI();
            RefreshAllTechButtons();
            RefreshSabotageButton();
            UpdateAllLines(); // For the connection lines
        }
    }

    private void RefreshAllTechButtons()
    {
        TechButton[] buttons = FindObjectsByType<TechButton>(FindObjectsInactive.Include, FindObjectsSortMode.None); 
        foreach(var btn in buttons) if(btn != null) btn.UpdateNodeVisuals();
    }

    private void UpdateAllLines()
    {
        TechLine[] lines = FindObjectsByType<TechLine>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var line in lines) if (line != null) line.UpdateVisuals(true);
    }

    public void SwitchCategory(GameObject targetPanel, string categoryName)
    {
        currentActiveCategory = targetPanel;
        if (headerTitleText != null) headerTitleText.text = $"TECHNOLOGY: {categoryName}";
        
        if (panelHardware) panelHardware.SetActive(false);
        if (panelWorkforce) panelWorkforce.SetActive(false);
        if (panelServices) panelServices.SetActive(false);
        if (panelSabotage) panelSabotage.SetActive(false);

        if (currentActiveCategory != null) 
        {
            currentActiveCategory.SetActive(true);
            ResetCategoryScroll(currentActiveCategory);
        }
    }

    public void ResetCategoryScroll(GameObject categoryObj)
    {
        ScrollRect scroll = categoryObj.GetComponentInParent<ScrollRect>();
        if (scroll == null) scroll = categoryObj.GetComponent<ScrollRect>();

        if (scroll != null && scroll.content != null)
        {
            scroll.velocity = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.horizontalNormalizedPosition = 0; 
            Vector2 pos = scroll.content.anchoredPosition;
            pos.x = 0;
            scroll.content.anchoredPosition = pos;
        }
    }

    public void OpenTechTree()
    {
        IsTechTreeOpen = true;
        if (gameHUD != null) gameHUD.SetActive(false);
        StartCoroutine(OpenSequence());
    }

    private IEnumerator OpenSequence()
    {
        if (techTreePanel != null) techTreePanel.SetActive(true);
        
        // Force the system to think no category is selected at the start, even if panelHardware is the default active one in the hierarchy.
        // This bypasses the check in RequestCategorySwitch, forcing the animation to play
        // for the default category (Hardware) when the window opens.
        currentActiveCategory = null; 

        // Trigger the switch to default (Hardware) WITH animation
        RequestCategorySwitch(panelHardware, "Hardware");

        RefreshAllTechButtons();
        RefreshSabotageButton();

        TriggerButtonAnim(btnHardware);
        TriggerButtonAnim(btnWorkforce);
        TriggerButtonAnim(btnServices);
        TriggerButtonAnim(btnSabotage);
        TriggerButtonAnim(closeButton);

        Time.timeScale = 0f;
        yield break;
    }

    private void TriggerButtonAnim(Button btn)
    {
        if (btn != null)
        {
            btn.gameObject.SetActive(true);
            var anim = btn.GetComponent<UIAnimator>();
            if (anim != null) anim.PlayEntryAnimation();
        }
    }

    public void CloseTechTree()
    {
        System.Action finishClosing = () => 
        {
            if (techTreePanel != null) techTreePanel.SetActive(false);
            if (gameHUD != null) gameHUD.SetActive(true);
            CloseInfoPanel();
            Time.timeScale = 1f;
            IsTechTreeOpen = false;
        };

        if (techTreeAnimator != null && techTreePanel.activeSelf)
            techTreeAnimator.AnimateExit(finishClosing);
        else
            finishClosing.Invoke();
    }
}