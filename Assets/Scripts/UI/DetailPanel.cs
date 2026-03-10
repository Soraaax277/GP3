using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;

// ─────────────────────────────────────────────────────────────────────────────
//  DetailPanel  –  Screen-space info panel shown when clicking any owned
//                  unit or building.
//
//  Units:
//    Header:      "BUILDER  |  Generalist   Lv.1 ★☆☆"
//    Description: flavour line, then live stats (charges, movement, upkeep,
//                 unit-specific stats, capability flags)
//
//  Buildings:
//    Header:      "SERVICE CENTER  |  Recruitment"
//    Description: static flavour text (unchanged)
//
//  ShowUnit() always rebuilds — stats change every turn.
//  ShowBuilding() skips rebuild if same target is already open.
// ─────────────────────────────────────────────────────────────────────────────

public class DetailPanel : MonoBehaviour
{
    public static DetailPanel Instance;

    [Header("UI References")]
    public GameObject      panel;
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI descriptionText;
    public Camera          mainCamera;

    // ── Private state ─────────────────────────────────────────────────────────
    private object     currentTarget;
    private UIAnimator uiAnimator;

    // ── Unit metadata (display name + hardcoded description) ─────────────────
    private struct UnitMeta { public string displayName; public string description; }

    private static readonly Dictionary<System.Type, UnitMeta> UnitMetas
        = new Dictionary<System.Type, UnitMeta>
    {
        { typeof(BuilderUnit),     new UnitMeta { displayName = "Builder",          description = "Primary construction unit." } },
        { typeof(WireSpecialist),  new UnitMeta { displayName = "Wire Specialist",  description = "Infrastructure unit. Charge wires and connect across the grid." } },
        { typeof(ScoutUnit),       new UnitMeta { displayName = "Scout",            description = "Exploration unit with high movement & vision." } },
        { typeof(Technician),      new UnitMeta { displayName = "Technician",       description = "Field support unit. Power up and activate adjacent wires." } },
        { typeof(Businessman),     new UnitMeta { displayName = "Businessman",      description = "Corporate unit with recruit charges." } },
        { typeof(SalesMarketer),   new UnitMeta { displayName = "Sales Marketer",   description = "Persuasion/Area-Denial unit." } },
        { typeof(Saboteurs),       new UnitMeta { displayName = "Saboteur",         description = "Dedicated aggressive unit. Damages enemy towers." } },
        { typeof(MaintenanceCrew), new UnitMeta { displayName = "Maintenance Crew", description = "Specializes in repairing destroyed towers with decent efficiency." } },
        { typeof(Foremen),         new UnitMeta { displayName = "Foremen",          description = "Elite builder unit. The builders' superiors." } },
        { typeof(ITPersonnel),     new UnitMeta { displayName = "IT Personnel",     description = "Highly efficient, can repair both towers and wires. Generates income if placed on a BPO." } },
        { typeof(RoboWorker),      new UnitMeta { displayName = "Robo Worker",      description = "Futuristic construction drone." } },
        { typeof(RoboMarshall),    new UnitMeta { displayName = "Robo Marshall",    description = "The ultimate robotic repair entity. Superior repair efficiency." } },
    };

    // ── Building / Structure data (static) ────────────────────────────────────
    private struct DetailData { public string header; public string description; }

    // IMPORTANT: derived types (AdvancedServiceCenter) must appear BEFORE their
    // base type (ServiceCenter) so TryGetValue hits the correct entry first.
    private static readonly Dictionary<System.Type, DetailData> BuildingDetails
        = new Dictionary<System.Type, DetailData>
    {
        { typeof(SignalNode),             new DetailData { header = "SIGNAL NODE  |  HQ",                       description = "Core business building. Main hub for recruiting general units and placing your initial Towers." } },
        { typeof(AdvancedServiceCenter),  new DetailData { header = "ADVANCED SERVICE CENTER  |  Recruitment",  description = "Upgraded Service Center. Adds Robo Worker and Robo Marshall to the recruitment roster." } },
        { typeof(ServiceCenter),          new DetailData { header = "SERVICE CENTER  |  Recruitment",           description = "Specialized recruitment hub. Unlocks Maintenance Crew, Foremen, and IT Personnel." } },
        { typeof(AdvancedBusinessCenter), new DetailData { header = "ADVANCED BUSINESS CENTER  |  Economy",     description = "Premium recruitment hub. Deploys Businessmen, Sales Marketers, and Saboteurs." } },
        { typeof(BPOCenter),              new DetailData { header = "BPO CENTER  |  Economy",                   description = "Generates passive income when specific units are stationed here. +50G Businessman, +30G IT Personnel." } },
        { typeof(CommercialHub),          new DetailData { header = "COMMERCIAL HUB  |  Economy",               description = "Economic structure. Supports auto-spawn of units each turn." } },
        { typeof(Canteen),                new DetailData { header = "CANTEEN  |  Recruitment",                  description = "Field recruitment hub. Produces Builders, Foremen, and Technicians close to the front line." } },
    };

    private static readonly Dictionary<string, DetailData> StructureDetails
        = new Dictionary<string, DetailData>
    {
        { "BusinessCenters",         new DetailData { header = "BUSINESS CENTER  |  Economy",             description = "Basic economic structure to increase revenue generation." } },
        { "AdvancedBusinessCenters", new DetailData { header = "ADVANCED BUSINESS CENTER  |  Economy",    description = "Upgraded economic structure. Unlocks business-tier unit recruitment." } },
        { "WorkerFactories",         new DetailData { header = "WORKER FACTORY  |  Production",           description = "Specialized facility to rapidly recruit and deploy robotic workforce units." } },
        { "DroneFactories",          new DetailData { header = "DRONE FACTORY  |  Production",            description = "Specialized facility for producing drone units quickly." } },
        { "PowerBoxes",              new DetailData { header = "POWER BOX  |  Infrastructure",            description = "Infrastructure node built to supply or extend power across the grid." } },
        { "SignalBooster",           new DetailData { header = "SIGNAL BOOSTER  |  Utility",              description = "Utility structure to enhance network and influence range." } },
        { "SignalJammers",           new DetailData { header = "SIGNAL JAMMER  |  Defense",               description = "Defensive and aggressive structure to disrupt enemy networks." } },
        { "Tesseract",               new DetailData { header = "TESSERACT  |  Advanced",                  description = "Highly advanced structure. Powers ALL wires globally when built." } },
        { "Rocketship",              new DetailData { header = "ROCKETSHIP  |  End Game",                 description = "Ultimate end-game structure." } },
        { "TelecomTowers",           new DetailData { header = "TOWER NODE  |  Expansion",                description = "Primary expansion nodes built by Builders. Extends influence territory and connects wires." } },
        { "Canteens",                new DetailData { header = "CANTEEN  |  Recruitment",                 description = "Field recruitment hub. Produces Builders, Foremen, and Technicians." } },
        { "ServiceCenter",           new DetailData { header = "SERVICE CENTER  |  Recruitment",          description = "Specialized recruitment hub. Unlocks Maintenance Crew, Foremen, and IT Personnel." } },
        { "AdvancedServiceCenter",   new DetailData { header = "ADVANCED SERVICE CENTER  |  Recruitment", description = "Upgraded Service Center. Adds Robo Worker and Robo Marshall to the roster." } },
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

    // Always rebuilds so live stats (charges, movement, etc.) stay current.
    public void ShowUnit(Unit unit)
    {
        if (unit == null) return;
        currentTarget = unit;
        Display(BuildUnitHeader(unit), BuildUnitDescription(unit));
    }

    public void ShowBuilding(MonoBehaviour building)
    {
        if (building == null) return;
        if (currentTarget == (object)building && panel.activeSelf) return;
        currentTarget = building;

        if (BuildingDetails.TryGetValue(building.GetType(), out DetailData data))
        { Display(data.header, data.description); return; }

        if (building is StructureNode sn &&
            StructureDetails.TryGetValue(sn.GetRequiredTechFeature(), out DetailData sd))
        { Display(sd.header, sd.description); return; }

        Display(building.GetType().Name.ToUpper(), "No description available.");
    }

    public void Close()
    {
        if (panel == null || !panel.activeSelf) return;

        if (uiAnimator != null)
            uiAnimator.AnimateExit(() => { panel.SetActive(false); currentTarget = null; });
        else
        { panel.SetActive(false); currentTarget = null; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Header builder
    //  Output: "Builder  |  Lv.1"
    // ─────────────────────────────────────────────────────────────────────────

    private string BuildUnitHeader(Unit unit)
    {
        string displayName = UnitMetas.TryGetValue(unit.GetType(), out UnitMeta m)
            ? m.displayName
            : unit.GetType().Name;

        return $"{displayName}  |  Lv.{unit.level}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Description builder  (live stats)
    //  Output:
    //    Primary construction unit.
    //    Actions:   3 / 3
    //    Movement:  0 / 2
    //    Upkeep:    10G / turn
    // ─────────────────────────────────────────────────────────────────────────

    private string BuildUnitDescription(Unit unit)
    {
        var sb = new StringBuilder();

        // Hardcoded description line
        if (UnitMetas.TryGetValue(unit.GetType(), out UnitMeta meta))
            sb.AppendLine(meta.description);

        sb.AppendLine($"Actions:   {unit.CurrentCharges} / {unit.MaxCharges}");
        sb.AppendLine($"Movement:  {unit.movementRemaining} / {unit.moveRange}");
        sb.AppendLine($"Upkeep:    {unit.goldUpkeep}G / turn");

        return sb.ToString().TrimEnd();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private void Display(string header, string description)
    {
        if (headerText      != null) headerText.text      = header;
        if (descriptionText != null) descriptionText.text = description;

        if (!panel.activeSelf)
        {
            panel.SetActive(true);
            if (uiAnimator != null) uiAnimator.PlayEntryAnimation();
        }
    }

    private bool IsPointerOverUI()
    {
        var pd = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pd, results);
        return results.Count > 0;
    }
}