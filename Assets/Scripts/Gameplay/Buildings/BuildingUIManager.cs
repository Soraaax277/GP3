using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  BuildingUIManager  –  Universal world-space panel for all buildings.
//
//  Usage — call from OnMouseDown() in each building script:
//    BuildingUIManager.Instance.Open(this);
//
//  Supported buildings:
//    • SignalNode       → Root: [Construct Infrastructure] [Deploy Unit]
//    • BPOCenter        → Status display (powered, worker, income)
//    • CommercialHub    → Toggle auto-spawn button
//    • ServiceCenter    → Workforce recruitment (Foremen, Maintenance, IT)
//    • Canteen          → Workforce recruitment (Builder, Foremen, Technician)
//
//  To add a new building type:
//    1. Add a new branch in Open(MonoBehaviour building).
//    2. Write a private void Show<BuildingType>() method.
//    3. Wire OnMouseDown() in the building script to call Open(this).
// ─────────────────────────────────────────────────────────────────────────────

public class BuildingUIManager : MonoBehaviour
{
    public static BuildingUIManager Instance;

    [Header("UI References")]
    public GameObject panel;

    [Tooltip("Prefab: Button root + 'ActionLabel' TMP + 'CostLabel' TMP children.")]
    public GameObject actionButtonPrefab;

    [Tooltip("Prefab used for display/reminder rows (non-interactable info text). Falls back to actionButtonPrefab if unassigned.")]
    public GameObject reminderButtonPrefab;

    [Tooltip("The Content RectTransform inside the panel that owns the Vertical Layout Group + ContentSizeFitter.")]
    public Transform buttonContainer;

    [Tooltip("TMP header text at the top of the panel.")]
    public TextMeshProUGUI headerText;

    [Tooltip("Secondary TMP below the header. Used to show state messages like 'No units available for deployment.'")]
    public TextMeshProUGUI subHeaderText;

    [Tooltip("ScrollRect wrapping the buttonContainer. Enabled automatically when button count exceeds scrollButtonThreshold.")]
    public UnityEngine.UI.ScrollRect buttonScrollRect;

    [Tooltip("Number of buttons at which the list switches to a scrollable view.")]
    public int scrollButtonThreshold = 5;

    [Tooltip("Height in pixels of the empty spacer added at the bottom of the scroll content.")]
    public float scrollBottomPadding = 24f;

    public Camera mainCamera;

    [Header("World Space Settings")]
    public Vector3 menuOffset = new Vector3(2f, 3f, 0f);

    [Header("Button Text Colors")]
    public Color colorCanAfford       = Color.white;
    public Color colorCannotAfford    = Color.red;
    public Color colorNotInteractable = Color.grey;
    public Color colorReminder        = new Color(0.588f, 0.588f, 0.588f); // #969696

    [Header("Dependencies")]
    public TowerPlacementManager placementManager;

    // Kept public so TowerPlacementManager / WirePlacementManager can set it
    public bool ignoreNextClick = false;

    // ── Private state ─────────────────────────────────────────────────────────
    private MonoBehaviour currentBuilding;
    private Transform followTarget;
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    private struct ActionConfig
    {
        public string label;
        public int cost;
        public bool interactable;
        public bool isDisplay;
        public System.Action onClick;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        if (panel.activeSelf && followTarget != null)
        {
            if (mainCamera != null)
            {
                Vector3 offset = mainCamera.transform.rotation * menuOffset;
                panel.transform.position = followTarget.position + offset;
                panel.transform.rotation = mainCamera.transform.rotation;
            }
            else
            {
                panel.transform.position = followTarget.position + menuOffset;
            }
        }

        if (!panel.activeSelf) return;
        if (placementManager != null && placementManager.IsPlacing) return;
        if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (ignoreNextClick) { ignoreNextClick = false; return; }
            if (IsClickOnUIButton()) return;

            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (currentBuilding != null && hit.collider.gameObject != GetBuildingGameObject())
                    Close();
            }
            else
            {
                Close();
            }
        }
    }

    private bool IsClickOnUIButton()
    {
        PointerEventData pd = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pd, results);
        foreach (var r in results)
            if (r.gameObject.GetComponent<Button>() != null) return true;
        return false;
    }

    private GameObject GetBuildingGameObject()
    {
        return currentBuilding != null ? currentBuilding.gameObject : null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Open / Close
    // ─────────────────────────────────────────────────────────────────────────

    public void Open(MonoBehaviour building)
    {
        if (building == null) return;

        PlayerData owner = GetOwner(building);
        if (owner != null && owner.isAI) return;
        if (owner != null && TurnManager.Instance != null && owner != TurnManager.Instance.currentPlayer) return;

        // ── Hook: show detail panel ───────────────────────────────────────────
        if (DetailPanel.Instance != null)
            DetailPanel.Instance.ShowBuilding(building);
        // ─────────────────────────────────────────────────────────────────────

        // Handle Unbuilt state: Skip the world-space "Build/Deploy" action panel
        bool isUnbuilt = (building is TowerNode tn && tn.state == TowerNode.TowerState.Hologram) ||
                         (building is StructureNode sn && !sn.IsBuilt);

        if (isUnbuilt)
        {
            // Close the panel if it was open for something else
            if (panel.activeSelf) Close();
            return;
        }

        currentBuilding = building;
        followTarget    = building.transform;

        panel.SetActive(true);
        ignoreNextClick = true;

        if (CameraController.Instance != null)
            CameraController.Instance.SetBuildModeLock(true, followTarget.position);

        if      (building is SignalNode               hq)      { followTarget = hq.transform; ShowHQRoot(hq); }
        else if (building is BPOCenter                bpo)     ShowBPO(bpo);
        else if (building is CommercialHub            hub)     ShowCommercialHub(hub);
        // IMPORTANT: AdvancedServiceCenter inherits from ServiceCenter — it MUST be
        // checked first or the base ServiceCenter branch will silently catch it.
        else if (building is AdvancedServiceCenter    asc)     ShowAdvancedServiceCenter(asc);
        else if (building is ServiceCenter            sc)      ShowServiceCenter(sc);
        // AdvancedBusinessCenter inherits from StructureNode directly (not BusinessCenter),
        // so dispatch order doesn't matter here — but keeping advanced before base is
        // consistent convention and safe-guards against future refactoring.
        else if (building is AdvancedBusinessCenter   abc)     ShowAdvancedBusinessCenter(abc);
        else if (building is BusinessCenter           bc)      ShowBusinessCenter(bc);
        else if (building is Canteen                  canteen) ShowCanteen(canteen);
        else if (building is WireNode                 wire)    ShowWirePanel(wire);
        else if (building is Rocketship               rs)      ShowRocketship(rs);
        else if (building is PowerBox                 pb)      ShowPowerBox(pb);
        else if (building is SignalBooster            sb)      ShowSignalBooster(sb);
        else if (building is Tesseract                tes)     ShowTesseract(tes);
        else
        {
            ClearButtons();
            if (headerText != null) headerText.text = building.GetType().Name.ToUpper();
            SpawnDisplayRow("No actions available.");
        }
    }

    public void Close()
    {
        if (panel == null || !panel.activeSelf) return;

        if (CameraController.Instance != null)
            CameraController.Instance.SetBuildModeLock(false, Vector3.zero);

        UIAnimator animator = panel.GetComponent<UIAnimator>();
        if (animator != null)
        {
            animator.AnimateExit(() =>
            {
                panel.SetActive(false);
                ClearButtons();
                currentBuilding = null;
                followTarget    = null;
            });
        }
        else
        {
            ClearButtons();
            panel.SetActive(false);
            currentBuilding = null;
            followTarget    = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SignalNode (HQ) — Level 1: Root
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowHQRoot(SignalNode hq)
    {
        ClearButtons();
        if (headerText != null) headerText.text = "BUSINESS HQ";
        if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);

        SpawnButton(new ActionConfig
        {
            label        = "Construct",
            cost         = 0,
            interactable = true,
            onClick      = () => ShowHQConstruct(hq)
        });

        SpawnButton(new ActionConfig
        {
            label        = "Deploy Unit",
            cost         = 0,
            interactable = true,
            onClick      = () => ShowHQDeploy(hq)
        });

        RefreshScrollRect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SignalNode (HQ) — Level 2A: Construct Infrastructure
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowHQConstruct(SignalNode hq)
    {
        ClearButtons();
        if (headerText != null) headerText.text = "CONSTRUCT INFRASTRUCTURE";
        if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);

        SpawnButton(new ActionConfig { label = "← Back", cost = 0, interactable = true, onClick = () => ShowHQRoot(hq) });

        int  gold           = hq.owner.resources;
        // FIX (Bug 2): Use owner-explicit overload so unlock checks resolve the correct
        //              player regardless of who TurnManager.currentPlayer happens to be.
        bool towersUnlocked = IsUnlockedFor(hq.owner, "TelecomTowers");
        int  towerCost      = TowerPlacementManager.Instance != null ? TowerPlacementManager.Instance.GetCurrentTowerCost() : 0;

        SpawnButton(new ActionConfig
        {
            label        = "Construct Tower",
            cost         = towerCost,
            interactable = towersUnlocked && hq.CanPlaceTower() && gold >= towerCost,
            onClick      = () => { StartTowerPlacement(hq); Close(); }
        });

        // Guard StructurePlacementManager BEFORE spawning any structure
        // buttons. Try Instance first; fall back to FindObjectOfType so
        // a script-execution-order gap doesn't silently skip all buttons.
        // If it's still null after the fallback, the GameObject is simply
        // not in the scene — check the hierarchy and add it.
        var spm = StructurePlacementManager.Instance
                  ?? FindObjectOfType<StructurePlacementManager>();

        if (spm == null)
        {
            Debug.LogError("[BuildingUIManager] StructurePlacementManager not found in scene. " +
                           "Add it to an active GameObject. Structure buttons cannot be shown.");
            RefreshScrollRect();
            return;
        }

        // Cache the found instance so future lookups don't need FindObjectOfType.
        if (StructurePlacementManager.Instance == null)
            Debug.LogWarning("[BuildingUIManager] StructurePlacementManager.Instance was null — " +
                             "recovered via FindObjectOfType. Check script execution order or " +
                             "ensure the GameObject is active at scene start.");

        // FIX (Bug 2): Pass hq.owner into TryAddStructureButton so feature unlock
        //              checks are always evaluated against the correct player.
        TryAddStructureButton("Build Service Center",          "ServiceCenter",         spm.serviceCenterPrefab,         hq.owner);
        TryAddStructureButton("Build Advanced Service Center", "AdvancedServiceCenter", spm.advancedServiceCenterPrefab, hq.owner);
        TryAddStructureButton("Build BPO Center",      "BPOCenters",      spm.bpoCenterPrefab,      hq.owner);
        TryAddStructureButton("Build Commercial Hub",  "CommercialHubs",  spm.commercialHubPrefab,  hq.owner);
        TryAddStructureButton("Build Business Center",          "BusinessCenters",         spm.businessCenterPrefab,         hq.owner);
        TryAddStructureButton("Build Advanced Business Center", "AdvancedBusinessCenters", spm.advancedBusinessCenterPrefab, hq.owner);
        TryAddStructureButton("Build Worker Factory",  "WorkerFactories", spm.workerFactoryPrefab,  hq.owner);
        TryAddStructureButton("Build Drone Factory",   "DroneFactories",  spm.droneFactoryPrefab,   hq.owner);
        TryAddStructureButton("Build Signal Booster",  "SignalBooster",   spm.signalBoosterPrefab,  hq.owner);
        TryAddStructureButton("Build Signal Jammer",   "SignalJammers",   spm.signalJammerPrefab,   hq.owner);
        TryAddStructureButton("Build Power Box",       "PowerBoxes",      spm.powerBoxPrefab,       hq.owner);
        TryAddStructureButton("Build Tesseract",       "Tesseract",       spm.tesseractPrefab,      hq.owner);
        TryAddStructureButton("Build Canteen",         "Canteens",        spm.canteenPrefab,        hq.owner);
        TryAddStructureButton("Build Rocketship",      "Rocketship",      spm.rocketshipPrefab,     hq.owner);

        RefreshScrollRect();
    }

    // FIX (Bug 2): Added explicit PlayerData owner parameter so feature unlock checks
    //              always use the building owner's tech state, not currentPlayer.
    private void TryAddStructureButton(string label, string featureKey, GameObject prefab, PlayerData owner)
    {
        if (prefab == null || !IsUnlockedFor(owner, featureKey)) return;

        StructureNode node = prefab.GetComponent<StructureNode>();
        int cost = node != null ? node.baseGoldCost : 100;

        SpawnButton(new ActionConfig
        {
            label        = label,
            cost         = cost,
            interactable = owner != null && owner.resources >= cost,
            onClick      = () => { StructurePlacementManager.Instance.StartPlacement(prefab, featureKey); Close(); }
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SignalNode (HQ) — Level 2B: Deploy Unit
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowHQDeploy(SignalNode hq)
    {
        ClearButtons();
        if (headerText != null) headerText.text = "DEPLOY UNIT";

        SpawnButton(new ActionConfig { label = "← Back", cost = 0, interactable = true, onClick = () => ShowHQRoot(hq) });

        if (UnitSpawner.Instance == null) return;

        int     gold = hq.owner.resources;
        var     us   = UnitSpawner.Instance;
        HexTile tile = hq.tile;

        TryAddUnitButton("Recruit Builder",         us.builderPrefab,        gold, tile, hq.owner, "Builder");
        TryAddUnitButton("Recruit Wire Specialist", us.wireSpecialistPrefab, gold, tile, hq.owner, "Wire Specialist");
        TryAddUnitButton("Recruit Scout",           us.scoutPrefab,          gold, tile, hq.owner, "Scout");
        TryAddUnitButton("Recruit Technician",      us.technicianPrefab,     gold, tile, hq.owner, "Technician");
        TryAddUnitButton("Recruit Businessman",     us.businessmanPrefab,    gold, tile, hq.owner, "Businessman");
        TryAddUnitButton("Recruit Sales Marketer",  us.salesMarketerPrefab,  gold, tile, hq.owner, "SalesMarketer");
        TryAddUnitButton("Recruit Saboteur",        us.saboteurPrefab,       gold, tile, hq.owner, "Saboteur");
        TryAddUnitButton("Recruit Robo Worker",     us.roboWorkerPrefab,     gold, tile, hq.owner, "RoboWorker");
        TryAddUnitButton("Recruit Robo Marshall",   us.roboMarshallPrefab,   gold, tile, hq.owner, "RoboMarshall");

        // Only the ← Back button exists — no units unlocked yet
        if (spawnedButtons.Count == 1)
        {
            if (subHeaderText != null) { subHeaderText.text = "No units available for deployment."; subHeaderText.gameObject.SetActive(true); }
        }
        else
        {
            if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);
        }

        RefreshScrollRect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  BPO Center — status display
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowBPO(BPOCenter bpo)
    {
        ClearButtons();
        if (headerText != null) headerText.text = "BPO CENTER";
        if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);

        SpawnDisplayRow($"Powered: {(bpo.IsPowered ? "Yes" : "No")}");
        SpawnDisplayRow($"Worker: {bpo.GetCurrentWorkerName()}");

        int income = bpo.GetCurrentWorkerIncome();
        if (income > 0)
            SpawnDisplayRow($"Income: +{income}G per turn");
        else
            SpawnDisplayRow("Move a Businessman or IT Personnel\nonto this tile to earn passive gold.");

        TryAddEjectButton(bpo);
        RefreshScrollRect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Commercial Hub — toggle auto-spawn
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowCommercialHub(CommercialHub hub)
    {
        ClearButtons();
        if (headerText != null) headerText.text = "COMMERCIAL HUB";
        if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);

        SpawnDisplayRow($"Automation: {(hub.autoSpawnEnabled ? "ON" : "OFF")}");

        SpawnButton(new ActionConfig
        {
            label        = hub.autoSpawnEnabled ? "Disable Auto-Spawn" : "Enable Auto-Spawn",
            cost         = 0,
            interactable = true,
            onClick      = () => { hub.ToggleAutoSpawn(); ShowCommercialHub(hub); }
        });

        TryAddEjectButton(hub);
        RefreshScrollRect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Service Center — workforce recruitment
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowServiceCenter(ServiceCenter sc)
    {
        ClearButtons();
        if (headerText != null) headerText.text = "SERVICE CENTER";
        if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);

        if (UnitSpawner.Instance == null) return;

        int     gold = sc.owner.resources;
        var     us   = UnitSpawner.Instance;
        HexTile tile = sc.ParentTile;

        TryAddUnitButton("Recruit Maintenance Crew", us.maintenanceCrewPrefab, gold, tile, sc.owner, "MaintenanceCrew");
        TryAddUnitButton("Recruit Foremen",          us.foremenPrefab,         gold, tile, sc.owner, "Foreman");
        TryAddUnitButton("Recruit IT Personnel",     us.itPersonnelPrefab,     gold, tile, sc.owner, "ITPersonel");

        // No units unlocked yet
        if (spawnedButtons.Count == 0)
        {
            if (subHeaderText != null) { subHeaderText.text = "No units available for deployment."; subHeaderText.gameObject.SetActive(true); }
        }
        else
        {
            if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);
        }

        TryAddEjectButton(sc);
        RefreshScrollRect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Advanced Service Center — extended workforce recruitment
    //  Offers everything the base Service Center does, plus Robo Worker and
    //  Robo Marshall for players who have researched the advanced tier.
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowAdvancedServiceCenter(AdvancedServiceCenter asc)
    {
        ClearButtons();
        if (headerText != null) headerText.text = "ADVANCED SERVICE CENTER";
        if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);

        if (UnitSpawner.Instance == null) return;

        int     gold = asc.owner.resources;
        var     us   = UnitSpawner.Instance;
        HexTile tile = asc.ParentTile;

        // ── Base Service Center roster ────────────────────────────────────────
        TryAddUnitButton("Recruit Maintenance Crew", us.maintenanceCrewPrefab, gold, tile, asc.owner, "MaintenanceCrew");
        TryAddUnitButton("Recruit Foremen",          us.foremenPrefab,         gold, tile, asc.owner, "Foreman");
        TryAddUnitButton("Recruit IT Personnel",     us.itPersonnelPrefab,     gold, tile, asc.owner, "ITPersonel");

        // ── Advanced-only roster ──────────────────────────────────────────────
        TryAddUnitButton("Recruit Robo Worker",      us.roboWorkerPrefab,      gold, tile, asc.owner, "RoboWorker");
        TryAddUnitButton("Recruit Robo Marshall",    us.roboMarshallPrefab,    gold, tile, asc.owner, "RoboMarshall");

        if (spawnedButtons.Count == 0)
        {
            if (subHeaderText != null) { subHeaderText.text = "No units available for deployment."; subHeaderText.gameObject.SetActive(true); }
        }
        else
        {
            if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);
        }

        TryAddEjectButton(asc);
        RefreshScrollRect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Advanced Business Center — business unit recruitment
    //  Recruits business-oriented units not available at standard structures.
    //  All units are individually gated by their own tech unlock keys.
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowAdvancedBusinessCenter(AdvancedBusinessCenter abc)
    {
        ClearButtons();
        if (headerText != null) headerText.text = "ADVANCED BUSINESS CENTER";
        if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);

        if (UnitSpawner.Instance == null) return;

        int     gold = abc.owner.resources;
        var     us   = UnitSpawner.Instance;
        HexTile tile = abc.ParentTile;

        TryAddUnitButton("Recruit Businessman",    us.businessmanPrefab,   gold, tile, abc.owner, "Businessman");
        TryAddUnitButton("Recruit Sales Marketer", us.salesMarketerPrefab, gold, tile, abc.owner, "SalesMarketer");
        TryAddUnitButton("Recruit Saboteur",       us.saboteurPrefab,      gold, tile, abc.owner, "Saboteur");

        if (spawnedButtons.Count == 0)
        {
            if (subHeaderText != null) { subHeaderText.text = "No units available for deployment."; subHeaderText.gameObject.SetActive(true); }
        }
        else
        {
            if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);
        }

        TryAddEjectButton(abc);
        RefreshScrollRect();
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void ShowCanteen(Canteen canteen)
    {
        ClearButtons();
        if (headerText != null) headerText.text = "CANTEEN";
        if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);

        if (UnitSpawner.Instance == null) return;

        int     gold = canteen.owner.resources;
        var     us   = UnitSpawner.Instance;
        HexTile tile = canteen.ParentTile;

        TryAddUnitButton("Recruit Builder",    us.builderPrefab,    gold, tile, canteen.owner, "Builder");
        TryAddUnitButton("Recruit Foremen",    us.foremenPrefab,    gold, tile, canteen.owner, null);
        TryAddUnitButton("Recruit Technician", us.technicianPrefab, gold, tile, canteen.owner, "Technician");

        if (spawnedButtons.Count == 0)
        {
            if (subHeaderText != null) { subHeaderText.text = "No units available for deployment."; subHeaderText.gameObject.SetActive(true); }
        }
        else
        {
            if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);
        }

        TryAddEjectButton(canteen);
        RefreshScrollRect();
    }

    private void ShowRocketship(Rocketship rs)
    {
        ClearButtons();
        if (headerText != null) headerText.text = "ROCKETSHIP";
        if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);

        bool canL = rs.CanLaunch();

        SpawnButton(new ActionConfig
        {
            label        = "Launch Payload",
            cost         = 0,
            interactable = canL,
            onClick      = () => { rs.Launch(); Close(); }
        });

        if (!canL)
        {
            SpawnDisplayRow("Requires: 1 Technician & 1 Businessman stationed on Rocketship hexes.");
        }

        TryAddEjectButton(rs);
        RefreshScrollRect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Tower placement
    // ─────────────────────────────────────────────────────────────────────────

    private void StartTowerPlacement(SignalNode hq)
    {
        if (!IsUnlockedFor(hq.owner, "TelecomTowers"))
        {
            Debug.Log("[BuildingUIManager] 'Telecom Towers' not yet researched.");
            return;
        }

        if (hq.CanPlaceTower() && placementManager != null)
        {
            placementManager.StartTowerPlacement(hq);
            ignoreNextClick = true;
        }
    }

    /// <summary>Returns the SignalNode currently open in the panel, or null.</summary>
    public SignalNode GetCurrentBusiness() => currentBuilding as SignalNode;

    // ─────────────────────────────────────────────────────────────────────────
    //  Tech helpers
    // ─────────────────────────────────────────────────────────────────────────

    // All internal callers now use the owner-explicit overloads below.
    // The old parameterless versions are kept for any external callers.
    private bool IsUnlocked(string featureKey)
        => TechManager.Instance != null && TechManager.Instance.IsFeatureUnlocked(featureKey);

    // Explicit-player overload — always use this inside Show* methods.
    private bool IsUnlockedFor(PlayerData owner, string featureKey)
        => TechManager.Instance != null && TechManager.Instance.IsFeatureUnlockedFor(owner, featureKey);

    private bool IsUnitUnlocked(string unitName)
        => TechManager.Instance != null && TechManager.Instance.unlockedUnitNames.Contains(unitName);

    // Explicit-player overload for unit unlock checks.
    private bool IsUnitUnlockedFor(PlayerData owner, string unitName)
        => TechManager.Instance != null && TechManager.Instance.GetUnlockedUnitNamesFor(owner).Contains(unitName);

    // ─────────────────────────────────────────────────────────────────────────
    //  Owner helper
    // ─────────────────────────────────────────────────────────────────────────

    private PlayerData GetOwner(MonoBehaviour building)
    {
        if (building is SignalNode    sn) return sn.owner;
        if (building is StructureNode st) return st.owner;
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Button / display row helpers
    // ─────────────────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────────────────
    //  Unit Eject  –  Free "push out" with no action/movement cost.
    //  Finds the first unit on the building's occupied tiles (owner only),
    //  then teleports it to the nearest free adjacent hex.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a "Make Unit Leave" button if any owned unit is stationed in the building.
    /// Shows the first unit's type name so the player knows who is leaving.
    /// </summary>
    private void TryAddEjectButton(StructureNode building)
    {
        if (building == null) return;

        var stationed = building.GetStationedUnits();
        if (stationed.Count == 0) return;

        Unit first = stationed[0];
        string unitName = first.GetType().Name;

        // Friendly display name from a simple map — falls back to class name
        string displayName = unitName
            .Replace("Unit", "")
            .Replace("Crew", " Crew");

        SpawnButton(new ActionConfig
        {
            label        = $"Eject {displayName}  ({stationed.Count} inside)",
            cost         = 0,
            interactable = true,
            onClick      = () => { EjectFirstUnit(building); }
        });
    }

    /// <summary>
    /// Teleports the first stationed unit to the nearest free adjacent tile.
    /// Costs nothing — movement and charges are untouched.
    /// </summary>
    private void EjectFirstUnit(StructureNode building)
    {
        var stationed = building.GetStationedUnits();
        if (stationed.Count == 0) return;

        Unit unit = stationed[0];
        HexTile ejectTile = FindFreeEjectTile(building);

        if (ejectTile == null)
        {
            Debug.LogWarning("[BuildingUIManager] EjectFirstUnit: No free adjacent tile found.");
            return;
        }

        // Clear old tile
        if (unit.currentTile != null)
            unit.currentTile.placedUnit = null;

        // Place on new tile
        ejectTile.placedUnit = unit;
        unit.currentTile     = ejectTile;

        // Snap world position (same yOffset used in Unit.Initialize)
        float yOffset = 0.5f;
        unit.transform.position = new Vector3(
            ejectTile.transform.position.x,
            ejectTile.GetSurfaceY() + yOffset,
            ejectTile.transform.position.z
        );

        // Refresh the panel so the button updates or disappears
        Open(building);
    }

    /// <summary>
    /// Finds the first walkable, structure-free tile adjacent to any occupied tile
    /// of the building that also has no unit on it.
    /// </summary>
    private HexTile FindFreeEjectTile(StructureNode building)
    {
        if (GridManager.Instance == null) return null;

        // Collect all occupied tiles so we don't eject back onto the building itself
        var footprint = new HashSet<HexTile>();
        foreach (var t in building.GetStationedUnits()) { } // just for clarity — footprint built below
        // Re-use GetStationedUnits' tile loop via the public occupiedTiles indirectly:
        // We'll check all neighbours of ParentTile and secondary tiles via GridManager.
        var checkedTiles = new HashSet<HexTile>();

        // Check neighbours of every occupied tile in order (ParentTile first)
        var toCheck = new List<HexTile>();
        toCheck.Add(building.ParentTile);
        foreach (var t in GridManager.Instance.GetTilesInRange(building.ParentTile, building.tilesOccupied))
        {
            if (t.placedStructure == building) toCheck.Add(t);
        }

        foreach (var occupied in toCheck)
        {
            if (occupied == null) continue;
            foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(occupied))
            {
                if (neighbor == null) continue;
                if (checkedTiles.Contains(neighbor)) continue;
                checkedTiles.Add(neighbor);

                // Must be walkable (Land, no unit) and no structure
                if (neighbor.IsWalkable() && !neighbor.hasStructure)
                    return neighbor;
            }
        }

        return null;
    }

    private void SpawnDisplayRow(string text)
    {
        SpawnButton(new ActionConfig
        {
            label        = "\u24d8 " + text,
            cost         = 0,
            interactable = false,
            isDisplay    = true,
            onClick      = null
        });
    }

    // Shows a greyed-out placeholder for locked units so layout stays stable
    private void SpawnLockedPlaceholder()
    {
        SpawnButton(new ActionConfig
        {
            label        = "???",
            cost         = 0,
            interactable = false,
            isDisplay    = true,
            onClick      = null
        });
    }

    private void TryAddUnitButton(string label, GameObject prefab, int gold,
                                   HexTile spawnTile, PlayerData owner, string techUnlockName)
    {
        if (prefab == null) return;

        // FIX (Bug 2): Use owner-explicit unit unlock check so the correct player's
        //              tech state is always evaluated, not currentPlayer's.
        if (techUnlockName != null && !IsUnitUnlockedFor(owner, techUnlockName))
            return;

        int cost = UnitSpawner.Instance.GetRecruitmentCost(prefab, owner, spawnTile);
        SpawnButton(new ActionConfig
        {
            label        = label,
            cost         = cost,
            interactable = gold >= cost,
            onClick      = () => { UnitSpawner.Instance.SpawnUnit(prefab, spawnTile, owner); Close(); }
        });
    }

    private void SpawnButton(ActionConfig config)
    {
        if (actionButtonPrefab == null || buttonContainer == null)
        {
            Debug.LogError("[BuildingUIManager] actionButtonPrefab or buttonContainer is not assigned!");
            return;
        }

        // Use the reminder prefab for display rows if one is assigned; otherwise fall back to actionButtonPrefab.
        GameObject prefabToUse = (config.isDisplay && reminderButtonPrefab != null)
            ? reminderButtonPrefab
            : actionButtonPrefab;

        GameObject go = Instantiate(prefabToUse, buttonContainer);
        spawnedButtons.Add(go);

        TextMeshProUGUI[] texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);

        if (texts.Length >= 1)
        {
            texts[0].text  = config.label;
            texts[0].color = config.isDisplay
                ? colorReminder
                : (config.interactable ? colorCanAfford : colorNotInteractable);
        }

        if (texts.Length >= 2)
        {
            if (config.cost > 0)
            {
                PlayerData owner = GetOwner(currentBuilding);
                bool canAfford   = owner != null && owner.resources >= config.cost;
                texts[1].text    = $"{config.cost}G";
                texts[1].color   = canAfford ? colorCanAfford : colorCannotAfford;
                texts[1].gameObject.SetActive(true);
            }
            else
            {
                texts[1].gameObject.SetActive(false);
            }
        }

        Button btn = go.GetComponent<Button>();
        if (btn != null)
        {
            btn.interactable = config.interactable && !config.isDisplay;
            if (config.onClick != null)
            {
                System.Action cachedAction = config.onClick;
                btn.onClick.AddListener(() => cachedAction?.Invoke());
            }
        }

        // For reminder rows: bypass ContentSizeFitter entirely and directly
        // set the height from TMP's measured preferredHeight. This is the only
        // reliable way to size dynamic text at spawn time in Unity.
        if (config.isDisplay && texts.Length >= 1)
        {
            // Force TMP to calculate its layout immediately.
            texts[0].ForceMeshUpdate();

            float padding = 16f; // top + bottom padding inside the background
            float neededHeight = texts[0].preferredHeight + padding;

            // Resize the root of the spawned prefab.
            RectTransform goRect = go.GetComponent<RectTransform>();
            if (goRect != null)
            {
                goRect.sizeDelta = new Vector2(goRect.sizeDelta.x, neededHeight);
            }

            // Also resize the background child if it is a separate object.
            RectTransform bgRect = go.transform.childCount > 0
                ? go.transform.GetChild(0).GetComponent<RectTransform>()
                : null;
            if (bgRect != null && bgRect != goRect)
            {
                bgRect.sizeDelta = new Vector2(bgRect.sizeDelta.x, neededHeight);
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(buttonContainer as RectTransform);
    }

    private void ClearButtons()
    {
        foreach (GameObject go in spawnedButtons)
            if (go != null) Destroy(go);
        spawnedButtons.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Scroll Rect management
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enables vertical scrolling if button count exceeds scrollButtonThreshold.
    /// Uses spawnedButtons.Count — always accurate, no layout timing issues.
    /// Always appends a bottom spacer so the last item isn't flush against the edge.
    /// </summary>
    private void RefreshScrollRect()
    {
        // Count real buttons before adding the spacer so the threshold is accurate.
        bool needsScroll = spawnedButtons.Count > scrollButtonThreshold;

        SpawnBottomSpacer();

        if (buttonScrollRect == null) return;

        buttonScrollRect.enabled  = true;
        buttonScrollRect.vertical = needsScroll;

        // Always snap content back to the top when a view is loaded.
        buttonScrollRect.verticalNormalizedPosition = 1f;

        RectTransform contentRect = buttonContainer as RectTransform;
        if (contentRect != null)
        {
            Vector2 pos = contentRect.anchoredPosition;
            pos.y = 0f;
            contentRect.anchoredPosition = pos;
        }
    }

    /// <summary>
    /// Adds an invisible fixed-height spacer at the bottom of the button list so
    /// the last item is never flush against the scroll viewport edge.
    /// Tracked in spawnedButtons so it's destroyed on ClearButtons().
    /// </summary>
    private void SpawnBottomSpacer()
    {
        if (buttonContainer == null) return;

        GameObject spacer = new GameObject("BottomSpacer", typeof(RectTransform));
        spacer.transform.SetParent(buttonContainer, false);

        LayoutElement le = spacer.AddComponent<LayoutElement>();
        le.minHeight       = scrollBottomPadding;
        le.preferredHeight = scrollBottomPadding;

        spawnedButtons.Add(spacer);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Business Center
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowBusinessCenter(BusinessCenter bc)
    {
        ClearButtons();
        if (headerText != null) headerText.text = "BUSINESS CENTER";
        if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);

        bool managed = BusinessCenter.IsCorporateManagementActive(bc.owner);
        SpawnDisplayRow($"Corporate Management: {(managed ? "ACTIVE (+10% BPO Income)" : "Inactive — station a Businessman here")}");

        TryAddEjectButton(bc);
        RefreshScrollRect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Power Box
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowPowerBox(PowerBox pb)
    {
        ClearButtons();
        if (headerText != null) headerText.text = "POWER BOX";
        if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);

        bool hasTech = pb.IsMannedBy<Technician>();
        SpawnDisplayRow(hasTech
            ? "Energy Trading: ACTIVE  (20–100G / turn, 10% fuse risk)"
            : "Station a Technician here to enable Energy Trading.");

        TryAddEjectButton(pb);
        RefreshScrollRect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Signal Booster
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowSignalBooster(SignalBooster sb)
    {
        ClearButtons();
        if (headerText != null) headerText.text = "SIGNAL BOOSTER";
        if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);

        bool hasIT = sb.IsMannedBy<ITPersonnel>();
        SpawnDisplayRow(hasIT
            ? $"Signal Monetization: ACTIVE  (+2G per hex in range)"
            : "Station an IT Personnel here to enable Signal Monetization.");

        TryAddEjectButton(sb);
        RefreshScrollRect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Tesseract
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowTesseract(Tesseract tes)
    {
        ClearButtons();
        if (headerText != null) headerText.text = "TESSERACT";
        if (subHeaderText != null) subHeaderText.gameObject.SetActive(false);

        bool hasIT = tes.IsMannedBy<ITPersonnel>();
        SpawnDisplayRow(hasIT
            ? "Data Harvesting: ACTIVE  (+15G per non-owned hex in range / turn)"
            : "Station an IT Personnel here to enable Data Harvesting.");

        TryAddEjectButton(tes);
        RefreshScrollRect();
    }

    private void ShowWirePanel(WireNode wire)
    {
        ClearButtons();
        if (headerText != null) headerText.text = "POWER WIRE";

        PlayerData p = wire.owner;
        bool canUpgrade = TechManager.Instance != null && TechManager.Instance.IsFeatureUnlockedFor(p, "DialupInfrastructure");

        if (!wire.isDigital)
        {
            SpawnButton(new ActionConfig
            {
                label        = "Upgrade to Digital",
                cost         = 25,
                interactable = canUpgrade,
                onClick      = () => {
                    if (p.resources >= 25) {
                        p.resources -= 25;
                        wire.UpgradeToDigital();
                        ShowWirePanel(wire);
                    }
                }
            });
        }
        else
        {
            SpawnDisplayRow("Wire is Digital");
        }
    }
}