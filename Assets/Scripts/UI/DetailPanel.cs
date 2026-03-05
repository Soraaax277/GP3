using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  DetailPanel  –  Screen-space info panel shown when clicking any owned
//                  unit or building. Slides in from the right.
//
//  Usage:
//    • Units:     DetailPanel.Instance.ShowUnit(unit)         ← hooked in PlayerInput
//    • Buildings: DetailPanel.Instance.ShowBuilding(building) ← hooked in BuildingUIManager.Open()
//
//  Closes when player clicks empty space.
//  Switches automatically when a different unit/building is clicked.
// ─────────────────────────────────────────────────────────────────────────────

public class DetailPanel : MonoBehaviour
{
    public static DetailPanel Instance;

    [Header("UI References")]
    public GameObject panel;
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI descriptionText;
    public Camera mainCamera;

    // ── Private state ─────────────────────────────────────────────────────────
    private object currentTarget;
    private UIAnimator uiAnimator;

    // ── Data ──────────────────────────────────────────────────────────────────
    private struct DetailData
    {
        public string header;
        public string description;
    }

    private static readonly Dictionary<System.Type, DetailData> UnitDetails
        = new Dictionary<System.Type, DetailData>
    {
        { typeof(BuilderUnit),     new DetailData { header = "BUILDER  |  Generalist",        description = "Jack-of-all-trades. Can build Towers AND Wires. Can eventually Sabotage. Cost: 50G" } },
        { typeof(WireSpecialist),  new DetailData { header = "WIRE SPECIALIST  |  Infrastructure", description = "Cheapest unit. Best for long power lines. Has more Wire Charges than anyone else. Cost: 40G" } },
        { typeof(ScoutUnit),       new DetailData { header = "SCOUT  |  Exploration",         description = "Highest vision range. Doesn't trigger Fog of War penalties. Can unlock Drone mode or Telescope tech for extra range. Cost: 45G" } },
        { typeof(Technician),      new DetailData { header = "TECHNICIAN  |  Core",           description = "Vital support unit. The only unit that can Power towers initially. High efficiency but low charges. Cost: 60G" } },
        { typeof(Businessman),     new DetailData { header = "BUSINESSMAN  |  Subversion",    description = "Tuned for high-success tower takeovers. Use to steal an enemy's base without destroying it. Generates income if placed on a BPO. Cost: 90G" } },
        { typeof(SalesMarketer),   new DetailData { header = "SALES MARKETER  |  Aggression", description = "Influence Control. Removes enemy influence and can recruit enemy workers. Best for destabilizing enemy territory. Cost: 70G" } },
        { typeof(Saboteurs),       new DetailData { header = "SABOTEUR  |  Destruction",      description = "Hard Damage specialist. Blows up enemy infrastructure far more effectively than a Builder. With Neutron Bombs tech, 20% chance to obliterate a structure. Cost: 110G" } },
        { typeof(MaintenanceCrew), new DetailData { header = "MAINTENANCE CREW  |  Support",  description = "Hybrid unit. Can build AND repair. Best for frontline work where structures break during expansion. Cost: 80G / 15 Upkeep" } },
        { typeof(Foremen),         new DetailData { header = "FOREMEN  |  Elite Builder",     description = "Speed and efficiency. More move range and double the Build Charges of a normal Builder. Cost: 100G / 20 Upkeep" } },
        { typeof(ITPersonnel),     new DetailData { header = "IT PERSONNEL  |  Elite Repair", description = "Massive repair efficiency. One IT unit can fully restore a dead tower in one action. Generates income if placed on a BPO. Cost: 120G / 18 Upkeep" } },
        { typeof(RoboWorker),      new DetailData { header = "ROBO WORKER  |  Late Game",     description = "Zero Upkeep. Expensive upfront but costs no gold per turn. Faster and more durable for massive endgame expansion. Cost: 150G / 0 Upkeep" } },
        { typeof(RoboMarshall),    new DetailData { header = "ROBO MARSHALL  |  Late Game",   description = "Zero Upkeep. Highest repair charges and efficiency in the game. Can roll Full Restore with specific tech. Cost: 180G / 0 Upkeep" } },
    };

    private static readonly Dictionary<System.Type, DetailData> BuildingDetails
        = new Dictionary<System.Type, DetailData>
    {
        { typeof(SignalNode),    new DetailData { header = "SIGNAL NODE  |  HQ",             description = "Core business building. Main hub for recruiting general units and placing your initial Towers." } },
        { typeof(ServiceCenter), new DetailData { header = "SERVICE CENTER  |  Recruitment", description = "Specialized recruitment hub. Required to spawn Workforce tier units: Maintenance Crew, Foremen, and IT Personnel." } },
        { typeof(BPOCenter),     new DetailData { header = "BPO CENTER  |  Economy",         description = "Generates extra passive income when specific units are placed on it. +50G for Businessmen, +30G for IT Personnel." } },
        { typeof(CommercialHub), new DetailData { header = "COMMERCIAL HUB  |  Economy",     description = "Economic structure focused on expanding commercial capabilities. Supports auto-spawn of units." } },
    };

    private static readonly Dictionary<string, DetailData> StructureDetails
        = new Dictionary<string, DetailData>
    {
        { "BusinessCenters",        new DetailData { header = "BUSINESS CENTER  |  Economy",          description = "Basic economic structure to increase revenue generation." } },
        { "AdvancedBusinessCenter",  new DetailData { header = "ADVANCED BUSINESS CENTER  |  Economy", description = "Upgraded economic structure for higher passive income generation." } },
        { "WorkerFactories",         new DetailData { header = "WORKER FACTORY  |  Production",       description = "Specialized facility to rapidly recruit and deploy robotic workforce units." } },
        { "DroneFactories",          new DetailData { header = "DRONE FACTORY  |  Production",        description = "Specialized facility for producing drone units quickly." } },
        { "PowerBoxes",              new DetailData { header = "POWER BOX  |  Infrastructure",        description = "Infrastructure node built to supply or extend power across the grid." } },
        { "SignalBooster",           new DetailData { header = "SIGNAL BOOSTER  |  Utility",          description = "Utility structure to enhance network and influence range." } },
        { "SignalJammers",           new DetailData { header = "SIGNAL JAMMER  |  Defense",           description = "Defensive and aggressive structure to disrupt enemy networks." } },
        { "Tesseract",               new DetailData { header = "TESSERACT  |  Advanced",              description = "Highly advanced structure. Powers ALL wires globally across the entire map when built." } },
        { "Rocketship",              new DetailData { header = "ROCKETSHIP  |  End Game",             description = "Ultimate end-game structure." } },
        { "TelecomTowers",           new DetailData { header = "TOWER NODE  |  Expansion",            description = "Primary expansion nodes built by Builders. Extends influence territory and connects wires." } },
    };

    // ─────────────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
        if (mainCamera == null) mainCamera = Camera.main;
        uiAnimator = panel != null ? panel.GetComponent<UIAnimator>() : null;
    }

    private void Update()
    {
        if (!panel.activeSelf) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (IsPointerOverUI()) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit _))
            Close();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    public void ShowUnit(Unit unit)
    {
        if (unit == null) return;
        if (currentTarget == (object)unit && panel.activeSelf) return;

        currentTarget = unit;

        if (UnitDetails.TryGetValue(unit.GetType(), out DetailData data))
            Display(data.header, data.description);
        else
            Display(unit.GetType().Name.ToUpper(), "No description available.");
    }

    public void ShowBuilding(MonoBehaviour building)
    {
        if (building == null) return;
        if (currentTarget == (object)building && panel.activeSelf) return;

        currentTarget = building;

        if (BuildingDetails.TryGetValue(building.GetType(), out DetailData data))
        {
            Display(data.header, data.description);
            return;
        }

        if (building is StructureNode sn && StructureDetails.TryGetValue(sn.GetRequiredTechFeature(), out DetailData structData))
        {
            Display(structData.header, structData.description);
            return;
        }

        Display(building.GetType().Name.ToUpper(), "No description available.");
    }

    public void Close()
    {
        if (panel == null || !panel.activeSelf) return;

        if (uiAnimator != null)
        {
            uiAnimator.AnimateExit(() =>
            {
                panel.SetActive(false);
                currentTarget = null;
            });
        }
        else
        {
            panel.SetActive(false);
            currentTarget = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Internal
    // ─────────────────────────────────────────────────────────────────────────

    private void Display(string header, string description)
    {
        if (headerText != null)      headerText.text      = header;
        if (descriptionText != null) descriptionText.text = description;

        if (!panel.activeSelf)
        {
            panel.SetActive(true);
            if (uiAnimator != null) uiAnimator.PlayEntryAnimation();
        }
    }

    private bool IsPointerOverUI()
    {
        PointerEventData pd = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pd, results);
        return results.Count > 0;
    }
}