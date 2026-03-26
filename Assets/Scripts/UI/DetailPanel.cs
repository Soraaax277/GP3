using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        // LIVE REFRESH: If we are viewing a unit, keep its stats (movement, charges)
        // updated in real time as it moves or works.
        if (panel != null && panel.activeSelf && currentTarget is Unit unit)
        {
            // Update the text silently so it doesn't trigger the slide-in animation
            Display(BuildUnitHeader(unit), BuildUnitDescription(unit), true);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    // Always rebuilds so live stats (charges, movement, etc.) stay current.
    public void ShowUnit(Unit unit, bool silent = false)
    {
        if (unit == null) return;
        currentTarget = unit;
        Display(BuildUnitHeader(unit), BuildUnitDescription(unit), silent);
    }

    public void ShowBuilding(MonoBehaviour building, bool silent = false)
    {
        if (building == null) return;
        
        bool isAlreadySelected = (currentTarget == (object)building && panel.activeSelf);
        currentTarget = building;

        RefreshBuildingContent(building, silent);
        
        // Always play animation for consistency (even if already selected)
        if (!silent && uiAnimator != null) uiAnimator.PlayEntryAnimation();
    }

    private void RefreshBuildingContent(MonoBehaviour building, bool silent = false)
    {
        if (BuildingDetails.TryGetValue(building.GetType(), out DetailData data))
        { Display(data.header, data.description, silent); return; }

        if (building is TowerNode)
        {
            if (StructureDetails.TryGetValue("TelecomTowers", out DetailData td))
            { Display(td.header, td.description, silent); return; }
        }

        if (building is StructureNode sn)
        { Display(StructureDetails.TryGetValue(sn.GetRequiredTechFeature(), out DetailData sd) ? sd.header : building.GetType().Name.ToUpper(), BuildBuildingDescription(sn), silent); return; }

        Display(building.GetType().Name.ToUpper(), "No description available.", silent);
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

    private string BuildBuildingDescription(StructureNode building)
    {
        var sb = new StringBuilder();

        // Get the base description from the dictionary
        if (StructureDetails.TryGetValue(building.GetRequiredTechFeature(), out DetailData sd))
            sb.AppendLine(sd.description);
        else if (BuildingDetails.TryGetValue(building.GetType(), out DetailData data))
            sb.AppendLine(data.description);

        sb.AppendLine("");
        sb.AppendLine($"Territory Expansion: {building.expansionRadius} Hexes");
        sb.AppendLine($"Status: {(building.IsBuilt ? (building.IsPowered ? "OPERATIONAL" : "UNPOWERED") : "CONSTRUCTING...")}");
        sb.AppendLine($"Maintenance: {building.goldUpkeep}G / turn");

        return sb.ToString().TrimEnd();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private void Display(string header, string description, bool silent = false)
    {
        string finalHeader = header;
        if (currentTarget is TowerNode tn && tn.state == TowerNode.TowerState.Hologram)
            finalHeader = "[UNBUILT] " + header;
        else if (currentTarget is StructureNode sn && !sn.IsBuilt)
            finalHeader = "[UNBUILT] " + header;

        if (headerText      != null) headerText.text      = finalHeader;
        if (descriptionText != null) descriptionText.text = description;

        if (!panel.activeSelf)
        {
            panel.SetActive(true);
            if (!silent && uiAnimator != null) uiAnimator.PlayEntryAnimation();
        }
        else
        {
            // Force re-trigger animation for "switch" feedback when already active
            // ONLY if not silent. Silent updates are for real-time movement.
            if (!silent && uiAnimator != null) uiAnimator.PlayEntryAnimation();
        }

    }

    private bool IsPointerOverUI()
    {
        Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        var pd = new PointerEventData(EventSystem.current) { position = mousePos };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pd, results);
        return results.Count > 0;
    }
}