using System.Collections.Generic;
using UnityEditor;

[CustomEditor(typeof(Skill_Range))]
public class Skill_RangeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        List<string> excludedProps = new List<string> { "m_Script" };

        SerializedProperty isEnemySkillProp = serializedObject.FindProperty("isEnemySkill");
        bool isEnemy = isEnemySkillProp != null && isEnemySkillProp.boolValue;

        if (isEnemy)
        {
            excludedProps.AddRange(new string[] {
                "Localize_skillName",
                "Localize_skillItemDesc",
                "Localize_skillEffectDesc",
                "enemyHitEffect",
                "commands",
                "isUpgraded",
                "UpgradedSkill",
                "IsAdvancedSkill",
                "isSpecialSkill",
                "effect",
                "icon",
                "triggerType",
                "triggerThreshold",
                "triggerCooldown",
                "triggerChance",
                "triggerMaxUsesPerCaster"
            });
        }
        else
        {
            excludedProps.Add("enemyHitEffect");

            SerializedProperty isSpecialSkillProp = serializedObject.FindProperty("isSpecialSkill");
            bool isSpecial = isSpecialSkillProp != null && isSpecialSkillProp.boolValue;

            if (!isSpecial)
            {
                excludedProps.AddRange(new string[] { "triggerType", "triggerThreshold", "triggerCooldown", "triggerChance", "triggerMaxUsesPerCaster" });
            }
        }

        excludedProps.AddRange(new string[] { "useStatusEffects", "guardableData", "stunEffects" });

        DrawPropertiesExcluding(serializedObject, excludedProps.ToArray());

        EditorGUILayout.Space();
        // 제외했던 프로퍼티들을 하단에 명시적으로 렌더링
        SerializedProperty useStatusEffectsProp = serializedObject.FindProperty("useStatusEffects");
        if (useStatusEffectsProp != null) EditorGUILayout.PropertyField(useStatusEffectsProp);

        SerializedProperty guardableDataProp = serializedObject.FindProperty("guardableData");
        if (guardableDataProp != null) EditorGUILayout.PropertyField(guardableDataProp);

        SerializedProperty stunEffectsProp = serializedObject.FindProperty("stunEffects");
        if (stunEffectsProp != null) EditorGUILayout.PropertyField(stunEffectsProp);

        serializedObject.ApplyModifiedProperties();
    }
}