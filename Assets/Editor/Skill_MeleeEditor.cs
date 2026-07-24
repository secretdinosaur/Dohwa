using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Skill_Melee))]
public class Skill_MeleeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        List<string> excludedProps = new List<string> { "m_Script" };

        bool isEnemy = serializedObject.FindProperty("isEnemySkill").boolValue;

        if (isEnemy)
        {
            // 플레이어 전용 로직 및 기타 필드 전부 명시적 제외
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
                "triggerMaxUsesPerCaster",
            });
        }
        else
        {
            excludedProps.Add("enemyHitEffect");
            if (!serializedObject.FindProperty("isSpecialSkill").boolValue)
            {
                excludedProps.AddRange(new string[] { "triggerType", "triggerThreshold", "triggerCooldown", "triggerChance", "triggerMaxUsesPerCaster" });
            }
        }

        // 1. 여기서 기본 그리기(DrawPropertiesExcluding) 대상에서 제외시킴
        excludedProps.AddRange(new string[] { "useStatusEffects", "guardableData", "stunEffects" });

        DrawPropertiesExcluding(serializedObject, excludedProps.ToArray());

        EditorGUILayout.Space();

        // ---------------------------------------------------------
        // 2. ★ 누락되었던 부분: 커스텀으로 다시 밑에다가 그려줍니다!
        // ---------------------------------------------------------
        EditorGUILayout.LabelField("Status & Guard Settings", EditorStyles.boldLabel);

        SerializedProperty useStatusEffectsProp = serializedObject.FindProperty("useStatusEffects");
        if (useStatusEffectsProp != null) EditorGUILayout.PropertyField(useStatusEffectsProp);

        // true를 넣어야 리스트나 클래스 내부(하위 속성)가 펼쳐져서 보입니다.
        SerializedProperty guardableDataProp = serializedObject.FindProperty("guardableData");
        if (guardableDataProp != null) EditorGUILayout.PropertyField(guardableDataProp, true);

        SerializedProperty stunEffectsProp = serializedObject.FindProperty("stunEffects");
        if (stunEffectsProp != null) EditorGUILayout.PropertyField(stunEffectsProp, true);
        // ---------------------------------------------------------

        serializedObject.ApplyModifiedProperties();
    }
}