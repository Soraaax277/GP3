using UnityEngine;
using UnityEngine.EventSystems;
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

    public Camera mainCamera;

    [Header("World Space Settings")]
    public Vector3 menuOffset = new Vector3(2f, 3f, 0f);

    [Header("Button Text Colors")]
    public Color colorCanAfford       = Color.white;
    public Color colorCannotAfford    = Color.red;
    public Color colorNotInteractable = Color.grey;
    public Color colorDisplay         = Color.cyan;

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

        if (Input.GetMouseButtonDown(0))
        {
            if (ignoreNextClick) { ignoreNextClick = false; return; }
            if (IsClickOnUIButton()) return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
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
        PointerEventData pd = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
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
        else if (building is Canteen                  canteen) ShowCanteen(canteen);
        else if (building is WireNode                 wire)    ShowWirePanel(wire);
        else if (building is Rocketship               rs)      ShowRocketship(rs);
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

    private void SpawnDisplayRow(string text)
    {
        SpawnButton(new ActionConfig
        {
            label        = text,
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

        GameObject go = Instantiate(actionButtonPrefab, buttonContainer);
        spawnedButtons.Add(go);

        TextMeshProUGUI[] texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);

        if (texts.Length >= 1)
        {
            texts[0].text  = config.label;
            texts[0].color = config.isDisplay
                ? colorDisplay
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
    /// </summary>
    private void RefreshScrollRect()
    {
        if (buttonScrollRect == null) return;

        bool needsScroll = spawnedButtons.Count > scrollButtonThreshold;

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
                 label = "Upgrade to Digital",
                 cost = 25,
                 interactable = canUpgrade,
                 onClick = () => {
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