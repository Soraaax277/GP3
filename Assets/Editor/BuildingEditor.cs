#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector for StructureNode and all subclasses.
/// Organizes every general-purpose building field into labeled, collapsible
/// sections. Per-building logic (BPOCenter income rates, Rocketship launch,
/// etc.) stays in each subclass — this editor only surfaces the shared fields
/// that live on StructureNode itself.
///
/// Place this file anywhere inside an "Editor" folder in your project.
/// Unity will pick it up automatically; no manual registration is needed.
/// </summary>
[CustomEditor(typeof(StructureNode), true)] // true = also apply to all subclasses
public class BuildingEditor : Editor
{
    // ── Foldout state (persisted per-session via EditorPrefs) ───────────────
    private bool showIdentity     = true;
    private bool showFootprint    = true;
    private bool showEconomy      = true;
    private bool showInfluence    = true;
    private bool showVision       = true;
    private bool showDurability   = true;
    private bool showRuntimeState = false;

    // ── Cached SerializedProperties ─────────────────────────────────────────
    // Size / Footprint
    SerializedProperty tilesOccupied;
    SerializedProperty autoScaleToFit;
    SerializedProperty verticalOffset;

    // Economy
    SerializedProperty baseGoldCost;
    SerializedProperty goldUpkeep;

    // Influence / Expansion
    SerializedProperty expansionRadius;
    SerializedProperty baseInfluenceAmount;

    // Vision
    SerializedProperty visionRange;

    // Durability
    SerializedProperty baseDurability;
    SerializedProperty hiddenDurability;

    // Runtime (read-only display)
    SerializedProperty currentDurability;
    SerializedProperty currentHiddenDurability;

    // ── Styles ──────────────────────────────────────────────────────────────
    private GUIStyle _headerStyle;
    private GUIStyle _sectionBoxStyle;
    private GUIStyle _readOnlyStyle;

    private GUIStyle HeaderStyle
    {
        get
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.foldoutHeader)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize  = 12
                };
            }
            return _headerStyle;
        }
    }

    private GUIStyle SectionBoxStyle
    {
        get
        {
            if (_sectionBoxStyle == null)
            {
                _sectionBoxStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(10, 10, 6, 6),
                    margin  = new RectOffset(0, 0, 2, 4)
                };
            }
            return _sectionBoxStyle;
        }
    }

    private GUIStyle ReadOnlyStyle
    {
        get
        {
            if (_readOnlyStyle == null)
            {
                _readOnlyStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = new Color(0.55f, 0.55f, 0.55f) }
                };
            }
            return _readOnlyStyle;
        }
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        // Footprint
        tilesOccupied          = serializedObject.FindProperty("tilesOccupied");
        autoScaleToFit         = serializedObject.FindProperty("autoScaleToFit");
        verticalOffset         = serializedObject.FindProperty("verticalOffset");

        // Economy
        baseGoldCost           = serializedObject.FindProperty("baseGoldCost");
        goldUpkeep             = serializedObject.FindProperty("goldUpkeep");

        // Influence
        expansionRadius        = serializedObject.FindProperty("expansionRadius");
        baseInfluenceAmount    = serializedObject.FindProperty("baseInfluenceAmount");

        // Vision
        visionRange            = serializedObject.FindProperty("visionRange");

        // Durability
        baseDurability         = serializedObject.FindProperty("baseDurability");
        hiddenDurability       = serializedObject.FindProperty("hiddenDurability");

        // Runtime
        currentDurability      = serializedObject.FindProperty("currentDurability");
        currentHiddenDurability= serializedObject.FindProperty("currentHiddenDurability");

        // Restore foldout prefs
        string key = "BE_" + target.GetType().Name;
        showIdentity     = EditorPrefs.GetBool(key + "_identity",     true);
        showFootprint    = EditorPrefs.GetBool(key + "_footprint",    true);
        showEconomy      = EditorPrefs.GetBool(key + "_economy",      true);
        showInfluence    = EditorPrefs.GetBool(key + "_influence",    true);
        showVision       = EditorPrefs.GetBool(key + "_vision",       true);
        showDurability   = EditorPrefs.GetBool(key + "_durability",   true);
        showRuntimeState = EditorPrefs.GetBool(key + "_runtime",      false);
    }

    private void OnDisable()
    {
        // Persist foldout prefs
        string key = "BE_" + target.GetType().Name;
        EditorPrefs.SetBool(key + "_identity",     showIdentity);
        EditorPrefs.SetBool(key + "_footprint",    showFootprint);
        EditorPrefs.SetBool(key + "_economy",      showEconomy);
        EditorPrefs.SetBool(key + "_influence",    showInfluence);
        EditorPrefs.SetBool(key + "_vision",       showVision);
        EditorPrefs.SetBool(key + "_durability",   showDurability);
        EditorPrefs.SetBool(key + "_runtime",      showRuntimeState);
    }

    // ── Draw ─────────────────────────────────────────────────────────────────
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        StructureNode node = (StructureNode)target;

        DrawIdentitySection(node);
        DrawFootprintSection(node);
        DrawEconomySection();
        DrawInfluenceSection();
        DrawVisionSection();
        DrawDurabilitySection();
        DrawRuntimeStateSection(node);

        // ── Subclass-specific fields ─────────────────────────────────────
        // Any [Header] or public field declared on a subclass (e.g. BPOCenter's
        // incomePerBusinessperson) is drawn here automatically by Unity's default
        // renderer, so we don't need to list them individually.
        EditorGUILayout.Space(6);
        DrawPropertiesExcluding(serializedObject,
            // Exclude every field we already drew above so there's no duplication
            "m_Script",
            "tilesOccupied", "autoScaleToFit", "verticalOffset",
            "baseGoldCost", "goldUpkeep",
            "expansionRadius", "baseInfluenceAmount",
            "visionRange",
            "baseDurability", "hiddenDurability",
            "currentDurability", "currentHiddenDurability"
        );

        serializedObject.ApplyModifiedProperties();
    }

    // ── Sections ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the concrete class name and the required tech feature string as
    /// read-only info — useful to confirm at a glance which building this is
    /// without opening the script.
    /// </summary>
    private void DrawIdentitySection(StructureNode node)
    {
        showIdentity = DrawSectionHeader("🏢  Building Identity", showIdentity);
        if (!showIdentity) return;

        EditorGUILayout.BeginVertical(SectionBoxStyle);

        // Class name
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Class", GUILayout.Width(120));
        EditorGUILayout.LabelField(node.GetType().Name, ReadOnlyStyle);
        EditorGUILayout.EndHorizontal();

        // Required tech feature
        string feature = "(unknown)";
        try { feature = node.GetRequiredTechFeature(); } catch { }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Required Tech Feature", GUILayout.Width(120));
        EditorGUILayout.LabelField(feature, ReadOnlyStyle);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Controls tile footprint size, auto-scale, and any vertical lift needed
    /// to keep the model flush with the ground.
    /// </summary>
    private void DrawFootprintSection(StructureNode node)
    {
        showFootprint = DrawSectionHeader("📐  Footprint & Scaling", showFootprint);
        if (!showFootprint) return;

        EditorGUILayout.BeginVertical(SectionBoxStyle);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(tilesOccupied, new GUIContent(
            "Tiles Occupied",
            "How many hex tiles this building claims.\n" +
            "1 = single hex  |  2 = pair  |  4 = quad  |  7 = full ring.\n" +
            "StructurePlacementManager.GetTargetTiles() reads this to pick neighbors."));

        // Friendly footprint label
        int tiles = tilesOccupied.intValue;
        string footprintLabel = tiles switch
        {
            1          => "Single hex",
            2          => "Hex pair (1 + 1 neighbor)",
            int n when n >= 4 && n < 7 => "Quad (1 + 3 neighbors)",
            7          => "Full ring (center + 6 neighbors)",
            _          => "Custom"
        };
        EditorGUILayout.HelpBox($"Footprint: {footprintLabel}", MessageType.None);

        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(autoScaleToFit, new GUIContent(
            "Auto Scale To Fit",
            "When true, AutoScaleToFitTiles() runs on Initialize to resize the\n" +
            "model so it fills its tile footprint. Some buildings override this\n" +
            "further in Start() (e.g. WorkerFactory multiplies by 0.20)."));

        EditorGUILayout.PropertyField(verticalOffset, new GUIContent(
            "Vertical Offset",
            "Extra Y lift applied on top of tile surface height during placement.\n" +
            "Rocketship hard-codes 8.93f here to reach Y=10.24 above the ground."));

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Purchase cost and per-turn upkeep with a live cost-per-tile breakdown.
    /// </summary>
    private void DrawEconomySection()
    {
        showEconomy = DrawSectionHeader("💰  Economy", showEconomy);
        if (!showEconomy) return;

        EditorGUILayout.BeginVertical(SectionBoxStyle);

        EditorGUILayout.PropertyField(baseGoldCost, new GUIContent(
            "Build Cost (Gold)",
            "Deducted from the player's resources when the building is placed.\n" +
            "Tech effects (e.g. 3DPrinter) can skip the cost entirely by calling Build() immediately."));

        EditorGUILayout.PropertyField(goldUpkeep, new GUIContent(
            "Upkeep Per Turn (Gold)",
            "Deducted each turn by EconomyManager.\n" +
            "An era-mismatch multiplier may be applied on top of this."));

        // Cost-per-tile helper
        int tiles = tilesOccupied?.intValue ?? 1;
        if (tiles > 0)
        {
            float perTile = (float)baseGoldCost.intValue / tiles;
            EditorGUILayout.HelpBox(
                $"≈ {perTile:F0}G per tile  |  Upkeep: {goldUpkeep.intValue}G/turn",
                MessageType.None);
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Expansion radius and influence amount. A warning fires if the radius is
    /// unusually large (>5) since that's typically reserved for MicrowaveRelay.
    /// </summary>
    private void DrawInfluenceSection()
    {
        showInfluence = DrawSectionHeader("📡  Influence & Expansion", showInfluence);
        if (!showInfluence) return;

        EditorGUILayout.BeginVertical(SectionBoxStyle);

        EditorGUILayout.PropertyField(expansionRadius, new GUIContent(
            "Expansion Radius",
            "Hex radius over which this building applies / removes influence.\n" +
            "Also used by GetTilesInRange() for mechanic range checks.\n" +
            "MicrowaveRelay = 5  |  Most buildings = 2–3."));

        EditorGUILayout.PropertyField(baseInfluenceAmount, new GUIContent(
            "Base Influence Amount",
            "Influence points applied per tile within expansion radius.\n" +
            "Reduced to 10% before build and 50% while unpowered (see ApplyInfluence)."));

        if (expansionRadius.intValue > 5)
            EditorGUILayout.HelpBox(
                "Radius > 5 is unusually large. Intentional? Only MicrowaveRelay uses 5.",
                MessageType.Warning);

        EditorGUILayout.EndVertical();
    }

    /// <summary>Vision range granted to the owning player's Fog of War.</summary>
    private void DrawVisionSection()
    {
        showVision = DrawSectionHeader("👁  Vision", showVision);
        if (!showVision) return;

        EditorGUILayout.BeginVertical(SectionBoxStyle);

        EditorGUILayout.PropertyField(visionRange, new GUIContent(
            "Vision Range",
            "Hex radius revealed in Fog of War for the owning player.\n" +
            "FieldOfViewManager reads this on placement.\n" +
            "Rocketship = 10  |  WorkerFactory = 4  |  Default = 3."));

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Base and hidden durability. Hidden durability is the second health bar
    /// that only starts draining after the building is broken.
    /// </summary>
    private void DrawDurabilitySection()
    {
        showDurability = DrawSectionHeader("🛡  Durability", showDurability);
        if (!showDurability) return;

        EditorGUILayout.BeginVertical(SectionBoxStyle);

        EditorGUILayout.PropertyField(baseDurability, new GUIContent(
            "Base Durability",
            "Primary HP pool. When this hits 0 the building becomes Broken\n" +
            "(IsBroken = true, IsPowered = false)."));

        EditorGUILayout.PropertyField(hiddenDurability, new GUIContent(
            "Hidden Durability",
            "Secondary HP pool that only drains after the building is Broken.\n" +
            "When this hits 0, DestroyStructure() is called and the object is deleted."));

        EditorGUILayout.HelpBox(
            $"Total effective HP: {baseDurability.floatValue + hiddenDurability.floatValue}",
            MessageType.None);

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Live runtime values — read-only. Only meaningful during Play Mode.
    /// </summary>
    private void DrawRuntimeStateSection(StructureNode node)
    {
        showRuntimeState = DrawSectionHeader("⚙  Runtime State  (Play Mode)", showRuntimeState);
        if (!showRuntimeState) return;

        EditorGUILayout.BeginVertical(SectionBoxStyle);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to see live values.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        // Power & build state
        DrawReadOnlyBool("Is Built",    node.IsBuilt);
        DrawReadOnlyBool("Is Powered",  node.IsPowered);
        DrawReadOnlyBool("Is Broken",   node.IsBroken);

        EditorGUILayout.Space(4);

        // Live HP bars
        float maxHP = baseDurability.floatValue;
        float curHP = currentDurability.floatValue;
        if (maxHP > 0)
        {
            EditorGUILayout.LabelField("Current Durability", $"{curHP:F0} / {maxHP:F0}");
            Rect r = EditorGUILayout.GetControlRect(false, 8);
            EditorGUI.DrawRect(r, new Color(0.2f, 0.2f, 0.2f));
            r.width *= Mathf.Clamp01(curHP / maxHP);
            EditorGUI.DrawRect(r, node.IsBroken ? Color.red : Color.green);
        }

        float maxHidden = hiddenDurability.floatValue;
        float curHidden = currentHiddenDurability.floatValue;
        if (maxHidden > 0)
        {
            EditorGUILayout.LabelField("Hidden Durability", $"{curHidden:F0} / {maxHidden:F0}");
            Rect r2 = EditorGUILayout.GetControlRect(false, 8);
            EditorGUI.DrawRect(r2, new Color(0.2f, 0.2f, 0.2f));
            r2.width *= Mathf.Clamp01(curHidden / maxHidden);
            EditorGUI.DrawRect(r2, new Color(1f, 0.5f, 0f));
        }

        EditorGUILayout.Space(4);

        // Owner
        string ownerName = node.owner != null ? node.owner.playerName : "None";
        EditorGUILayout.LabelField("Owner", ownerName);

        // Parent tile
        string tileName = node.ParentTile != null ? node.ParentTile.name : "None";
        EditorGUILayout.LabelField("Parent Tile", tileName);

        EditorGUILayout.EndVertical();

        // Repaint every frame in Play Mode so the HP bars update live
        if (Application.isPlaying) Repaint();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Draws a styled foldout header and returns the new open/closed state.
    /// </summary>
    private bool DrawSectionHeader(string label, bool currentState)
    {
        EditorGUILayout.Space(2);
        bool newState = EditorGUILayout.BeginFoldoutHeaderGroup(currentState, label, HeaderStyle);
        EditorGUILayout.EndFoldoutHeaderGroup();
        return newState;
    }

    /// <summary>Draws a read-only bool row with a colored indicator dot.</summary>
    private void DrawReadOnlyBool(string label, bool value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth));
        Color prev = GUI.color;
        GUI.color = value ? Color.green : new Color(0.5f, 0.5f, 0.5f);
        EditorGUILayout.LabelField(value ? "✔ Yes" : "✘ No", ReadOnlyStyle);
        GUI.color = prev;
        EditorGUILayout.EndHorizontal();
    }
}
#endif
