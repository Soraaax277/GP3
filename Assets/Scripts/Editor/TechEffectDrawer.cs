// custom property drawer for TechEffect, which shows different fields based on the selected EffectType

using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(TechEffect))]
public class TechEffectDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var typeProp = property.FindPropertyRelative("type");
        EffectType type = (EffectType)typeProp.enumValueIndex;
        
        // Start with the height of the "Type" field
        float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        switch (type)
        {
            case EffectType.UpgradeUnitStat:
                height += GetHeight("targetUnits", property);
                height += GetHeight("statToUpgrade", property);
                height += GetHeight("amount", property);
                break;

            case EffectType.UnlockUnit:
                height += GetHeight("targetUnits", property);
                break;

            case EffectType.UnlockSkill:
                height += GetHeight("targetUnits", property);
                height += GetHeight("skillName", property);
                break;

            case EffectType.UpgradeInfrastructure:
                height += GetHeight("infraStatName", property);
                height += GetHeight("infraValueMod", property);
                height += GetHeight("isMultiplier", property);
                break;

            case EffectType.UnlockFeature:
                height += GetHeight("featureName", property);
                break;

            // NEW: UpgradePlayerEra — only needs the isHardwareEra toggle
            case EffectType.UpgradePlayerEra:
                height += GetHeight("isHardwareEra", property);
                break;
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Draw the Type dropdown
        Rect typeRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var typeProp = property.FindPropertyRelative("type");
        EditorGUI.PropertyField(typeRect, typeProp, new GUIContent("Effect Type"));

        // Move down for the next fields
        float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EffectType type = (EffectType)typeProp.enumValueIndex;

        switch (type)
        {
            case EffectType.UpgradeUnitStat:
                DrawProp("targetUnits", property, ref y, position);
                DrawProp("statToUpgrade", property, ref y, position);
                DrawProp("amount", property, ref y, position);
                break;

            case EffectType.UnlockUnit:
                DrawProp("targetUnits", property, ref y, position);
                break;

            case EffectType.UnlockSkill:
                DrawProp("targetUnits", property, ref y, position);
                DrawProp("skillName", property, ref y, position);
                break;

            case EffectType.UpgradeInfrastructure:
                DrawProp("infraStatName", property, ref y, position);
                DrawProp("infraValueMod", property, ref y, position);
                DrawProp("isMultiplier", property, ref y, position);
                break;

            case EffectType.UnlockFeature:
                DrawProp("featureName", property, ref y, position);
                break;

            // NEW: UpgradePlayerEra
            // Shows a single toggle: checked = Hardware Era, unchecked = Workforce Era.
            // A help box below reminds the designer what each option does.
            case EffectType.UpgradePlayerEra:
                DrawProp("isHardwareEra", property, ref y, position);

                // Inline hint so designers don't have to leave the Inspector
                var isHardware = property.FindPropertyRelative("isHardwareEra").boolValue;
                string hint = isHardware
                    ? "✔ Hardware Era  →  reduces the obsolete-tech INFLUENCE penalty\n   (advance when World Era outpaces the player's tech)"
                    : "✘ Workforce Era  →  reduces the labor-mismatch UPKEEP penalty\n   (advance to match an already-upgraded Hardware Era)";

                float hintHeight = EditorGUIUtility.singleLineHeight * 2.5f;
                Rect hintRect = new Rect(position.x, y, position.width, hintHeight);
                EditorGUI.HelpBox(hintRect, hint, MessageType.Info);
                break;
        }

        EditorGUI.EndProperty();
    }

    private void DrawProp(string propName, SerializedProperty rootProp, ref float y, Rect totalPosition)
    {
        SerializedProperty prop = rootProp.FindPropertyRelative(propName);
        float height = EditorGUI.GetPropertyHeight(prop, true);

        Rect rect = new Rect(totalPosition.x, y, totalPosition.width, height);

        EditorGUI.indentLevel++;
        EditorGUI.PropertyField(rect, prop, true);
        EditorGUI.indentLevel--;

        y += height + EditorGUIUtility.standardVerticalSpacing;
    }

    private float GetHeight(string propName, SerializedProperty rootProp)
    {
        SerializedProperty prop = rootProp.FindPropertyRelative(propName);
        return EditorGUI.GetPropertyHeight(prop, true) + EditorGUIUtility.standardVerticalSpacing;
    }
}