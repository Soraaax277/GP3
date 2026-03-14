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

    [Header("Fog Shader Images")]
    [Tooltip("Assign all Image components using a fog shader (decorative fog). " +
             "These are driven by ManualTime every frame.")]
    [SerializeField] private List<Image> fogImages;

    [Header("Category Fog Managers")]
    [Tooltip("TechCategoryFogManager on the Hardware panel.")]
    [SerializeField] private TechCategoryFogManager hardwareFogManager;
    [Tooltip("TechCategoryFogManager on the Workforce panel.")]
    [SerializeField] private TechCategoryFogManager workforceFogManager;
    [Tooltip("TechCategoryFogManager on the Services panel.")]
    [SerializeField] private TechCategoryFogManager servicesFogManager;
    [Tooltip("TechCategoryFogManager on the Sabotage panel.")]
    [SerializeField] private TechCategoryFogManager sabotageFogManager;

    // Private
    private List<Material> _fogMaterials = new List<Material>();
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

        if (confirmButtonText != null) confirmButtonText.text = "PURCHASE";
        if (confirmUpgradeButton != null) confirmUpgradeButton.interactable = false;

        RefreshSabotageButton();

        if (btnHardware) btnHardware.onClick.AddListener(() => RequestCategorySwitch(panelHardware, "Hardware"));
        if (btnWorkforce) btnWorkforce.onClick.AddListener(() => RequestCategorySwitch(panelWorkforce, "Workforce"));
        if (btnServices) btnServices.onClick.AddListener(() => RequestCategorySwitch(panelServices, "Services"));
        if (btnSabotage) btnSabotage.onClick.AddListener(() => RequestCategorySwitch(panelSabotage, "Sabotage"));
        if (openButton) openButton.onClick.AddListener(OpenTechTree);
        if (closeButton) closeButton.onClick.AddListener(CloseTechTree);
        if (confirmUpgradeButton) confirmUpgradeButton.onClick.AddListener(ConfirmPurchase);

        if (panelHardware) panelHardware.SetActive(false);
        if (panelWorkforce) panelWorkforce.SetActive(false);
        if (panelServices) panelServices.SetActive(false);
        if (panelSabotage) panelSabotage.SetActive(false);
        
        currentActiveCategory = null;

        // Instance fog shader materials so each Image is independent
        _fogMaterials.Clear();
        foreach (var img in fogImages)
        {
            if (img == null) continue;
            var mat = Instantiate(img.material);
            img.material = mat;
            _fogMaterials.Add(mat);
        }
    }

    // -------------------------------------------------------------------------
    // SABOTAGE BUTTON LOCK
    // -------------------------------------------------------------------------
    public void RefreshSabotageButton()
    {
        if (btnSabotage == null) return;

        PlayerData humanPlayer = GetHumanPlayer();
        bool unlocked = TechManager.Instance != null && humanPlayer != null && 
                        TechManager.Instance.IsSabotageTabUnlockedFor(humanPlayer);

        btnSabotage.interactable = unlocked;

        var cg = btnSabotage.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = unlocked ? 1f : 0.4f;
            cg.blocksRaycasts = unlocked;
        }

        if (!unlocked && currentActiveCategory == panelSabotage)
            RequestCategorySwitch(panelHardware, "Hardware");
    }

    // -------------------------------------------------------------------------
    // TRANSITION LOGIC
    // -------------------------------------------------------------------------
    public void RequestCategorySwitch(GameObject targetPanel, string categoryName)
    {
        if (currentActiveCategory == targetPanel) return;

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

        foreach (var mat in _fogMaterials)
            if (mat != null) mat.SetFloat("_ManualTime", Time.unscaledTime);

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
                if (hitObj.transform.IsChildOf(upgradeInfoPanel.transform) || hitObj == upgradeInfoPanel)
                { clickedSafeZone = true; break; }
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
            UIAnimator.DeactivateCurrentTechButton();
            CloseInfoPanel();
        }
    }

    public void SelectTechNode(TechNode node)
    {
        currentSelectedNode = node;
        if (upgradeInfoPanel != null)
        {
            if (upgradeInfoPanel.activeSelf)
                UpdateInfoPanelUI();
            else
            {
                upgradeInfoPanel.SetActive(true);
                UpdateInfoPanelUI();
            }
            
            if (confirmButtonText != null && confirmButtonText.text == "PURCHASE")
                TriggerButtonAnim(confirmUpgradeButton);
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

        UIAnimator.DeactivateCurrentTechButton();
        if (confirmUpgradeButton != null) confirmUpgradeButton.interactable = false;
    }

    private void UpdateInfoPanelUI()
    {
        if (currentSelectedNode == null) return;

        PlayerData humanPlayer   = GetHumanPlayer();
        bool isUnlocked          = humanPlayer != null && currentSelectedNode.IsUnlockedBy(humanPlayer);
        bool isResearching       = humanPlayer != null && currentSelectedNode.IsResearchingBy(humanPlayer);
        int  turnsRemaining      = isResearching
                                    ? TechManager.Instance.GetResearchTurnsRemaining(humanPlayer, currentSelectedNode)
                                    : 0;
        bool canUnlock           = humanPlayer != null && currentSelectedNode.CanUnlockFor(humanPlayer);
        int  playerRP            = humanPlayer != null ? humanPlayer.researchPoints : 0;
        bool canAfford           = playerRP >= currentSelectedNode.researchCost;

        // --- TITLE ---
        if (infoTitleText != null) infoTitleText.text = currentSelectedNode.techName;

        // --- DESCRIPTION ---
        if (infoDescriptionText != null)
        {
            string costLine = $"<color=yellow>Cost: {currentSelectedNode.researchCost} RP</color>";

            string durationLine = "";
            if (currentSelectedNode.researchTurns > 1)
            {
                durationLine = isResearching
                    ? $"\n<color=cyan>Integrating… {turnsRemaining} turn{(turnsRemaining == 1 ? "" : "s")} remaining</color>"
                    : $"\n<color=#aaaaaa>Research time: {currentSelectedNode.researchTurns} turns</color>";
            }

            infoDescriptionText.text = $"{currentSelectedNode.description}\n\n{costLine}{durationLine}";
        }

        // --- BUTTON STATE ---
        if (confirmUpgradeButton != null && confirmButtonText != null)
        {
            if (isUnlocked)
            {
                confirmUpgradeButton.interactable = false;
                confirmButtonText.text = "UNLOCKED";
            }
            else if (isResearching)
            {
                // Cost already paid; node is ticking down — prevent re-purchase.
                confirmUpgradeButton.interactable = false;
                confirmButtonText.text = $"IN RESEARCH ({turnsRemaining})";
            }
            else if (canUnlock)
            {
                if (canAfford)
                {
                    confirmUpgradeButton.interactable = true;
                    confirmButtonText.text = currentSelectedNode.researchTurns > 1
                        ? $"BEGIN RESEARCH"
                        : "PURCHASE";
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
        if (currentSelectedNode == null) return;

        if (TechManager.Instance != null)
            TechManager.Instance.ResearchTech(currentSelectedNode);
        else
        {
            // Fallback (no TechManager) — instant unlock via legacy API.
            if (currentSelectedNode.CanUnlock()) currentSelectedNode.UnlockTech();
        }

        // Always refresh UI after a purchase attempt regardless of queued vs instant.
        UpdateInfoPanelUI();
        RefreshAllTechButtons();
        RefreshSabotageButton();
        UpdateAllLines();
        RefreshAllEraFog(instant: false);
    }

    public void RefreshAllTechButtons()
    {
        TechButton[] buttons = FindObjectsByType<TechButton>(FindObjectsInactive.Include, FindObjectsSortMode.None); 
        foreach (var btn in buttons) if (btn != null) btn.UpdateNodeVisuals();
    }

    // Made public so TechManager.TickResearch can trigger a line refresh when
    // a queued tech completes outside of a normal UI purchase flow.
    public void UpdateAllLines()
    {
        TechLine[] lines = FindObjectsByType<TechLine>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var line in lines) if (line != null) line.UpdateVisuals(true);
    }

    // -------------------------------------------------------------------------
    // ERA FOG REFRESH
    // -------------------------------------------------------------------------
    public void RefreshAllEraFog(bool instant)
    {
        PlayerData humanPlayer = GetHumanPlayer();
        if (humanPlayer == null) return;

        hardwareFogManager?.RefreshAll(humanPlayer, instant);
        workforceFogManager?.RefreshAll(humanPlayer, instant);
        servicesFogManager?.RefreshAll(humanPlayer, instant);
        sabotageFogManager?.RefreshAll(humanPlayer, instant);
    }

    // -------------------------------------------------------------------------
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

        RefreshAllEraFog(instant: true);
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
        
        currentActiveCategory = null; 
        RequestCategorySwitch(panelHardware, "Hardware");
        RefreshAllTechButtons();
        RefreshSabotageButton();

        RefreshAllEraFog(instant: true);

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

    private void OnDestroy()
    {
        foreach (var mat in _fogMaterials)
            if (mat != null) Destroy(mat);
        _fogMaterials.Clear();
    }

    // -------------------------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------------------------
    private PlayerData GetHumanPlayer()
    {
        return (GameManager.Instance != null && GameManager.Instance.players.Count > 0)
            ? GameManager.Instance.players[0]
            : null;
    }
}