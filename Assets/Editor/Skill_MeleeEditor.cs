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
                // "enemyHitEffect",  // <--- 적 스킬의 피격 이펙트가 보이도록 제외 목록에서 제거
                "commands",
                "isUpgraded",
                "UpgradedSkill",
                "IsAdvancedSkill",
                "isSpecialSkill",
                // "effect",          // <--- 적 스킬의 시전 이펙트가 보이도록 제외 목록에서 제거
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
            // 플레이어 스킬일 경우 (필요에 따라 enemyHitEffect 숨김 처리)
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
        // 2. 커스텀으로 다시 밑에다가 그려줍니다!
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