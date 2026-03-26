using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

public class UnitActionPanel : MonoBehaviour
{
    public static UnitActionPanel Instance;

    [Header("UI References")]
    public GameObject panel;

    [Tooltip("Prefab: Button root + 'ActionLabel' TMP + 'CostLabel' TMP children.")]
    public GameObject actionButtonPrefab;

    [Tooltip("The Content transform that owns the Vertical/Horizontal Layout Group.")]
    public Transform buttonContainer;

    [Tooltip("TMP Text at the top of the container that shows the unit type header.")]
    public TextMeshProUGUI headerText;

    [Tooltip("ScrollRect wrapping the buttonContainer. Enabled automatically when button count exceeds scrollButtonThreshold.")]
    public UnityEngine.UI.ScrollRect buttonScrollRect;

    [Tooltip("Number of buttons at which the list switches to a scrollable view.")]
    public int scrollButtonThreshold = 5;

    public Camera mainCamera;

    [Header("World Space Settings")]
    public Vector3 menuOffset = new Vector3(3.75f, 0.25f, -3.5f);

    [Header("Panel Flip")]
    [Tooltip("The ScrollViewport child of the panel — its X scale is counter-flipped so buttons stay readable.")]
    public Transform scrollViewport;

    [Tooltip("Horizontal screen-space deadzone in pixels. Prevents jitter when mouse is directly over the unit.")]
    public float flipDeadzone = 40f;

    [Header("Button Text Colors")]
    [Tooltip("Label color when the player can afford the action.")]
    public Color colorCanAfford = Color.white;

    [Tooltip("Label color when the player cannot afford the action.")]
    public Color colorCannotAfford = Color.red;

    [Tooltip("Label color when the button is not interactable (action already used etc).")]
    public Color colorNotInteractable = Color.grey;

    // ── Private state ─────────────────────────────────────────────────────────
    private Transform followTarget;
    private Unit currentUnit;
    private HexTile lastRefreshTile;
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    // Flip state — tracked so we only apply changes when it actually changes
    private bool _isFlippedLeft = false;

    // ── Internal data ─────────────────────────────────────────────────────────
    private struct ActionConfig
    {
        public string label;
        public int cost;
        public bool interactable;
        public bool isDisplay;
        public System.Action onClick;
    }

    // ── Unit type display names ───────────────────────────────────────────────
    private static readonly Dictionary<System.Type, string> UnitDisplayNames = new Dictionary<System.Type, string>
    {
        { typeof(BuilderUnit),     "BUILDER"           },
        { typeof(WireSpecialist),  "WIRE SPECIALIST"   },
        { typeof(Technician),      "TECHNICIAN"        },
        { typeof(Foremen),         "FOREMAN"           },
        { typeof(RoboWorker),      "ROBO WORKER"       },
        { typeof(ITPersonnel),     "IT PERSONNEL"      },
        { typeof(MaintenanceCrew), "MAINTENANCE CREW"  },
        { typeof(RoboMarshall),    "ROBO MARSHALL"     },
        { typeof(Saboteurs),       "SABOTEUR"          },
        { typeof(SalesMarketer),   "SALES MARKETER"    },
        { typeof(Businessman),     "BUSINESSMAN"       },
        { typeof(ScoutUnit),       "SCOUT"             },
    };

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
        if (!panel.activeSelf || followTarget == null) return;

        // ── Flip logic ────────────────────────────────────────────────────────
        // Convert unit world position to screen space, compare against mouse X.
        // A deadzone prevents jitter when the cursor sits directly over the unit.
        // Flip is frozen when the mouse is inside the panel so buttons stay
        // hittable and a click never triggers a mid-action flip.
        if (mainCamera != null)
        {
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            bool mouseOverPanel = panelRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(panelRect, mousePos, mainCamera);

            if (!mouseOverPanel)
            {
                Vector3 unitScreenPos = mainCamera.WorldToScreenPoint(followTarget.position);
                float deltaX = mousePos.x - unitScreenPos.x;

                if (Mathf.Abs(deltaX) > flipDeadzone)
                {
                    bool wantFlip = deltaX > 0f; // mouse is to the right → flip panel left
                    if (wantFlip != _isFlippedLeft)
                    {
                        _isFlippedLeft = wantFlip;
                        ApplyFlip();
                    }
                }
            }
        }

        // ── Position tracking ─────────────────────────────────────────────────
        Vector3 activeOffset = GetCurrentOffset();

        if (mainCamera != null)
        {
            Vector3 offset = mainCamera.transform.rotation * activeOffset;
            panel.transform.position = followTarget.position + offset;
            panel.transform.rotation = mainCamera.transform.rotation;
        }
        else
        {
            panel.transform.position = followTarget.position + activeOffset;
        }

        // ── Dynamic refresh ───────────────────────────────────────────────────
        if (currentUnit != null && currentUnit.isMoving && currentUnit.currentTile != lastRefreshTile)
            Refresh(true);
    }

    // Returns the offset with X sign adjusted for current flip state.
    private Vector3 GetCurrentOffset()
    {
        return new Vector3(
            _isFlippedLeft ? -Mathf.Abs(menuOffset.x) : Mathf.Abs(menuOffset.x),
            menuOffset.y,
            menuOffset.z);
    }

    [Header("Flip Animation")]
    [Tooltip("Total duration of the card-flip squeeze in seconds.")]
    public float flipDuration = 0.2f;
    [Tooltip("Ease applied to the flip squeeze.")]
    public Ease flipEase = Ease.InOutSine;

    // Squeezes the panel to scaleX=0 at the midpoint, snaps direction, then expands back.
    // The ScrollViewport is counter-scaled so buttons/text are never flipped visually.
    private void ApplyFlip()
    {
        float targetX = _isFlippedLeft ? -1f : 1f;

        // Kill any in-progress tweens on both transforms
        DOTween.Kill(panel.transform);
        if (scrollViewport != null) DOTween.Kill(scrollViewport);

        float half = flipDuration * 0.5f;

        // Phase 1: squeeze to 0
        panel.transform.DOScaleX(0f, half).SetEase(flipEase).OnComplete(() =>
        {
            // Snap direction at the invisible midpoint
            Vector3 ps = panel.transform.localScale;
            panel.transform.localScale = new Vector3(targetX, ps.y, ps.z);

            if (scrollViewport != null)
            {
                Vector3 sv = scrollViewport.localScale;
                scrollViewport.localScale = new Vector3(targetX, sv.y, sv.z);
            }

            // Phase 2: expand back out
            panel.transform.DOScaleX(targetX, half).SetEase(flipEase);
            if (scrollViewport != null)
                scrollViewport.DOScaleX(targetX, half).SetEase(flipEase);
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Open / Close
    // ─────────────────────────────────────────────────────────────────────────

    public void Open(Unit unit, bool silent = false)
    {
        if (unit == null) return;

        currentUnit  = unit;
        followTarget = unit.transform;

        // Pre-compute the correct flip direction from the current mouse position
        // so both panel and scrollViewport are already at the right scale the moment
        // the panel becomes visible. This prevents Update()'s flip logic from firing
        // ApplyFlip() on frame 1 and causing the entrance glitch.
        if (mainCamera != null)
        {
            Vector3 unitScreenPos = mainCamera.WorldToScreenPoint(unit.transform.position);
            Vector2 mp = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            _isFlippedLeft = (mp.x - unitScreenPos.x) > 0f;
        }
        else
        {
            _isFlippedLeft = false;
        }

        float openScaleX = _isFlippedLeft ? -1f : 1f;
        DOTween.Kill(panel.transform);
        panel.transform.localScale = new Vector3(openScaleX, panel.transform.localScale.y, panel.transform.localScale.z);
        if (scrollViewport != null)
        {
            DOTween.Kill(scrollViewport);
            scrollViewport.localScale = new Vector3(openScaleX, scrollViewport.localScale.y, scrollViewport.localScale.z);
        }

        Refresh(silent);

        panel.SetActive(true);

        if (CameraController.Instance != null)
            CameraController.Instance.SetBuildModeLock(true, followTarget.position);
    }

    public void Refresh(bool silent = false)
    {
        if (currentUnit == null) return;

        lastRefreshTile = currentUnit.currentTile;
        ClearButtons();

        currentUnit.CheckTechStatus();

        List<ActionConfig> actions = BuildActionsFor(currentUnit);

        if (actions.Count == 0)
        {
            actions.Add(new ActionConfig
            {
                label        = "NO ACTIONS AVAILABLE",
                cost         = 0,
                interactable = false,
                isDisplay    = true
            });
        }

        if (headerText != null)
            headerText.text = "SELECT AN ACTION:";

        foreach (ActionConfig action in actions)
            SpawnButton(action);

        RefreshScrollRect();
    }

    public void Close()
    {
        UIAnimator animator = panel.GetComponent<UIAnimator>();
        if (animator != null)
        {
            animator.AnimateExit(() =>
            {
                panel.SetActive(false);
                ClearButtons();
                currentUnit  = null;
                followTarget = null;
            });
        }
        else
        {
            ClearButtons();
            panel.SetActive(false);
            currentUnit  = null;
            followTarget = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Action builder  –  one branch per unit type
    // ─────────────────────────────────────────────────────────────────────────

    private List<ActionConfig> BuildActionsFor(Unit unit)
    {
        var actions = new List<ActionConfig>();
        int gold    = unit.owner.resources;
        bool canAct = unit.canAct;

        if (unit.IsNearServiceCenter())
        {
            int refillCost = unit.GetRefillCost();
            actions.Add(new ActionConfig
            {
                label        = "Refill",
                cost         = refillCost,
                interactable = gold >= refillCost,
                onClick      = () => { unit.RefillCharges(); Close(); }
            });
        }

        if (unit is BuilderUnit builder)
        {
            if (builder.canConstructTower)
            {
                int cost = builder.GetBuildingCost();
                actions.Add(new ActionConfig { label = "Construct",   cost = cost, interactable = canAct && gold >= cost, onClick = () => { builder.ConstructAdjacentInfrastructure(); Close(); } });
            }
            if (builder.canRepairInfrastructure)
            {
                int cost = builder.GetRepairCost();
                actions.Add(new ActionConfig { label = "Repair",      cost = cost, interactable = canAct && gold >= cost, onClick = () => { builder.RepairAdjacentStructure(); Close(); } });
            }
            if (builder.canSabotage)
                actions.Add(new ActionConfig { label = "Sabotage",    cost = 0,    interactable = canAct,                 onClick = () => { builder.DamageAdjacentStructure(); Close(); } });
        }
        else if (unit is WireSpecialist specialist)
        {
            int wireCost = WirePlacementManager.Instance != null ? WirePlacementManager.Instance.GetCurrentWireCost() : 0;
            actions.Add(new ActionConfig { label = "Lay Wire",        cost = wireCost, interactable = canAct && gold >= wireCost, onClick = () => { WirePlacementManager.Instance.StartWirePlacement(specialist); Close(); } });
            if (specialist.canRepairTowers)
            {
                int cost = specialist.GetRepairCost();
                actions.Add(new ActionConfig { label = "Repair Tower", cost = cost, interactable = canAct && gold >= cost, onClick = () => { specialist.RepairAdjacentTower(); Close(); } });
            }
            if (specialist.canSabotage)
                actions.Add(new ActionConfig { label = "Sabotage",    cost = 0,    interactable = canAct, onClick = () => { specialist.DamageAdjacentStructure(); Close(); } });
        }
        else if (unit is Technician technician)
        {
            if (technician.IsAtBase() && !technician.isResearching)
            {
                actions.Add(new ActionConfig
                {
                    label        = "Research HW",
                    cost         = 500,
                    interactable = canAct && gold >= 500 && technician.owner.researchPoints >= 200,
                    onClick      = () => { technician.StartResearchProject("Era4Hardware"); Close(); }
                });
            }
            int repairCost = technician.GetRepairCost();
            actions.Add(new ActionConfig { label = "Repair", cost = repairCost, interactable = canAct && gold >= repairCost, onClick = () => { technician.RepairAdjacentStructure(); Close(); } });
            actions.Add(new ActionConfig { label = "Power",  cost = 0,          interactable = canAct,                      onClick = () => { technician.PowerAdjacentStructure();  Close(); } });
        }
        else if (unit is Foremen foremen)
        {
            int cost = foremen.GetBuildingCost();
            actions.Add(new ActionConfig { label = "Construct", cost = cost, interactable = canAct && gold >= cost, onClick = () => { foremen.ConstructAdjacentInfrastructure(); Close(); } });
        }
        else if (unit is RoboWorker roboWorker)
        {
            int cost = roboWorker.GetBuildingCost();
            actions.Add(new ActionConfig { label = "Construct", cost = cost, interactable = canAct && gold >= cost, onClick = () => { roboWorker.ConstructAdjacentInfrastructure(); Close(); } });
        }
        else if (unit is ITPersonnel itPersonnel)
        {
            int cost = itPersonnel.GetRepairCost();
            actions.Add(new ActionConfig { label = "Repair", cost = cost, interactable = canAct && gold >= cost, onClick = () => { itPersonnel.RepairAdjacentStructure(); Close(); } });
        }
        else if (unit is MaintenanceCrew maintenanceCrew)
        {
            if (maintenanceCrew.canRepairTowers)
            {
                int cost = maintenanceCrew.GetRepairCost();
                actions.Add(new ActionConfig { label = "Maintain", cost = cost, interactable = canAct && gold >= cost, onClick = () => { maintenanceCrew.PerformMaintenance(); Close(); } });
            }
        }
        else if (unit is RoboMarshall roboMarshall)
        {
            int cost = roboMarshall.GetRepairCost();
            actions.Add(new ActionConfig { label = "Repair", cost = cost, interactable = canAct && gold >= cost, onClick = () => { roboMarshall.RepairAdjacentStructure(); Close(); } });
        }
        else if (unit is Saboteurs saboteur)
        {
            actions.Add(new ActionConfig { label = "Sabotage", cost = 0, interactable = canAct, onClick = () => { saboteur.DamageAdjacentStructure(); Close(); } });
        }
        else if (unit is SalesMarketer marketer)
        {
            actions.Add(new ActionConfig { label = "Deny",    cost = 0, interactable = canAct, onClick = () => { marketer.PerformDeny(); Close(); } });
            if (marketer.canRecruit)
                actions.Add(new ActionConfig { label = "Recruit", cost = 0, interactable = canAct, onClick = () => { marketer.RecruitNearestWorker(); Close(); } });
        }
        else if (unit is Businessman businessman)
        {
            actions.Add(new ActionConfig { label = "Convert", cost = 0, interactable = canAct, onClick = () => { businessman.RecruitNearestWorker(); Close(); } });
        }

        return actions;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Scroll Rect management
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshScrollRect()
    {
        if (buttonScrollRect == null) return;

        bool needsScroll = spawnedButtons.Count > scrollButtonThreshold;

        buttonScrollRect.enabled  = true;
        buttonScrollRect.vertical = needsScroll;
        buttonScrollRect.verticalNormalizedPosition = 1f;

        RectTransform contentRect = buttonContainer as RectTransform;
        if (contentRect != null)
        {
            Vector2 pos = contentRect.anchoredPosition;
            pos.y = 0f;
            contentRect.anchoredPosition = pos;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Button helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void SpawnButton(ActionConfig config)
    {
        if (actionButtonPrefab == null || buttonContainer == null)
        {
            Debug.LogError("[UnitActionPanel] actionButtonPrefab or buttonContainer is not assigned!");
            return;
        }

        GameObject go = Instantiate(actionButtonPrefab, buttonContainer);
        spawnedButtons.Add(go);

        TextMeshProUGUI[] texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);

        if (texts.Length >= 1)
        {
            texts[0].text  = config.label;
            texts[0].color = config.isDisplay ? Color.gray : (config.interactable ? colorCanAfford : colorNotInteractable);
        }

        if (texts.Length >= 2)
        {
            if (config.cost > 0)
            {
                bool canAfford    = currentUnit != null && currentUnit.owner.resources >= config.cost;
                texts[1].text     = $"{config.cost}G";
                texts[1].color    = canAfford ? colorCanAfford : colorCannotAfford;
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
            if (go.GetComponent<UIButtonSounds>() == null)
                go.AddComponent<UIButtonSounds>();

            btn.interactable = config.interactable && !config.isDisplay;
            if (config.onClick != null)
            {
                System.Action cachedAction = config.onClick;
                btn.onClick.AddListener(() => cachedAction?.Invoke());
            }
        }
    }

    private void ClearButtons()
    {
        foreach (GameObject go in spawnedButtons)
            if (go != null) Destroy(go);
        spawnedButtons.Clear();
    }
}