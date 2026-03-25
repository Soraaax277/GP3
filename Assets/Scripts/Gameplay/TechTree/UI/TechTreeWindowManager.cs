using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections;
using System;
using DG.Tweening;

[Serializable]
public class ImageSpriteSwap
{
    public Image targetImage;
    public Sprite newSprite;
}

[Serializable]
public class TechNodeSpriteSwap
{
    public TechNode triggerNode;
    public List<ImageSpriteSwap> swaps;
    public TMPro.TextMeshProUGUI[] textTargets;
    public Color textColor;
}

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

    [Header("Shutter Trigger Node")]
    [Tooltip("Assign TechNodes here. The shutter animation replays each time any of these nodes is unlocked.")]
    [SerializeField] private TechNode[] shutterTriggerNodes;

    [Header("Sprite Swaps")]
    [Tooltip("Each entry binds a TechNode to an Image + replacement Sprite. The swap happens at the midpoint of the techTreeAnimator shutter.")]
    [SerializeField] private TechNodeSpriteSwap[] spriteSwaps;

    // Private
    private HashSet<TechNode> _firedShutterNodes = new HashSet<TechNode>();
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
        if (confirmUpgradeButton != null)
        {
            if (confirmUpgradeButton.gameObject.GetComponent<UIButtonSounds>() == null)
                confirmUpgradeButton.gameObject.AddComponent<UIButtonSounds>();

            confirmUpgradeButton.interactable = false;
            // Ensure a CanvasGroup exists for fading
            if (confirmUpgradeButton.GetComponent<CanvasGroup>() == null)
                confirmUpgradeButton.gameObject.AddComponent<CanvasGroup>();
            SetConfirmButtonVisible(false, instant: true);
        }

        RefreshSabotageButton();

        if (btnHardware) 
        {
            if (btnHardware.gameObject.GetComponent<UIButtonSounds>() == null)
                btnHardware.gameObject.AddComponent<UIButtonSounds>();
            btnHardware.onClick.AddListener(() => RequestCategorySwitch(panelHardware, "Hardware"));
        }
        if (btnWorkforce) 
        {
            if (btnWorkforce.gameObject.GetComponent<UIButtonSounds>() == null)
                btnWorkforce.gameObject.AddComponent<UIButtonSounds>();
            btnWorkforce.onClick.AddListener(() => RequestCategorySwitch(panelWorkforce, "Workforce"));
        }
        if (btnServices) 
        {
            if (btnServices.gameObject.GetComponent<UIButtonSounds>() == null)
                btnServices.gameObject.AddComponent<UIButtonSounds>();
            btnServices.onClick.AddListener(() => RequestCategorySwitch(panelServices, "Services"));
        }
        if (btnSabotage) 
        {
            if (btnSabotage.gameObject.GetComponent<UIButtonSounds>() == null)
                btnSabotage.gameObject.AddComponent<UIButtonSounds>();
            btnSabotage.onClick.AddListener(() => RequestCategorySwitch(panelSabotage, "Sabotage"));
        }
        if (openButton) 
        {
            if (openButton.gameObject.GetComponent<UIButtonSounds>() == null)
                openButton.gameObject.AddComponent<UIButtonSounds>();
            openButton.onClick.AddListener(OpenTechTree);
        }
        if (closeButton) 
        {
            if (closeButton.gameObject.GetComponent<UIButtonSounds>() == null)
                closeButton.gameObject.AddComponent<UIButtonSounds>();
            closeButton.onClick.AddListener(CloseTechTree);
        }
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
            cg.blocksRaycasts = unlocked;

        if (btnSabotage.transform.childCount > 0)
        {
            var img = btnSabotage.transform.GetChild(0).GetComponent<Image>();
            if (img != null)
            {
                Color c = img.color;
                c.a = unlocked ? 1f : 0f;
                img.color = c;
            }
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

        // Shutter trigger — fires once per node when any assigned node is unlocked
        if (shutterTriggerNodes != null)
        {
            PlayerData humanPlayer = GetHumanPlayer();
            if (humanPlayer != null)
            {
                foreach (var node in shutterTriggerNodes)
                {
                    if (node == null || _firedShutterNodes.Contains(node)) continue;
                    if (node.IsUnlockedBy(humanPlayer))
                    {
                        _firedShutterNodes.Add(node);
                        PlayShutterSequence(node);
                    }
                }
            }
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
            SetConfirmButtonVisible(true, instant: false);
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
        SetConfirmButtonVisible(false, instant: false);
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
            // Research cost
            string costLine = $"Cost: {currentSelectedNode.researchCost} RP";

            // Prerequisites — unlocked ones are shown with strikethrough, hide section if none exist
            string prereqLine = "";
            if (currentSelectedNode.preReqs != null && currentSelectedNode.preReqs.Count > 0)
            {
                var reqEntries = new List<string>();
                foreach (var req in currentSelectedNode.preReqs)
                {
                    if (req == null) continue;
                    if (req.IsUnlockedBy(humanPlayer))
                        reqEntries.Add($"<s>{req.techName}</s>");
                    else
                        reqEntries.Add(req.techName);
                }
                if (reqEntries.Count > 0)
                {
                    string reqList = "\n  • " + string.Join("\n  • ", reqEntries);
                    prereqLine = $"\nRequires:{reqList}";
                }
            }

            // Research duration
            string durationLine = currentSelectedNode.researchTurns > 0
                ? isResearching
                    ? $"\nIntegrating… {turnsRemaining} turn{(turnsRemaining == 1 ? "" : "s")} remaining"
                    : $"\nResearch duration: {currentSelectedNode.researchTurns} turn{(currentSelectedNode.researchTurns == 1 ? "" : "s")}"
                : "\nResearch duration: Instant";

            // Passive RP bonus
            string passiveLine = currentSelectedNode.rpBonusPerTurn > 0
                ? $"\nPassive: +{currentSelectedNode.rpBonusPerTurn} RP/turn"
                : "";

            infoDescriptionText.text = $"{currentSelectedNode.description}\n\n{costLine}{prereqLine}{durationLine}{passiveLine}";
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

        // Immediately update the active research panel so newly queued
        // techs appear without waiting for the next turn tick.
        ActiveResearchPanel.Instance?.Refresh();
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

        RefreshAllEraFog(instant: true);

        TriggerButtonAnim(btnHardware);
        TriggerButtonAnim(btnWorkforce);
        TriggerButtonAnim(btnServices);
        TriggerButtonAnim(btnSabotage);
        TriggerButtonAnim(closeButton);

        RefreshSabotageButton(); // Must be AFTER TriggerButtonAnim — anims reset alpha
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
    private void PlayShutterSequence(TechNode triggerNode)
    {
        if (categoryShutter == null && techTreeAnimator == null) return;

        // Step 2 — fires at techTreeAnimator midpoint: swap sprites then let shutters open.
        void OnTechTreeMidpoint()
        {
            techTreeAnimator.onShutterClosed.RemoveAllListeners();
            ApplySpriteSwapsFor(triggerNode);
        }

        // Step 1 — fires at categoryShutter midpoint: start techTreeAnimator.
        void OnCategoryMidpoint()
        {
            categoryShutter.onShutterClosed.RemoveAllListeners();
            if (techTreeAnimator != null)
            {
                techTreeAnimator.onShutterClosed.RemoveAllListeners();
                techTreeAnimator.onShutterClosed.AddListener(OnTechTreeMidpoint);
                techTreeAnimator.PlayEntryAnimation();
            }
        }

        if (categoryShutter != null)
        {
            categoryShutter.onShutterClosed.RemoveAllListeners();
            categoryShutter.onShutterClosed.AddListener(OnCategoryMidpoint);
            categoryShutter.PlayEntryAnimation();
        }
        else if (techTreeAnimator != null)
        {
            techTreeAnimator.onShutterClosed.RemoveAllListeners();
            techTreeAnimator.onShutterClosed.AddListener(OnTechTreeMidpoint);
            techTreeAnimator.PlayEntryAnimation();
        }
    }

    private void ApplySpriteSwapsFor(TechNode triggerNode)
    {
        if (spriteSwaps == null || triggerNode == null) return;
        foreach (var entry in spriteSwaps)
        {
            if (entry == null || entry.triggerNode != triggerNode) continue;
            if (entry.swaps != null)
            {
                foreach (var swap in entry.swaps)
                {
                    if (swap == null) continue;
                    if (swap.targetImage != null && swap.newSprite != null)
                        swap.targetImage.sprite = swap.newSprite;
                }
            }
            if (entry.textTargets != null)
            {
                foreach (var tmp in entry.textTargets)
                    if (tmp != null) tmp.color = entry.textColor;
            }
        }
    }

    // -------------------------------------------------------------------------
    private void SetConfirmButtonVisible(bool visible, bool instant)
    {
        if (confirmUpgradeButton == null) return;
        var cg = confirmUpgradeButton.GetComponent<CanvasGroup>();
        if (cg == null) return;
        float target = visible ? 1f : 0f;
        // Enable raycasts/interactable immediately on show so button is ready when fade finishes.
        // Disable them immediately on hide so clicks are blocked during the fade out.
        cg.blocksRaycasts = visible;
        cg.interactable = visible;
        if (instant)
        {
            DOTween.Kill(cg);
            cg.alpha = target;
        }
        else
        {
            DOTween.Kill(cg);
            cg.DOFade(target, 0.2f).SetUpdate(true);
        }
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